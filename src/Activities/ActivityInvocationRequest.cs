// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace DurableTask.Dapr.Activities;

/// <summary>
/// The parameters of an activity invocation.
/// </summary>
[DataContract]
public class ActivityInvocationRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityInvocationRequest"/> class.
    /// </summary>
    [JsonConstructor]
    public ActivityInvocationRequest(
        string activityName,
        int taskId,
        string? serializedInput,
        string instanceId,
        string executionId)
    {
        this.ActivityName = activityName;
        this.TaskId = taskId;
        this.SerializedInput = serializedInput;
        this.InstanceId = instanceId;
        this.ExecutionId = executionId;
    }

    /// <summary>
    /// The name of the activity to invoke.
    /// </summary>
    [DataMember]
    public string ActivityName { get; private set; }

    /// <summary>
    /// The ID of the activity task.
    /// </summary>
    [DataMember]
    public int TaskId { get; private set; }

    /// <summary>
    /// The serialized input of the activity.
    /// </summary>
    [DataMember]
    public string? SerializedInput { get; private set; }

    /// <summary>
    /// Gets the orchestration instance ID associated with this activity.
    /// </summary>
    [DataMember]
    public string? InstanceId { get; private set; }

    /// <summary>
    /// Gets the orchestration instance execution ID associated with this activity.
    /// </summary>
    [DataMember]
    public string? ExecutionId { get; private set; }

    // An activity can be identified by it's name followed by it's task ID. Example: SayHello#0, SayHello#1, etc.
    internal string Identifier => $"{this.ActivityName}#{this.TaskId}";

    /// <summary>
    /// Returns an activity identifier string in the form of {activityName}#{taskID}.
    /// </summary>
    public override string ToString() => this.Identifier;
}