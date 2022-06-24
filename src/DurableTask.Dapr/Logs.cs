// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors;
using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

/// <summary>
/// Extension methods for <see cref="ILogger"/> that write Dapr Workflow-specific logs.
/// </summary>
static partial class Logs
{
    // NOTE: All partial methods defined in this class have source-generated implementations.

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "{actorId}: Creating reminder '{reminderName}' with due time {dueTime} and recurrence {recurrence}.")]
    public static partial void CreatingReminder(
        this ILogger logger,
        ActorId actorId,
        string reminderName,
        TimeSpan dueTime,
        TimeSpan recurrence);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "{actorId}: Reminder '{reminderName}' fired.")]
    public static partial void ReminderFired(
        this ILogger logger,
        ActorId actorId,
        string reminderName);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{actorId}: Fetching workflow state.")]
    public static partial void FetchingWorkflowState(this ILogger logger, ActorId actorId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "{actorId}: No workflow state was found to handle reminder '{reminderName}'.")]
    public static partial void WorkflowStateNotFound(
        this ILogger logger,
        ActorId actorId,
        string reminderName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "{instanceId}: No durable timer entry was found that matched reminder '{reminderName}'.")]
    public static partial void TimerNotFound(
        this ILogger logger,
        string instanceId,
        string reminderName);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "{instanceId}: A reminder '{reminderName}' was triggered for this workflow but the workflow inbox was empty.")]
    public static partial void InboxIsEmpty(
        this ILogger logger,
        string instanceId,
        string reminderName);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "{instanceId}: Scheduling {count} activity tasks: {taskList}")]
    public static partial void SchedulingActivityTasks(
        this ILogger logger,
        string instanceId,
        int count,
        string taskList);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "{actorId}: Received invalid activity batch request.")]
    public static partial void InvalidActivityBatchRequest(this ILogger logger, string actorId);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "{actorId}: Received activity batch request #{sequenceNumber} for '{instanceId}' with {count} activity tasks: {taskList}")]
    public static partial void ReceivedActivityBatchRequest(
        this ILogger logger,
        string actorId,
        string instanceId,
        int sequenceNumber,
        int count,
        string taskList);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message = "{actorId}: {details}.")]
    public static partial void ActivityActorWarning(
        this ILogger logger,
        ActorId actorId,
        string details);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Information,
        Message = "{actorId}: Deleting reminder '{reminderName}'.")]
    public static partial void DeletingReminder(
        this ILogger logger,
        ActorId actorId,
        string reminderName);
}
