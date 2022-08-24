// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add workflows. NOTE: This could alternatively be placed in another project.
builder.Services.AddWorkflow(options =>
{
    // Inline order processing workflow logic
    options.RegisterWorkflow<OrderPayload, OrderResult>("ProcessOrder", implementation: async (context, input) =>
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        // Notify order received
        context.PublishEvent(
            pubSubName: "notifications-pubsub",
            topic: "notifications",
            payload: $"Received order {context.InstanceId} for {input.Name} at {input.TotalCost:c}");

        // Invoke the inventory service to reserve the specified items
        InventoryResult? result = await context.InvokeMethodAsync<InventoryResult>(
            httpMethod: HttpMethod.Post,
            appId: "inventory",
            methodName: "reserve-inventory",
            data: new { item = input.Name, quantity = input.Quantity });

        if (result?.success != true)
        {
            context.SetCustomStatus($"Insufficient inventory for {input.Name}");
            return new OrderResult(Processed: false);
        }

        // Orders >= $1,000 require an approval to be received within 24 hours
        if (input.TotalCost >= 1000.00)
        {
            // Notify waiting for approval
            TimeSpan approvalTimeout = TimeSpan.FromHours(24);
            string message = $"Waiting for approval. Deadline = {context.CurrentUtcDateTime.Add(approvalTimeout):s}";
            context.SetCustomStatus(message);
            context.PublishEvent(pubSubName: "notifications-pubsub", topic: "notifications", payload: message);

            try
            {
                // Wait up to 24-hours for an "Approval" event to be delivered to this workflow instance
                await context.WaitForExternalEventAsync("Approval", approvalTimeout);
            }
            catch (TaskCanceledException)
            {
                // Notify approval deadline expired
                context.PublishEvent(
                    pubSubName: "notifications-pubsub",
                    topic: "notifications",
                    payload: $"Approval deadline for order {context.InstanceId} expired!");

                return new OrderResult(Processed: false);
            }

            // Notify approval received
            message = $"Received approval at {context.CurrentUtcDateTime:s}";
            context.SetCustomStatus(message);
            context.PublishEvent(pubSubName: "notifications-pubsub", topic: "notifications", payload: message);
        }

        // Invoke the payment service
        await context.InvokeMethodAsync(
            httpMethod: HttpMethod.Post,
            appId: "payments",
            methodName: "process-payment",
            data: new { amount = input.TotalCost, currency = "USD" });

        // Notify order processed successfully
        context.PublishEvent(
            pubSubName: "notifications-pubsub",
            topic: "notifications",
            payload: $"Order {context.InstanceId} was processed successfully!");

        return new OrderResult(Processed: true);
    });
});

// Configure JSON options.
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

WebApplication app = builder.Build();

// POST creates new orders
app.MapPost("/orders", async (WorkflowClient client, LinkGenerator linker, [FromBody] OrderPayload? orderInput) =>
{
    if (orderInput == null || orderInput.Name == null)
    {
        return Results.BadRequest(new
        {
            message = "Order data was missing from the request",
            example = new OrderPayload("Paperclips", 99.95),
        });
    }

    // TODO: Add some content
    string orderId = Guid.NewGuid().ToString()[..8];
    await client.ScheduleNewWorkflowAsync("ProcessOrder", orderId, orderInput);
    return Results.AcceptedAtRoute("GetOrder", new { orderId }, new
    {
        id = orderId,
        orderInput,
    });
}).WithName("CreateOrder");

// GET returns the status of existing orders
app.MapGet("/orders/{orderId}", async (string orderId, WorkflowClient client) =>
{
    WorkflowMetadata metadata = await client.GetWorkflowMetadata(orderId, getInputsAndOutputs: true);
    if (metadata.Exists)
    {
        return Results.Ok(metadata);
    }
    else
    {
        return Results.NotFound($"No order with ID = '{orderId}' was found.");
    }
}).WithName("GetOrder");

// POST to submit a manual approval to the order workflow
app.MapPost("/orders/{orderId}/approve", async (string orderId, WorkflowClient client) =>
{
    WorkflowMetadata metadata = await client.GetWorkflowMetadata(orderId, getInputsAndOutputs: true);
    if (!metadata.Exists)
    {
        return Results.NotFound($"No order with ID = '{orderId}' was found.");
    }
    else if (!metadata.IsWorkflowRunning)
    {
        return Results.BadRequest($"This order has already completed processing.");
    }

    // Raise the approval event to the running workflow instance
    await client.RaiseEventAsync(orderId, "Approval");

    return Results.Accepted();
}).WithName("ApproveOrder");

// Start the web server
app.Run("http://0.0.0.0:8080");

record OrderPayload(string Name, double TotalCost, int Quantity = 1);
record OrderResult(bool Processed);
record InventoryResult(bool success);
