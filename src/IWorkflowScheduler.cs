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
    IList<TaskMessage> Timers,
    IList<TaskMessage> Outbox,
    IList<HistoryEvent> NewHistoryEvents);

public enum WorkflowExecutionResultType
{
    /// <summary>
    /// The workflow executed successfully.
    /// </summary>
    Executed,

    /// <summary>
    /// The workflow was unable to run due to concurrency throttles.
    /// </summary>
    Throttled,

    /// <summary>
    /// There was a problem executing the workflow and the results, if any, should be discarded.
    /// The workflow should be retried again later.
    /// </summary>
    Abandoned,
}