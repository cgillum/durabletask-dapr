// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.DurableTask;

namespace Dapr.Workflow;

public class WorkflowMetadata
{
    internal WorkflowMetadata(OrchestrationMetadata? metadata)
    {
        this.Details = metadata;
    }

    public bool Exists => this.Details != null;

    public bool IsWorkflowRunning => this.Details?.RuntimeStatus == OrchestrationRuntimeStatus.Running; 

    public OrchestrationMetadata? Details { get; }
}
