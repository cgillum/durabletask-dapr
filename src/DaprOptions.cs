// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

public class DaprOptions
{
    /// <summary>
    /// The <see cref="ILoggerFactory"/> to use for both the Dapr orchestration service and the ASP.NET Core host.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }
}
