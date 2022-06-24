// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DurableTask.Dapr.Activities;

/// <summary>
/// Interface for objects that can implement activity execution logic.
/// </summary>
interface IActivityExecutor
{
    /// <summary>
    /// Executes the activities logic and returns the results.
    /// </summary>
    /// <param name="request">The activity execution parameters.</param>
    /// <returns>The result of the activity execution.</returns>
    Task<ActivityCompletionResponse> ExecuteActivityAsync(ActivityInvocationRequest request);
}
