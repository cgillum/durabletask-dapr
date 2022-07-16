// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.DurableTask;

namespace Dapr.Workflow;

public class WorkflowContext
{
    readonly TaskOrchestrationContext innerContext;

    internal WorkflowContext(TaskOrchestrationContext innerContext)
    {
        this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
    }

    // TODO: Expose a "WorkflowId" type, similar to "ActorId"
    public string InstanceId => this.innerContext.InstanceId;

    public DateTime CurrentUtcDateTime => this.innerContext.CurrentUtcDateTime;

    public void SetCustomStatus(object? customStatus) => this.innerContext.SetCustomStatus(customStatus);

    // TODO: Should we keep the CreateTimer name to be consistent with the underlying SDK or nah?
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return this.innerContext.CreateTimer(delay, cancellationToken);
    }

    public Task WaitForExternalEventAsync(string eventName, TimeSpan timeout)
    {
        return this.innerContext.WaitForExternalEvent<object>(eventName, timeout);
    }

    public void PublishEvent(string pubSubName, string topic, object payload)
    {
        string pubSubId = $"dapr.pubsub://{pubSubName}";
        this.innerContext.SendEvent(pubSubId, topic, payload);
    }

    public Task<JsonElement> InvokeMethodAsync(HttpMethod httpMethod, string appId, string methodName, object data)
    {
        return this.innerContext.CallDaprInvokeAsync(new InvokeArgs(httpMethod, appId, methodName, data));
    }

    public async Task<TResult?> InvokeMethodAsync<TResult>(
        HttpMethod httpMethod,
        string appId,
        string methodName,
        object data)
    {
        InvokeArgs args = new(httpMethod, appId, methodName, data);
        JsonElement json = await this.innerContext.CallDaprInvokeAsync(args);
        return JsonSerializer.Deserialize<TResult>(json);
    }
}