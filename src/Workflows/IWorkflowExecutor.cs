// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DurableTask.Core;
using DurableTask.Core.History;

namespace DurableTask.Dapr.Workflows;

/// <summary>
/// Interface for types that are able to execute workflow logic.
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// Executes the next step in a workflow's logic and returns the results of that execution.
    /// </summary>
    /// <param name="instanceId">The instance ID of the workflow.</param>
    /// <param name="inbox">The set of new events to be processed by the workflow.</param>
    /// <param name="history">The existing history state of the workflow.</param>
    /// <returns>
    /// A task that completes when the workflow step execution completes. The result of this task is the actions
    /// that were scheduled by the workflow.
    /// </returns>
    Task<WorkflowExecutionResult> ExecuteWorkflowStepAsync(
        string instanceId,
        IList<TaskMessage> inbox,
        IList<HistoryEvent> history);
}

/// <summary>
/// The output of the workflow execution step.
/// </summary>
/// <param name="Type">The type of the result - e.g. whether the execution ran, got throttled, aborted, etc.</param>
/// <param name="UpdatedState">The updated orchestration state associated with the workflow instance.</param>
/// <param name="Timers">Any scheduled timers.</param>
/// <param name="Outbox">Any scheduled activity invocation.</param>
/// <param name="NewHistoryEvents">The set of history events to append to the workflow orchestration state.</param>
public record WorkflowExecutionResult(
    ExecutionResultType Type,
    OrchestrationState UpdatedState,
    IList<TaskMessage> Timers,
    IList<TaskMessage> Outbox,
    IList<HistoryEvent> NewHistoryEvents);

/// <summary>
/// Represents the set of outcome types for a workflow execution step.
/// </summary>
public enum ExecutionResultType
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
