// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors;

namespace DurableTask.Dapr.Activities;

/// <summary>
/// Interface for the workflow activity actor.
/// </summary>
public interface IActivityActor : IActor
{
    /// <summary>
    /// Triggers an activity to execute.
    /// </summary>
    /// <remarks>
    /// The result of the activity is delivered asynchronously back to the orchestration that scheduled it.
    /// </remarks>
    /// <param name="request">The activity invocation parameters.</param>
    /// <returns>Returns a task that completes when the activity invocation is reliably scheduled.</returns>
    Task InvokeAsync(ActivityInvocationRequest request);
}
