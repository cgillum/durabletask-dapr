// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DurableTask;
using Microsoft.DurableTask.Grpc;

namespace Dapr.Workflow;

public sealed class WorkflowClient : IAsyncDisposable
{
    readonly DurableTaskClient innerClient;

    internal WorkflowClient(IServiceProvider? services = null)
    {
        DurableTaskGrpcClient.Builder builder = new();
        if (services != null)
        {
            builder.UseServices(services);
        }

        this.innerClient = builder.Build();
    }

    public Task<string> ScheduleNewWorkflowAsync(
        string name,
        string? instanceId = null,
        object? input = null,
        DateTime? startTime = null)
    {
        return this.innerClient.ScheduleNewOrchestrationInstanceAsync(name, instanceId, input, startTime);
    }

    public async Task<WorkflowMetadata> GetWorkflowMetadata(string instanceId, bool getInputsAndOutputs = false)
    {
        OrchestrationMetadata? metadata = await this.innerClient.GetInstanceMetadataAsync(
            instanceId,
            getInputsAndOutputs);
        return new WorkflowMetadata(metadata);
    }

    public Task RaiseEventAsync(string instanceId, string eventName, object? eventData = null)
    {
        return this.innerClient.RaiseEventAsync(instanceId, eventName, eventData);
    }

    public Task TerminateWorkflowAsync(string instanceId, string reason)
    {
        return this.innerClient.TerminateAsync(instanceId, reason);
    }

    public ValueTask DisposeAsync()
    {
        return ((IAsyncDisposable)this.innerClient).DisposeAsync();
    }
}
