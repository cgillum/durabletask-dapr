// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors;
using DurableTask.Core;
using DurableTask.Dapr.Activities;

namespace DurableTask.Dapr.Workflows;

/// <summary>
/// Interface for interacting with workflow actors.
/// </summary>
public interface IWorkflowActor : IActor
{
    /// <summary>
    /// Initializes a workflow actor with an ExecutionStarted message for creating a workflow orchestration instance.
    /// </summary>
    /// <param name="creationMessage">The message containing the ExecutionStarted history event.</param>
    /// <param name="dedupeStatuses">
    /// Fail the init operation if an orchestration with one of these status values already exists.
    /// </param>
    /// <returns>A task that completes when the actor has finished initializing.</returns>
    Task InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses);

    /// <summary>
    /// Returns the current state of the workflow.
    /// </summary>
    Task<OrchestrationState?> GetCurrentStateAsync();

    /// <summary>
    /// Marks an activity execution as completed.
    /// </summary>
    /// <param name="completionInfo">
    /// Additional information about the activity completion, such as the ID of the activity task, the result, etc.
    /// </param>
    /// <returns>A task that completes when the activity completion is stored in the workflow state.</returns>
    Task CompleteActivityAsync(ActivityCompletionResponse completionInfo);

    /// <summary>
    /// Marks a sub-orchestration execution as completed.
    /// </summary>
    /// <param name="message">The message containing the sub-orchestration completion details.</param>
    /// <returns>A task that completes when the sub-orchestration completion is successfully recorded.</returns>
    Task CompleteSubOrchestrationAsync(TaskMessage message);

    /// <summary>
    /// Posts a message to the workflow's inbox.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <returns>A task that completes when the message is successfully persisted.</returns>
    Task PostToInboxAsync(TaskMessage message);
}
