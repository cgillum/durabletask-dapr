// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors;
using DurableTask.Core;

namespace DurableTask.Dapr;

public interface IOrchestrationActor : IActor
{
    /// <summary>
    /// Initializes a new orchestration actor with a ExecutionStarted message.
    /// </summary>
    /// <param name="creationMessage">The message containing the ExecutionStarted history event.</param>
    /// <param name="dedupeStatuses">
    /// Fail the init operation if an orchestration with one of these status values already exists.
    /// </param>
    /// <returns>A task that completes when the actor has finished initializing.</returns>
    Task InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses);

    /// <summary>
    /// Returns the current state of the orchestration.
    /// </summary>
    Task<OrchestrationState?> GetCurrentStateAsync();
}
