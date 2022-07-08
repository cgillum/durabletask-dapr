// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Dapr.Workflow;
using Microsoft.AspNetCore.Mvc;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

// The workflow host is a background service that connects to the sidecar over gRPC
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddWorkflow(options =>
{
    options.RegisterWorkflow<OrderPayload, OrderResult>("ProcessOrder", async (context, input) =>
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        // TODO: Check inventory

        if (input.TotalCost > 1000.00)
        {
            context.SetCustomStatus($"Waiting for approval. Deadline = {context.CurrentUtcDateTime.AddHours(24):s}");

            try
            {
                await context.WaitForExternalEventAsync("Approval", TimeSpan.FromHours(24));
            }
            catch (TaskCanceledException)
            {
                // TODO: Publish a notification of cancellation
                return new OrderResult(Processed: false);
            }

            context.SetCustomStatus($"Received approval at {context.CurrentUtcDateTime:s}");
        }

        // TODO: Invoke the payment service

        // TODO: Publish a notification of success

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
    else if (metadata.Details!.RuntimeStatus != Microsoft.DurableTask.OrchestrationRuntimeStatus.Running)
    {
        return Results.BadRequest($"This order has already completed processing.");
    }

    await client.RaiseEventAsync(orderId, "Approval");
    return Results.Accepted();
}).WithName("ApproveOrder");

// Start the web server
app.Run("http://0.0.0.0:8080");

record OrderPayload(string Name, double TotalCost);
record OrderResult(bool Processed);
