// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

/// <summary>
/// Extension methods for <see cref="ILogger"/> that write Dapr Workflow-specific logs.
/// </summary>
static partial class Logs
{
    // NOTE: All partial methods defined in this class have source-generated implementations.

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{actorId}: Creating reminder '{reminderName}' with due time {dueTime} and recurrence {recurrence}.")]
    public static partial void CreatingReminder(
        this ILogger logger,
        string actorId,
        string reminderName,
        TimeSpan dueTime,
        TimeSpan recurrence);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{actorId}: Reminder '{reminderName}' fired.")]
    public static partial void ReminderFired(
        this ILogger logger,
        string actorId,
        string reminderName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{actorId}: Fetching workflow state.")]
    public static partial void FetchingWorkflowState(this ILogger logger, string actorId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{actorId}: No workflow state was found to handle reminder '{reminderName}'.")]
    public static partial void WorkflowStateNotFound(
        this ILogger logger,
        string actorId,
        string reminderName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{instanceId}: No durable timer entry was found that matched reminder '{reminderName}'.")]
    public static partial void TimerNotFound(
        this ILogger logger,
        string instanceId,
        string reminderName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{instanceId}: A reminder '{reminderName}' was triggered for this workflow but the workflow inbox was empty.")]
    public static partial void InboxIsEmpty(
        this ILogger logger,
        string instanceId,
        string reminderName);
}
