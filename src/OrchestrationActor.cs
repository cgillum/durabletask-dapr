﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using Dapr.Actors.Runtime;
using DurableTask.Core;
using DurableTask.Core.History;

namespace DurableTask.Dapr;

// NOTE: This code runs on the container where the app code lives
class DaprWorkflowScheduler : Actor, IOrchestrationActor, IRemindable
{
    const string OrchestrationReminder = "background-execute";

    readonly IWorkflowScheduler workflowScheduler;

    InternalState? state;

    public DaprWorkflowScheduler(ActorHost host, IWorkflowScheduler workflowScheduler)
        : base(host)
    {
        this.workflowScheduler = workflowScheduler ?? throw new ArgumentNullException(nameof(workflowScheduler));
    }

    public async Task InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses)
    {
        ConditionalValue<InternalState> existing = await this.StateManager.TryGetStateAsync<InternalState>("state");

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
        await this.CreateOrchestratorReminder(delay: TimeSpan.Zero);

        // Cache the state in memory so that we don't have to fetch it again when the reminder fires.
        // TODO: This state ideally needs to be split across multiple records.
        DateTime now = DateTime.UtcNow;
        this.state = new InternalState
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

        await this.StateManager.SetStateAsync("state", this.state);
    }

    async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        if (this.state == null)
        {
            // The actor may have been unloaded from memory. Refetch it's internal state.
            this.state = await this.StateManager.GetStateAsync<InternalState>("state");
        }

        if (this.state == null)
        {
            // The actor may have failed to save its state after being created. The client should have already received
            // an error message associated with the failure, so we can safely drop this reminder.
            return;
        }

        WorkflowExecutionResult result = await this.workflowScheduler.ExecuteWorkflowAsync(
            this.state.InstanceId,
            this.state.Inbox,
            this.state.History);

        switch (result.Type)
        {
            case WorkflowExecutionResultType.Executed:
                // TODO: Process the items in the outbox
                break;
            case WorkflowExecutionResultType.Throttled:
                // TODO: Exponential backoff with some randomness to avoid thundering herd problem.
                await this.CreateOrchestratorReminder(delay: TimeSpan.FromSeconds(30));
                break;
            case WorkflowExecutionResultType.Abandoned:
                await this.CreateOrchestratorReminder(delay: TimeSpan.FromSeconds(5));
                break;
        }
    }

    Task UpdateWorkflowStateAsync(WorkflowExecutionResult result)
    {
        this.state!.OrchestrationStatus = result.UpdatedState.OrchestrationStatus;
        this.state!.LastUpdatedTimeUtc = DateTime.UtcNow;
        this.state!.Output = result.UpdatedState.Output;

        // REVIEW: What does this method do? Does it implicitly know what state to update?
        return this.StateManager.SaveStateAsync();
    }

    Task CreateOrchestratorReminder(TimeSpan delay)
    {
        // All orchestrator reminders have the same name and are non-repeating
        return this.RegisterReminderAsync(
            OrchestrationReminder,
            null,
            delay,
            TimeSpan.FromMilliseconds(-1));
    }

    class InternalState
    {
        public OrchestrationStatus OrchestrationStatus { get; set; }
        public string Name { get; init; } = "";
        public string InstanceId { get; init; } = "";
        public string ExecutionId { get; init; } = "";
        public string? Input { get; init; }
        public string? Output { get; set; }
        public DateTime CreatedTimeUtc { get; init; }
        public DateTime LastUpdatedTimeUtc { get; set; }
        public DateTime? CompletedTimeUtc { get; set; }
        public List<TaskMessage> Inbox { get; } = new List<TaskMessage>();
        public List<HistoryEvent> History { get; } = new List<HistoryEvent>();
    }
}
