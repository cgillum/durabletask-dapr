// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors;
using DurableTask.Core;

namespace DurableTask.Dapr;

public interface IOrchestrationActor : IActor
{
    Task InitAsync(TaskMessage creationMessage, OrchestrationStatus[] dedupeStatuses);
}
