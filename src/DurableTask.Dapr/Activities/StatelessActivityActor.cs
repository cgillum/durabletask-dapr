// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using DurableTask.Core;
using DurableTask.Dapr.Workflows;

namespace DurableTask.Dapr.Activities;

/// <summary>
/// A stateless, reliable actor that executes activity logic.
/// </summary>
class StatelessActivityActor : ReliableActor, IActivityActor
{
    readonly IActivityExecutor activityInvoker;

    public StatelessActivityActor(ActorHost host, DaprOptions options, IActivityExecutor activityInvoker)
        : base(host, options)
    {
        this.activityInvoker = activityInvoker ?? throw new ArgumentNullException(nameof(activityInvoker));
    }

    Task IActivityActor.InvokeAsync(ActivityInvocationRequest request)
    {
        // Persist the request in a reminder to 1) unblock the calling orchestrator and 2) ensure reliable execution.
        byte[] reminderState = JsonSerializer.SerializeToUtf8Bytes(request);
        return this.CreateReliableReminder(reminderName: "execute", reminderState);
    }

    protected override async Task OnReminderReceivedAsync(string reminderName, byte[] state)
    {
        ActivityInvocationRequest? request = null;
        try
        {
            request = JsonSerializer.Deserialize<ActivityInvocationRequest>(state);
        }
        catch (Exception e)
        {
            this.Log.ActivityActorWarning(
                this.Id,
                $"Failed to deserialize the activity invocation request from the reminder state: {e}");
            await this.UnregisterReminderAsync(reminderName);
            return;
        }

        if (request == null)
        {
            this.Log.ActivityActorWarning(
                this.Id,
                $"Failed to deserialize the activity invocation request from the reminder state.");
            await this.UnregisterReminderAsync(reminderName);
            return;
        }

        if (request.InstanceId == null)
        {
            this.Log.ActivityActorWarning(this.Id, $"Couldn't find the orchestration instance ID state.");
            await this.UnregisterReminderAsync(reminderName);
            return;
        }

        ActivityCompletionResponse response;
        try
        {
            response = await this.activityInvoker.ExecuteActivityAsync(request);
        }
        catch (Exception e)
        {
            response = new ActivityCompletionResponse()
            {
                TaskId = request.TaskId,
                FailureDetails = new FailureDetails(e),
            };
        }

        // Asynchronously call back into the workflow actor with the result of the activity execution.
        // REVIEW: Should this proxy be cached?
        IWorkflowActor proxy = ActorProxy.Create<IWorkflowActor>(
            new ActorId(request.InstanceId),
            nameof(WorkflowActor),
            new ActorProxyOptions());
        await proxy.CompleteActivityAsync(response);
    }
}
