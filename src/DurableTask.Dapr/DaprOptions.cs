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
    /// The interval time for reminders that are created specifically for reliability. The default value is 5 minutes.
    /// </summary>
    /// <remarks>
    /// A value of <see cref="TimeSpan.Zero"/> or less will disable reminder intervals.
    /// </remarks>
    public TimeSpan ReliableReminderInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The gRPC endpoint to use when communicating with the Dapr sidecar over gRPC.
    /// </summary>
    /// <remarks>
    /// This value is typically in the form <c>"http://127.0.0.1:%DAPR_GRPC_PORT%"</c>.
    /// </remarks>
    public string? GrpcEndpoint { get; set; }
}
