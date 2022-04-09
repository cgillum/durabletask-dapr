// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Channels;
using Dapr.Actors;
using Dapr.Actors.Client;
using DurableTask.Core;
using DurableTask.Core.History;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DurableTask.Dapr;

public class DaprOrchestrationService : OrchestrationServiceBase, IWorkflowScheduler
{
    readonly DaprOptions options;
    readonly Channel<TaskOrchestrationWorkItem> orchestrationWorkItemChannel;
    readonly IHost daprActorHost;

    public DaprOrchestrationService(DaprOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        this.orchestrationWorkItemChannel = Channel.CreateUnbounded<TaskOrchestrationWorkItem>();
        this.daprActorHost = this.CreateActorHost();
    }

    IHost CreateActorHost()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddActors(options =>
        {
            options.Actors.RegisterActor<DaprWorkflowScheduler>();
        });

        // Register the orchestration service as a dependency so that the actors can invoke methods on it.
        builder.Services.AddSingleton<IWorkflowScheduler>(this);

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapActorsHandlers());

        return app;
    }

    #region Task Hub Management
    // Nothing to do, since we rely on the existing Dapr actor infrastructure to already be there.
    public override Task CreateAsync(bool recreateInstanceStore) => Task.CompletedTask;

    // Nothing to do, since we rely on the existing Dapr actor infrastructure to already be there.
    public override Task CreateIfNotExistsAsync() => Task.CompletedTask;

    // REVIEW: Would it make sense to so something here, like delete any created resources?
    public override Task DeleteAsync(bool deleteInstanceStore) => Task.CompletedTask;
    #endregion

    #region Client APIs
    public override async Task CreateTaskOrchestrationAsync(
        TaskMessage creationMessage,
        OrchestrationStatus[] dedupeStatuses)
    {
        ActorId actorId = new(creationMessage.OrchestrationInstance.InstanceId);
        IOrchestrationActor proxy = ActorProxy.Create<IOrchestrationActor>(actorId, nameof(DaprWorkflowScheduler));

        // The Init method invokes the actor directly, which then decides whether to apply de-dupe logic.
        await proxy.InitAsync(creationMessage, dedupeStatuses);
    }

    public override Task SendTaskOrchestrationMessageAsync(TaskMessage message)
    {
        // TODO: Invoke a specific actor API?
        throw new NotImplementedException();
    }

    public override Task<OrchestrationState> WaitForOrchestrationAsync(
        string instanceId,
        string executionId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public override Task<OrchestrationState?> GetOrchestrationStateAsync(string instanceId, string? executionId)
    {
        throw new NotImplementedException();
    }

    public override Task ForceTerminateTaskOrchestrationAsync(string instanceId, string reason)
    {
        throw new NotImplementedException();
    }

    public override Task PurgeOrchestrationHistoryAsync(DateTime thresholdDateTimeUtc, OrchestrationStateTimeRangeFilterType timeRangeFilterType)
    {
        throw new NotImplementedException();
    }
    #endregion

    #region Worker APIs

    public override Task AbandonTaskActivityWorkItemAsync(TaskActivityWorkItem workItem)
    {
        throw new NotImplementedException();
    }

    public override Task AbandonTaskOrchestrationWorkItemAsync(TaskOrchestrationWorkItem workItem)
    {
        WorkflowExecutionWorkItem workflowWorkItem = (WorkflowExecutionWorkItem)workItem;

        // Call back into the actor via a TaskCompletionSource to reschedule the work-item
        workflowWorkItem.TaskCompletionSource.SetResult(new WorkflowExecutionResult(
            WorkflowExecutionResultType.Abandoned,
            null!,
            Array.Empty<TaskMessage>(),
            Array.Empty<TaskMessage>(),
            Array.Empty<HistoryEvent>()));

        return Task.CompletedTask;
    }

    public override Task CompleteTaskActivityWorkItemAsync(TaskActivityWorkItem workItem, TaskMessage responseMessage)
    {
        throw new NotImplementedException();
    }

    public override Task CompleteTaskOrchestrationWorkItemAsync(
        TaskOrchestrationWorkItem workItem,
        OrchestrationRuntimeState newOrchestrationRuntimeState,
        IList<TaskMessage> outboundMessages,
        IList<TaskMessage> orchestratorMessages,
        IList<TaskMessage> timerMessages,
        TaskMessage continuedAsNewMessage,
        OrchestrationState orchestrationState)
    {
        WorkflowExecutionWorkItem workflowWorkItem = (WorkflowExecutionWorkItem)workItem;

        // Call back into the actor via a TaskCompletionSource to save the state
        workflowWorkItem.TaskCompletionSource.SetResult(new WorkflowExecutionResult(
            WorkflowExecutionResultType.Executed,
            orchestrationState,
            orchestratorMessages.Union(timerMessages).Append(continuedAsNewMessage).ToList(),
            outboundMessages,
            newOrchestrationRuntimeState.NewEvents));

        return Task.CompletedTask;
    }

    public override Task<TaskActivityWorkItem?> LockNextTaskActivityWorkItem(TimeSpan receiveTimeout, CancellationToken cancellationToken)
    {
        // Not supported - just block indefinitely
        return Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => default(TaskActivityWorkItem));
    }

    public override async Task<TaskOrchestrationWorkItem?> LockNextTaskOrchestrationWorkItemAsync(
        TimeSpan receiveTimeout,
        CancellationToken cancellationToken)
    {
        return await this.orchestrationWorkItemChannel.Reader.ReadAsync(cancellationToken);
    }

    public override Task<TaskActivityWorkItem> RenewTaskActivityWorkItemLockAsync(TaskActivityWorkItem workItem)
    {
        throw new NotImplementedException();
    }

    public override Task RenewTaskOrchestrationWorkItemLockAsync(TaskOrchestrationWorkItem workItem)
    {
        throw new NotImplementedException();
    }

    public override Task StartAsync() => this.daprActorHost.StartAsync();

    public override Task StopAsync() => this.daprActorHost.StopAsync();

    #endregion

    #region Actor API calls

    Task<WorkflowExecutionResult> IWorkflowScheduler.ExecuteWorkflowAsync(
        string instanceId,
        IList<TaskMessage> inbox,
        IList<HistoryEvent> history)
    {
        WorkflowExecutionWorkItem workItem = new()
        {
            InstanceId = instanceId,
            NewMessages = inbox,
            OrchestrationRuntimeState = new OrchestrationRuntimeState(history),
        };

        // The IOrchestrationService.LockNextTaskOrchestrationWorkItemAsync method
        // is listening for new work-items on this channel.
        if (!this.orchestrationWorkItemChannel.Writer.TryWrite(workItem))
        {
            return Task.FromResult(new WorkflowExecutionResult(
                WorkflowExecutionResultType.Throttled,
                null!,
                Array.Empty<TaskMessage>(),
                Array.Empty<TaskMessage>(),
                Array.Empty<HistoryEvent>()));
        }

        // The IOrchestrationService.CompleteTaskOrchestrationWorkItemAsync method
        // is expected to set the result for this task.
        return workItem.TaskCompletionSource.Task;
    }

    #endregion

    class WorkflowExecutionWorkItem : TaskOrchestrationWorkItem
    {
        public TaskCompletionSource<WorkflowExecutionResult> TaskCompletionSource { get; } = new();
    }
}
