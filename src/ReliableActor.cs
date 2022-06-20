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
    protected ReliableActor(ActorHost host, ILoggerFactory loggerFactory)
        : base(host)
    {
        this.Log = loggerFactory.CreateLoggerForDaprProvider();
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
            // TODO: Uncomment when this is fixed: https://github.com/dapr/dapr/issues/4801
            //// await this.UnregisterReminderAsync(reminderName);
        }
    }

    protected Task CreateReliableReminder(string reminderName, byte[]? state = null, TimeSpan? delay = null)
    {
        // 5 minutes represents the amount of time to wait to redeliver a reminder the process crashes
        // before the operation is able to succeed.
        // TODO: Make this configurable
        ////TimeSpan defaultRecurrence = TimeSpan.FromMinutes(5);

        // TODO: Workaround for https://github.com/dapr/dapr/issues/4801
        TimeSpan defaultRecurrence = TimeSpan.FromMilliseconds(-1);

        // The default behavior is to execute immediately
        delay ??= TimeSpan.Zero;

        this.Log.CreatingReminder(this.Id, reminderName, delay.Value, defaultRecurrence);
        return this.RegisterReminderAsync(reminderName, state, delay.Value, defaultRecurrence);
    }

    protected abstract Task OnReminderReceivedAsync(string reminderName, byte[] state);
}
