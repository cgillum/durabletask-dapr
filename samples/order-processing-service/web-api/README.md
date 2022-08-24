## Prerequisites

* [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
* [Docker Desktop](https://www.docker.com/products/docker-desktop)

## Building and running

Use the following commands to build the project.

```bash
dotnet build
```

You can run the project with the following command:

```bash
dotnet run
```

## Durable Task Sidecar with Dapr Workflow support

This starts the Durable Task sidecar which is preconfigured with the latest DurableTask.Dapr backend.
This process is where the workflow actor code lives, and interacts directly with the Dapr sidecar and assumes
that the gRPC endpoint is on port 50001.
The code in the web-api project will connect to this sidecar on port 4001.

```bash
docker run --name durabletask-sidecar-dapr -p 4001:4001 -d cgillum/durabletask-sidecar:0.3.3-dapr --backend Dapr
```

## Dapr CLI

This starts the Dapr sidecar in standalone mode. It does *not* attempt to start the web API / workflow app.

```bash
dapr run --app-id web-api --app-port 5000 --dapr-http-port 3500 --dapr-grpc-port 50001 --components-path ../components
```

If you want to run the Dapr sidecar together with the app, run the following command instead:

```bash
dapr run --app-id web-api --app-port 5000 --dapr-http-port 3500 --dapr-grpc-port 50001 --components-path ../components -- dotnet run
```

## Examples

See the [demo.http](../demo.http) file for several examples of starting workflow by invoking the HTTP APIs.
It works best if used with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) VS Code extension.

For example, a purchase order workflow can be started with the following HTTP request:

```http
POST http://localhost:8080/orders
Content-Type: application/json

{ "name": "catfood", "quantity": 3, "totalCost": 19.99 }
```

The response will contain a `Location` header that looks something like `http://localhost:8080/orders/XYZ`, where `XYZ` is a randomly generated order ID.
Follow this URL to get the workflow status as a JSON response.

If the workflow requires approval, you can do so by sending a POST request to `http://localhost:8080/orders/XYZ/approve` where `XYZ` is the order ID.
