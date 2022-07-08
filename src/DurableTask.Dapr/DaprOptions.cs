// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DurableTask.Dapr;

public class DaprOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DaprOptions"/> class.
    /// </summary>
    public DaprOptions(ILoggerFactory? loggerFactory = null)
    {
        this.LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    /// <summary>
    /// The <see cref="ILoggerFactory"/> to use for both the Dapr orchestration service and the ASP.NET Core host.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; init; }

    /// <summary>
    /// The interval time for reminders that are created specifically for reliability.
    /// </summary>
    /// <remarks>
    /// A value of <see cref="TimeSpan.Zero"/> or less will disable reminder intervals.
    /// </remarks>
    public TimeSpan ReliableReminderInterval { get; set; }
}
