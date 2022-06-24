// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Dapr.Actors.Runtime;
using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

/// <summary>
/// Base class for actors used to implement reliable workflow execution.
/// </summary>
/// <remarks>
/// All logic that needs to execute reliably is backed by a reminder. If the logic executes successfully, even if the
/// result is an exception, that reminder will be deleted. The reminder is intended to ressurect the execution in the
/// event of an unexpected process failure.
/// </remarks>
abstract class ReliableActor : Actor, IRemindable
{
    readonly DaprOptions options;

    protected ReliableActor(ActorHost host, DaprOptions options)
        : base(host)
    {
        this.Log = options.LoggerFactory.CreateLoggerForDaprProvider();
        this.options = options;
    }

    protected ILogger Log { get; }

    async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        this.Log.ReminderFired(this.Id, reminderName);
        try
        {
            await this.OnReminderReceivedAsync(reminderName, state);
        }
        finally
        {
            if (period > TimeSpan.Zero)
            {
                this.Log.DeletingReminder(this.Id, reminderName);

                // NOTE: This doesn't actually delete the reminder: https://github.com/dapr/dapr/issues/4801
                await this.UnregisterReminderAsync(reminderName);
            }
        }
    }

    protected Task CreateReliableReminder(string reminderName, byte[]? state = null, TimeSpan? delay = null)
    {
        TimeSpan recurrence = this.options.ReliableReminderInterval;
        if (recurrence <= TimeSpan.Zero)
        {
            // Disable recurrence
            recurrence = TimeSpan.FromMilliseconds(-1);
        }

        // The default behavior is to execute immediately
        delay ??= TimeSpan.Zero;

        this.Log.CreatingReminder(this.Id, reminderName, delay.Value, recurrence);
        return this.RegisterReminderAsync(reminderName, state, delay.Value, recurrence);
    }

    protected abstract Task OnReminderReceivedAsync(string reminderName, byte[] state);
}
