// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using DurableTask.Core;

namespace DurableTask.Dapr.Activities;

/// <summary>
/// The result of a workflow activity execution.
/// </summary>
[DataContract]
public class ActivityCompletionResponse
{
    /// <summary>
    /// Gets the orchestration-specific ID of the activity task.
    /// </summary>
    [DataMember]
    public int TaskId { get; init; }

    /// <summary>
    /// Gets the serialized output of the activity.
    /// </summary>
    [DataMember]
    public string? SerializedResult { get; set; }

    /// <summary>
    /// If the activity execution resulted in a failure, gets the details of the failure.
    /// Otherwise, this property will return <c>null</c>.
    /// </summary>
    [DataMember]
    public FailureDetails? FailureDetails { get; set; }
}
