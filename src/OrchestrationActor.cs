// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Dapr.Actors.Runtime;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

// NOTE: This code runs on the container where the app code lives
class DaprWorkflowScheduler : Actor, IOrchestrationActor, IRemindable
{
    readonly IWorkflowScheduler workflowScheduler;
    readonly ILogger log;

    WorkflowState? state;

    public DaprWorkflowScheduler(ActorHost host, IWorkflowScheduler workflowScheduler, ILoggerFactory loggerFactory)
        : base(host)
    {
        this.workflowScheduler = workflowScheduler ?? throw new ArgumentNullException(nameof(workflowScheduler));
        this.log = loggerFactory.CreateLoggerForDaprProvider();
    }

    async Task IOrchestrationActor.InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses)
    {
        ConditionalValue<WorkflowState> existing = await this.TryLoadStateAsync();

        // NOTE: Validation must happen before the reminder is scheduled.
        // De-dupe logic
        if (existing.HasValue && dedupeStatuses.Contains(existing.Value.OrchestrationStatus))
        {
            // TODO: Use a more specific exception type
            throw new InvalidOperationException("This orchestration already exists!");
        }

        if (creationMessage.Event is not ExecutionStartedEvent startEvent)
        {
            throw new ArgumentException($"Only {nameof(ExecutionStartedEvent)} messages can be used to initialize workflows.");
        }

        // Schedule a reminder to execute immediately after this operation. The reminder will trigger the actual
        // orchestration execution. This is preferable to using the current thread so that we don't block the client
        // while the workflow logic is running.
        //
        // RELIABILITY:
        // This reminder must be scheduled before the state update to ensure that an unexpected process failure
        // doesn't result in orphaned state in the state store.
        await this.CreateOrchestratorReminder("init", delay: TimeSpan.Zero);

        // Cache the state in memory so that we don't have to fetch it again when the reminder fires.
        DateTime now = DateTime.UtcNow;
        this.state = new WorkflowState
        {
            OrchestrationStatus = OrchestrationStatus.Pending,
            Name = startEvent.Name,
            Input = startEvent.Input,
            InstanceId = startEvent.OrchestrationInstance.InstanceId,
            ExecutionId = startEvent.OrchestrationInstance.ExecutionId,
            CreatedTimeUtc = now,
            LastUpdatedTimeUtc = now,
            Inbox = { creationMessage },
        };

        // Persist the initial "Pending" state to the state store/
        //
        // RELIABILITY:
        // If a crash occurs before the state is saved, the previously scheduled reminder should fail to find the
        // initial workflow state and won't schedule any workflow execution. This is the correct behavior.
        await this.StateManager.SetStateAsync("state", this.state);
        await this.StateManager.SaveStateAsync();
    }

    async Task IRemindable.ReceiveReminderAsync(
        string reminderName,
        byte[] reminderState,
        TimeSpan dueTime,
        TimeSpan period)
    {
        this.log.ReminderFired(this.Id.ToString(), reminderName);

        await this.TryLoadStateAsync();
        if (this.state == null)
        {
            // The actor may have failed to save its state after being created. The client should have already received
            // an error message associated with the failure, so we can safely drop this reminder.
            this.log.WorkflowStateNotFound(this.Id.ToString(), reminderName);
            return;
        }

        if (IsReminderForDurableTimer(reminderName))
        {
            if (this.state.Timers.TryGetValue(reminderName, out TimerFiredEvent? timerEvent))
            {
                this.state.Inbox.Add(this.state.NewMessage(timerEvent));
            }
            else
            {
                this.log.TimerNotFound(this.state.InstanceId, reminderName);
            }
        }

        if (!this.state.Inbox.Any())
        {
            // Nothing to do! This isn't expected.
            this.log.InboxIsEmpty(this.state.InstanceId, reminderName);
            return;
        }

        WorkflowExecutionResult result = await this.workflowScheduler.ExecuteWorkflowAsync(
            this.state.InstanceId,
            this.state.Inbox,
            this.state.History);

        switch (result.Type)
        {
            case WorkflowExecutionResultType.Executed:
                // Persist the changes to the data store
                DateTime utcNow = DateTime.UtcNow;
                this.state.OrchestrationStatus = result.UpdatedState.OrchestrationStatus;
                this.state.LastUpdatedTimeUtc = utcNow;
                this.state.Output = result.UpdatedState.Output;

                if (result.UpdatedState.OrchestrationStatus == OrchestrationStatus.Completed ||
                    result.UpdatedState.OrchestrationStatus == OrchestrationStatus.Failed ||
                    result.UpdatedState.OrchestrationStatus == OrchestrationStatus.Terminated)
                {
                    this.state.CompletedTimeUtc = utcNow;
                }
                else
                {
                    // Schedule reminders for durable timers
                    await result.Timers.ParallelForEachAsync(item =>
                    {
                        TimerFiredEvent timer = (TimerFiredEvent)item.Event;
                        lock (this.state.Timers)
                        {
                            this.state.Timers.Add(timer);
                        }

                        TimeSpan reminderDelay = timer.FireAt.Subtract(utcNow).PositiveOrZero();
                        return this.CreateOrchestratorReminder(GetReminderNameForTimer(timer), reminderDelay);
                    });

                    // TODO: Process outbox messages
                }

                // At this point, the inbox should be fully processed.
                this.state.Inbox.Clear();

                // Append the new history
                this.state.History.AddRange(result.NewHistoryEvents);

                // RELIABILITY:
                // Saving state needs to be the last step to ensure that a crash doesn't cause us to lose any work.
                // If there is a crash, there should be a reminder that wakes us up and causes us to reprocess the
                // latest orchestration state.
                // TODO: Need to implement this persistent reminder...
                await this.StateManager.SaveStateAsync();

                break;
            case WorkflowExecutionResultType.Throttled:
                // TODO: Exponential backoff with some randomness to avoid thundering herd problem.
                await this.CreateOrchestratorReminder("retry", delay: TimeSpan.FromSeconds(30));
                break;
            case WorkflowExecutionResultType.Abandoned:
                await this.CreateOrchestratorReminder("retry", delay: TimeSpan.FromSeconds(5));
                break;
        }
    }

    async Task<OrchestrationState?> IOrchestrationActor.GetCurrentStateAsync()
    {
        ConditionalValue<WorkflowState> existing = await this.TryLoadStateAsync();
        if (existing.HasValue)
        {
            WorkflowState internalState = existing.Value;
            return new OrchestrationState
            {
                CompletedTime = internalState.CompletedTimeUtc.GetValueOrDefault(),
                CreatedTime = internalState.CreatedTimeUtc,
                FailureDetails = null /* TODO */,
                Input = internalState.Input,
                LastUpdatedTime = internalState.LastUpdatedTimeUtc,
                Name = internalState.Name,
                OrchestrationInstance = new OrchestrationInstance
                {
                    InstanceId = internalState.InstanceId,
                    ExecutionId = internalState.ExecutionId,
                },
                OrchestrationStatus = internalState.OrchestrationStatus,
                Output = internalState.Output,
                ParentInstance = null /* TODO */,
                Status = internalState.CustomStatus,
            };
        }

        return null;
    }

    async Task<ConditionalValue<WorkflowState>> TryLoadStateAsync()
    {
        // Cache hit?
        if (this.state != null)
        {
            return new ConditionalValue<WorkflowState>(true, this.state);
        }

        // Cache miss
        this.log.FetchingWorkflowState(this.Id.ToString());
        ConditionalValue<WorkflowState> result = await this.StateManager.TryGetStateAsync<WorkflowState>("state");
        if (result.HasValue)
        {
            this.state = result.Value;
        }

        return result;
    }

    Task CreateOrchestratorReminder(string reminderName, TimeSpan delay)
    {
        // Non-repeating reminder
        TimeSpan periodForNoRecurrence = TimeSpan.FromMilliseconds(-1);

        this.log.CreatingReminder(this.Id.ToString(), reminderName, delay, periodForNoRecurrence);
        return this.RegisterReminderAsync(reminderName, null, delay, periodForNoRecurrence);
    }

    static string GetReminderNameForTimer(TimerFiredEvent e)
    {
        // The TimerId is unique for every timer event for a particular orchestration in the Durable Task Framework.
        // WARNING: Do not change this naming convention since that could put timers out of sync with existing reminders.
        return $"timer-{e.TimerId}";
    }

    static bool IsReminderForDurableTimer(string reminderName)
    {
        return reminderName.StartsWith("timer-");
    }

    /// <summary>
    /// A collection of pending durable timers that gets saved into workflow state.
    /// </summary>
    /// <remarks>
    /// This collection contains one record for every durable timer scheduled by a specific orchestration instance.
    /// This collection supports O(1) key-based lookups, where the key is the name of the associated reminder in Dapr.
    /// </remarks>
    class TimerCollection : KeyedCollection<string, TimerFiredEvent>
    {
        protected override string GetKeyForItem(TimerFiredEvent e)
        {
            return GetReminderNameForTimer(e);
        }
    }

    class WorkflowState
    {
        public OrchestrationStatus OrchestrationStatus { get; set; }
        public string Name { get; init; } = "";
        public string InstanceId { get; init; } = "";
        public string ExecutionId { get; init; } = "";
        [DataMember(EmitDefaultValue = false)]
        public string? Input { get; init; }
        [DataMember(EmitDefaultValue = false)]
        public string? Output { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public string? CustomStatus { get; set; }
        public DateTime CreatedTimeUtc { get; init; }
        public DateTime LastUpdatedTimeUtc { get; set; }
        [DataMember(EmitDefaultValue = false)]
        public DateTime? CompletedTimeUtc { get; set; }
        public List<TaskMessage> Inbox { get; } = new List<TaskMessage>();
        public TimerCollection Timers { get; } = new();
        public List<HistoryEvent> History { get; } = new List<HistoryEvent>();

        internal TaskMessage NewMessage(HistoryEvent e)
        {
            return new TaskMessage
            {
                Event = e,
                OrchestrationInstance = new OrchestrationInstance
                {
                    InstanceId = this.InstanceId,
                    ExecutionId = this.ExecutionId,
                },
            };
        }
    }
}
