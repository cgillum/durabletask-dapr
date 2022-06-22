// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Dapr.Activities;
using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr.Workflows;

class WorkflowActor : ReliableActor, IWorkflowActor
{
    readonly IWorkflowExecutor workflowScheduler;

    WorkflowState? state;

    public WorkflowActor(ActorHost host, ILoggerFactory loggerFactory, IWorkflowExecutor workflowScheduler)
        : base(host, loggerFactory)
    {
        this.workflowScheduler = workflowScheduler ?? throw new ArgumentNullException(nameof(workflowScheduler));
    }

    async Task IWorkflowActor.InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses)
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
        await this.CreateReliableReminder("init", state: null, delay: TimeSpan.Zero);

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

    protected override async Task OnReminderReceivedAsync(string reminderName, byte[] state)
    {
        await this.TryLoadStateAsync();
        if (this.state == null)
        {
            // The actor may have failed to save its state after being created. The client should have already received
            // an error message associated with the failure, so we can safely drop this reminder.
            this.Log.WorkflowStateNotFound(this.Id, reminderName);
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
                this.Log.TimerNotFound(this.state.InstanceId, reminderName);
            }
        }

        if (!this.state.Inbox.Any())
        {
            // Nothing to do! This isn't expected.
            this.Log.InboxIsEmpty(this.state.InstanceId, reminderName);
            return;
        }

        this.state.SequenceNumber++;

        WorkflowExecutionResult result = await this.workflowScheduler.ExecuteWorkflowStepAsync(
            this.state.InstanceId,
            this.state.Inbox,
            this.state.History);

        switch (result.Type)
        {
            case ExecutionResultType.Executed:
                // Persist the changes to the data store
                DateTime utcNow = DateTime.UtcNow;
                this.state.OrchestrationStatus = result.UpdatedState.OrchestrationStatus;
                this.state.LastUpdatedTimeUtc = utcNow;
                this.state.Output = result.UpdatedState.Output;
                this.state.CustomStatus = result.UpdatedState.Status;

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
                        return this.CreateReliableReminder(GetReminderNameForTimer(timer), delay: reminderDelay);
                    });

                    // Process outbox messages
                    IReadOnlyList<ActivityInvocationRequest> activityRequests = result.Outbox
                        .Where(msg => msg.Event.EventType == EventType.TaskScheduled)
                        .Select(msg =>
                        {
                            TaskScheduledEvent taskEvent = (TaskScheduledEvent)msg.Event;
                            return new ActivityInvocationRequest(
                                taskEvent.Name!,
                                taskEvent.EventId,
                                taskEvent.Input,
                                msg.OrchestrationInstance.InstanceId,
                                msg.OrchestrationInstance.ExecutionId);
                        })
                        .ToList();

                    if (activityRequests.Count > 0)
                    {
                        this.Log.SchedulingActivityTasks(
                            this.state.InstanceId,
                            activityRequests.Count,
                            string.Join(", ", activityRequests));

                        // Each activity invocation gets triggered in parallel by its own stateless actor.
                        // The task ID of the activity is used to uniquely identify the activity for this
                        // particular workflow instance.
                        await activityRequests.ParallelForEachAsync(request =>
                        {
                            IActivityActor activityInvokerProxy = ActorProxy.Create<IActivityActor>(
                                new ActorId($"{this.state.InstanceId}:activity:{request.TaskId}"),
                                nameof(StatelessActivityActor),
                                new ActorProxyOptions());
                            return activityInvokerProxy.InvokeAsync(request);
                        });
                    }
                }

                // At this point, the inbox should be fully processed.
                this.state.Inbox.Clear();

                // Append the new history
                // TODO: Move each history event into its own key for better scalability and reduced I/O (esp. writes).
                //       Alternatively, key by sequence number to reduce the number of roundtrips when loading state.
                this.state.History.AddRange(result.NewHistoryEvents);

                // RELIABILITY:
                // Saving state needs to be the last step to ensure that a crash doesn't cause us to lose any work.
                // If there is a crash, there should be a reminder that wakes us up and causes us to reprocess the
                // latest orchestration state.
                // TODO: Need to implement this persistent reminder...
                await this.StateManager.SetStateAsync("state", this.state);
                await this.StateManager.SaveStateAsync();

                break;
            case ExecutionResultType.Throttled:
                // TODO: Exponential backoff with some randomness to avoid thundering herd problem.
                await this.CreateReliableReminder("retry", delay: TimeSpan.FromSeconds(30));
                break;
            case ExecutionResultType.Abandoned:
                await this.CreateReliableReminder("retry", delay: TimeSpan.FromSeconds(5));
                break;
        }

        await this.UnregisterReminderAsync(reminderName);
    }

    async Task<OrchestrationState?> IWorkflowActor.GetCurrentStateAsync()
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
        this.Log.FetchingWorkflowState(this.Id);
        ConditionalValue<WorkflowState> result = await this.StateManager.TryGetStateAsync<WorkflowState>("state");
        if (result.HasValue)
        {
            this.state = result.Value;
        }

        return result;
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

    // This is the API used for external events, termination, etc.
    async Task IWorkflowActor.PostToInboxAsync(TaskMessage message)
    {
        await this.TryLoadStateAsync();
        if (this.state == null)
        {
            // TODO: If we want to support Durable Entities, we could allow well formatted external event messages
            //       to invoke the Init flow if there isn't already state associated with the workflow.
            this.Log.WorkflowStateNotFound(this.Id, "post-to-inbox");
            return;
        }

        this.state.Inbox.Add(message);

        // Save the state after scheduling the reminder to ensure 
        await this.StateManager.SetStateAsync("state", this.state);
        await this.StateManager.SaveStateAsync();

        // This reminder will trigger the main workflow loop
        await this.CreateReliableReminder("received-inbox-message");
    }

    // This is the callback from the activity worker actor when an activity execution completes
    async Task IWorkflowActor.CompleteActivityAsync(ActivityCompletionResponse completionInfo)
    {
        await this.TryLoadStateAsync();
        if (this.state == null)
        {
            // The actor may have failed to save its state after being created. The client should have already received
            // an error message associated with the failure, so we can safely drop this reminder.
            this.Log.WorkflowStateNotFound(this.Id, "activity-callback");
            return;
        }

        HistoryEvent historyEvent;
        if (completionInfo.FailureDetails == null)
        {
            historyEvent = new TaskCompletedEvent(-1, completionInfo.TaskId, completionInfo.SerializedResult);
        }
        else
        {
            historyEvent = new TaskFailedEvent(-1, completionInfo.TaskId, null, null, completionInfo.FailureDetails);
        }

        // TODO: De-dupe any task completion events that we've already seen

        this.state.Inbox.Add(this.state.NewMessage(historyEvent));
        await this.StateManager.SetStateAsync("state", this.state);
        await this.StateManager.SaveStateAsync();

        // This reminder will trigger the main workflow loop
        await this.CreateReliableReminder("task-completed");
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
        public int SequenceNumber { get; set; }

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
