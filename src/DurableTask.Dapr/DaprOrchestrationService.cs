// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Threading.Channels;
using Dapr.Actors;
using Dapr.Actors.Client;
using DurableTask.Core;
using DurableTask.Core.History;
using DurableTask.Dapr.Activities;
using DurableTask.Dapr.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DurableTask.Dapr;

/// <summary>
/// The primary integration point for the Durable Task Framework and Dapr actors.
/// </summary>
/// <remarks>
/// <para>
/// Durable Task Framework apps can use Dapr actors as the underlying storage provider and scheduler by creating
/// instances of this class and passing them in as the constructor arguments for the <see cref="TaskHubClient"/> and
/// <see cref="TaskHubWorker"/> objects. The client and worker will then call into class via the 
/// <see cref="IOrchestrationServiceClient"/> and <see cref="IOrchestrationService"/> interfaces, respectively.
/// </para><para>
/// In this orchestration service, each created orchestration instance maps a Dapr actor instance (it may map to more
/// than one actor in a future iteration). The actor stores the orchestration history and metadata in its own internal
/// state. Operations invoked on the actor will either query the orchestration state or trigger the orchestration to
/// be executed in the current process.
/// </para>
/// </remarks>
public class DaprOrchestrationService : OrchestrationServiceBase, IWorkflowExecutor, IActivityExecutor
{
    /// <summary>
    /// Dapr-specific configuration options for this orchestration service.
    /// </summary>
    readonly DaprOptions options;

    /// <summary>
    /// Channel used to asynchronously invoke the orchestration when certain actor messages are received.
    /// </summary>
    readonly Channel<WorkflowExecutionWorkItem> orchestrationWorkItemChannel;

    /// <summary>
    /// Channel used to asynchronously invoke an activity when certain actor messages are received.
    /// </summary>
    readonly Channel<ActivityExecutionWorkItem> activityWorkItemChannel;

    /// <summary>
    /// The web host that routes HTTP requests to specific actor instances.
    /// </summary>
    readonly IHost daprActorHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprOrchestrationService"/> class with the specified configuration
    /// options.
    /// </summary>
    /// <param name="options">Configuration options for Dapr integration.</param>
    public DaprOrchestrationService(DaprOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));

        this.orchestrationWorkItemChannel = Channel.CreateUnbounded<WorkflowExecutionWorkItem>();
        this.activityWorkItemChannel = Channel.CreateUnbounded<ActivityExecutionWorkItem>();

        // The actor host is an HTTP service that routes incoming requests to actor instances.
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        if (options.LoggerFactory != null)
        {
            builder.Services.AddSingleton<ILoggerFactory>(options.LoggerFactory);
        }

        builder.Services.AddDaprClient(clientOptions =>
        {
            if (!string.IsNullOrEmpty(options.GrpcEndpoint))
            {
                clientOptions.UseGrpcEndpoint(options.GrpcEndpoint);
            }
        });

        builder.Services.AddActors(actorOptions =>
        {
            actorOptions.Actors.RegisterActor<WorkflowActor>();
            actorOptions.Actors.RegisterActor<StatelessActivityActor>();

            // The Durable Task Framework history events are not compatible with System.Text.Json so we need to create
            // a custom converter for these.
            actorOptions.JsonSerializerOptions.Converters.Add(new DurableTaskHistoryConverter());
            actorOptions.JsonSerializerOptions.Converters.Add(new DurableTaskTimerFiredConverter());
        });

        // Register the orchestration service as a dependency so that the actors can invoke methods on it.
        builder.Services.AddSingleton<IWorkflowExecutor>(this);
        builder.Services.AddSingleton<IActivityExecutor>(this);
        builder.Services.AddSingleton(options);
        
        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapActorsHandlers());
        this.daprActorHost = app;
    }

    #region Task Hub Management
    // Nothing to do, since we rely on the existing Dapr actor infrastructure to already be there.
    public override Task CreateAsync(bool recreateInstanceStore) => Task.CompletedTask;

    // Nothing to do, since we rely on the existing Dapr actor infrastructure to already be there.
    public override Task CreateIfNotExistsAsync() => Task.CompletedTask;

    // REVIEW: Would it make sense to so something here, like delete any created resources?
    public override Task DeleteAsync(bool deleteInstanceStore) => Task.CompletedTask;
    #endregion

    #region Client APIs (called by TaskHubClient)
    public override async Task CreateTaskOrchestrationAsync(
        TaskMessage creationMessage,
        OrchestrationStatus[] dedupeStatuses)
    {
        IWorkflowActor proxy = this.GetOrchestrationActorProxy(creationMessage.OrchestrationInstance.InstanceId);

        // Depending on where the actor gets placed, this may invoke an actor on another machine.
        await proxy.InitAsync(creationMessage, dedupeStatuses);
    }

    public override async Task SendTaskOrchestrationMessageAsync(TaskMessage message)
    {
        IWorkflowActor proxy = this.GetOrchestrationActorProxy(message.OrchestrationInstance.InstanceId);
        await proxy.PostToInboxAsync(message);
    }

    public override async Task<OrchestrationState> WaitForOrchestrationAsync(
        string instanceId,
        string executionId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();

        do
        {
            OrchestrationState? state = await this.GetOrchestrationStateAsync(instanceId, executionId);
            if (state != null && (
                state.OrchestrationStatus == OrchestrationStatus.Completed ||
                state.OrchestrationStatus == OrchestrationStatus.Failed ||
                state.OrchestrationStatus == OrchestrationStatus.Terminated))
            {
                return state;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        while (timeout == Timeout.InfiniteTimeSpan || sw.Elapsed < timeout);

        throw new TimeoutException();
    }

    public override async Task<OrchestrationState?> GetOrchestrationStateAsync(string instanceId, string? executionId)
    {
        IWorkflowActor proxy = this.GetOrchestrationActorProxy(instanceId);
        OrchestrationState? state = await proxy.GetCurrentStateAsync();
        return state;
    }

    public override Task PurgeOrchestrationHistoryAsync(
        DateTime thresholdDateTimeUtc,
        OrchestrationStateTimeRangeFilterType timeRangeFilterType)
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
            ExecutionResultType.Abandoned,
            null!,
            Array.Empty<TaskMessage>(),
            Array.Empty<TaskMessage>(),
            Array.Empty<TaskMessage>(),
            Array.Empty<HistoryEvent>()));

        return Task.CompletedTask;
    }

    public override Task CompleteTaskActivityWorkItemAsync(TaskActivityWorkItem workItem, TaskMessage responseMessage)
    {
        ActivityExecutionWorkItem activityExecutionWorkItem = (ActivityExecutionWorkItem)workItem;
        TaskScheduledEvent scheduledEvent = (TaskScheduledEvent)workItem.TaskMessage.Event;

        ActivityCompletionResponse response = new() { TaskId = scheduledEvent.EventId };
        if (responseMessage.Event is TaskCompletedEvent completedEvent)
        {
            response.SerializedResult = completedEvent.Result;
        }
        else
        {
            TaskFailedEvent failedEvent = (TaskFailedEvent)responseMessage.Event;
            response.FailureDetails = failedEvent.FailureDetails;
        }

        activityExecutionWorkItem.TaskCompletionSource.SetResult(response);
        return Task.CompletedTask;
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
        // TODO: continuedAsNewMessage (does this still apply?)
        workflowWorkItem.TaskCompletionSource.SetResult(new WorkflowExecutionResult(
            Type: ExecutionResultType.Executed,
            UpdatedState: orchestrationState,
            Timers: timerMessages,
            ActivityOutbox: outboundMessages,
            OrchestrationOutbox: orchestratorMessages,
            NewHistoryEvents: newOrchestrationRuntimeState.NewEvents));

        return Task.CompletedTask;
    }

    public override async Task<TaskActivityWorkItem?> LockNextTaskActivityWorkItem(
        TimeSpan receiveTimeout,
        CancellationToken cancellationToken)
    {
        return await this.activityWorkItemChannel.Reader.ReadAsync(cancellationToken);
    }

    public override async Task<TaskOrchestrationWorkItem?> LockNextTaskOrchestrationWorkItemAsync(
        TimeSpan receiveTimeout,
        CancellationToken cancellationToken)
    {
        return await this.orchestrationWorkItemChannel.Reader.ReadAsync(cancellationToken);
    }

    public override Task<TaskActivityWorkItem> RenewTaskActivityWorkItemLockAsync(TaskActivityWorkItem workItem)
    {
        // Not used
        return Task.FromResult(new TaskActivityWorkItem());
    }

    public override Task RenewTaskOrchestrationWorkItemLockAsync(TaskOrchestrationWorkItem workItem)
    {
        // Not used
        return Task.CompletedTask;
    }

    // Called by the TaskHubWorker
    public override async Task StartAsync()
    {
        await this.daprActorHost.StartAsync();

        // Dapr requires several seconds for actor discovery to happen
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    // Called by the TaskHubWorker
    public override Task StopAsync() => this.daprActorHost.StopAsync();

    #endregion

    #region Actor API calls

    // NOTE: This is just glue code to make the IOrchestrationService's polling abstraction invocable as a method
    Task<WorkflowExecutionResult> IWorkflowExecutor.ExecuteWorkflowStepAsync(
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
                ExecutionResultType.Throttled,
                null!,
                Array.Empty<TaskMessage>(),
                Array.Empty<TaskMessage>(),
                Array.Empty<TaskMessage>(),
                Array.Empty<HistoryEvent>()));
        }

        // The IOrchestrationService.CompleteTaskOrchestrationWorkItemAsync method
        // is expected to set the result for this task.
        return workItem.TaskCompletionSource.Task;
    }

    // NOTE: This is just glue code to make the IOrchestrationService's polling abstraction invocable as a method
    Task<ActivityCompletionResponse> IActivityExecutor.ExecuteActivityAsync(ActivityInvocationRequest request)
    {
        ActivityExecutionWorkItem workItem = new()
        {
            Id = $"{request.InstanceId}:{request.TaskId:X16}",
            LockedUntilUtc = DateTime.MaxValue,
            TaskMessage = new TaskMessage()
            {
                OrchestrationInstance = new OrchestrationInstance
                {
                    InstanceId = request.InstanceId,
                    ExecutionId = request.ExecutionId,
                },
                Event = new TaskScheduledEvent(request.TaskId)
                {
                    Name = request.ActivityName,
                    Input = request.SerializedInput,
                },
            },
        };

        // IOrchestrationService.LockNextTaskActivityWorkItemAsync is listening for new work-items on this channel.
        if (!this.activityWorkItemChannel.Writer.TryWrite(workItem))
        {
            // TODO
        }

        // The IOrchestrationService.CompleteTaskActivityWorkItemAsync method
        // is expected to set the result for this task.
        return workItem.TaskCompletionSource.Task;
    }

    #endregion

    IWorkflowActor GetOrchestrationActorProxy(string instanceId, TimeSpan? timeout = null)
    {
        // REVIEW: Should we be caching these proxy objects?
        return ActorProxy.Create<IWorkflowActor>(
            new ActorId(instanceId),
            nameof(WorkflowActor),
            new ActorProxyOptions { RequestTimeout = timeout });
    }

    class WorkflowExecutionWorkItem : TaskOrchestrationWorkItem
    {
        public TaskCompletionSource<WorkflowExecutionResult> TaskCompletionSource { get; } = new();
    }

    class ActivityExecutionWorkItem : TaskActivityWorkItem
    {
        public TaskCompletionSource<ActivityCompletionResponse> TaskCompletionSource { get; } = new();
    }
}
