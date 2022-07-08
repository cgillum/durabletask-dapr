// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DurableTask;

namespace Dapr.Workflow;

public sealed class WorkflowRuntimeOptions
{
    readonly Dictionary<string, Action<IDurableTaskRegistry>> factories = new();

    public void RegisterWorkflow<TInput, TOutput>(string name, Func<WorkflowContext, TInput?, Task<TOutput?>> implementation)
    {
        // Dapr workflows are implemented as specialized Durable Task orchestrations
        this.factories.Add(name, (IDurableTaskRegistry registry) =>
        {
            registry.AddOrchestrator<TInput, TOutput>(name, (innerContext, input) =>
            {
                WorkflowContext workflowContext = new(innerContext);
                return implementation(workflowContext, input);
            });
        });
    }

    // TODO: Add support for activities

    internal void AddWorkflowsToRegistry(IDurableTaskRegistry registry)
    {
        foreach (Action<IDurableTaskRegistry> factory in this.factories.Values)
        {
            factory.Invoke(registry);
        }
    }
}

