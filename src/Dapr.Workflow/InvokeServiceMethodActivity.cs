// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Dapr.Client;
using Microsoft.DurableTask;
using Newtonsoft.Json;

namespace Dapr.Workflow;

[DurableTask("DaprInvoke")]
class InvokeServiceMethodActivity : TaskActivityBase<InvokeArgs, JsonElement>
{
    readonly DaprClient daprClient;

    public InvokeServiceMethodActivity(DaprClient daprClient)
    {
        this.daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
    }

    protected override async Task<JsonElement> OnRunAsync(TaskActivityContext context, InvokeArgs? input)
    {
        ArgumentNullException.ThrowIfNull(input, nameof(input));

        HttpRequestMessage httpRequest = this.daprClient.CreateInvokeMethodRequest(
            input.HttpMethod,
            input.AppId,
            input.MethodName,
            input.Data);

        try
        {
            // TODO: Use HttpClient instead of DaprClient for service invocation.
            // See discussion in https://github.com/dapr/dotnet-sdk/issues/907
            JsonElement result = await this.daprClient.InvokeMethodAsync<JsonElement>(httpRequest);
            return result;
        }
        catch (InvocationException e) when (e.InnerException is JsonReaderException)
        {
            // TODO: Need a more well-defined mechanism for handling deserialization issues.
            return new JsonElement();
        }
    }
}

public record InvokeArgs(HttpMethod HttpMethod, string AppId, string MethodName, object Data);
