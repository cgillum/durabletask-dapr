// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DurableTask;

namespace Dapr.Workflow;

public class WorkflowContext
{
    readonly TaskOrchestrationContext innerContext;

    internal WorkflowContext(TaskOrchestrationContext innerContext)
    {
        this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
    }

    public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

    public void SetCustomStatus(object? customStatus) => this.innerContext.SetCustomStatus(customStatus);

    public Task WaitForExternalEventAsync(string eventName, TimeSpan timeout)
    {
        return this.innerContext.WaitForExternalEvent<object>(eventName, timeout);
    }
}