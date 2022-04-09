// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DurableTask.Core;
using DurableTask.Core.History;

namespace DurableTask.Dapr;

public interface IWorkflowScheduler
{
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        string instanceId,
        IList<TaskMessage> inbox,
        IList<HistoryEvent> history);
}

// TODO: move to its own file
public record WorkflowExecutionResult(
    WorkflowExecutionResultType Type,
    OrchestrationState UpdatedState,
    IList<TaskMessage> Inbox,
    IList<TaskMessage> Outbox,
    IList<HistoryEvent> NewHistoryEvents);

public enum WorkflowExecutionResultType
{
    Executed,
    Throttled,
    Abandoned,
}