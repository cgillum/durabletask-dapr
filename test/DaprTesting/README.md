# Manually testing the Dapr Workflow engine POC

This README walks through the process of manually testing the Dapr Workflow engine POC (proof-of-concept). This guide was originally written for Dapr 1.7.

## Running the test

The `/test/DaprTesting` directory includes a console app that runs a Durable Task Framework orchestration using the Dapr backend. It's designed to be opened in Visual Studio and started with "F5".

However, before the test can run, you need to start the Dapr sidecar. The best way to set this up is using the Dapr CLI (see the [Dapr getting started guide](https://docs.dapr.io/getting-started/)). Below is the command-line you can use to start the sidecar.

```bash
dapr run --app-id myapp2 --dapr-http-port 3500 --app-port 5000
```

The output should look something like the following:

```
Starting up...
2022-03-07T10:03:44.520814Z info: DurableTask.Core[11] Durable task hub worker is starting
2022-03-07T10:03:44.631434Z info: Microsoft.Hosting.Lifetime[14] Now listening on: http://localhost:5000
2022-03-07T10:03:44.631777Z info: Microsoft.Hosting.Lifetime[14] Now listening on: https://localhost:5001
2022-03-07T10:03:44.632347Z info: Microsoft.Hosting.Lifetime[0] Application started. Press Ctrl+C to shut down.
2022-03-07T10:03:44.632681Z info: Microsoft.Hosting.Lifetime[0] Hosting environment: Production
2022-03-07T10:03:44.632761Z info: Microsoft.Hosting.Lifetime[0] Content root path: C:\GitHub\durabletask-dapr\test\DaprTesting\bin\Debug\net6.0\
2022-03-07T10:03:44.664269Z dbug: DurableTask.Core[20] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Starting dispatch loop
2022-03-07T10:03:44.664757Z dbug: DurableTask.Core[20] TaskActivityDispatcher-ae22779db9df45e28dbe299314327462-0: Starting dispatch loop
2022-03-07T10:03:44.665066Z info: DurableTask.Core[11] Durable task hub worker started successfully after 127ms
Press [ENTER] to create an orchestration instance.
2022-03-07T10:03:44.672905Z dbug: DurableTask.Core[23] TaskActivityDispatcher-ae22779db9df45e28dbe299314327462-0: Fetching next work item. Current active work-item count: 0. Maximum active work-item count: 20. Timeout: 30s
2022-03-07T10:03:44.672905Z dbug: DurableTask.Core[23] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Fetching next work item. Current active work-item count: 0. Maximum active work-item count: 20. Timeout: 30s
```

Press the [Enter] key to start a workflow. The debug output should look something like the following:

```
2022-04-07T10:04:14.771275Z info: DurableTask.Core[40] Scheduling orchestration 'DaprTesting.EchoOrchestration' with instance ID = '9794492e587b424f841cbf528b936d1a' and 14 bytes of input
2022-04-07T10:04:15.013327Z dbug: DurableTask.Dapr.DaprWorkflowScheduler[0] Activated
Started orchestration with ID = '9794492e587b424f841cbf528b936d1a' and waiting for it to complete...
2022-04-07T10:04:15.124884Z info: DurableTask.Core[43] Waiting up to 300 seconds for instance '9794492e587b424f841cbf528b936d1a' to complete, fail, or be terminated
2022-04-07T10:04:15.133354Z dbug: DurableTask.Core[24] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Fetched next work item '9794492e587b424f841cbf528b936d1a' after 30459ms. Current active work-item count: 0. Maximum active work-item count: 20
2022-04-07T10:04:15.133858Z dbug: DurableTask.Core[23] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Fetching next work item. Current active work-item count: 1. Maximum active work-item count: 20. Timeout: 30s
2022-04-07T10:04:15.135680Z dbug: DurableTask.Core[27] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Processing work-item '9794492e587b424f841cbf528b936d1a'
2022-04-07T10:04:15.148597Z dbug: DurableTask.Core[50] 9794492e587b424f841cbf528b936d1a: Preparing to process a [ExecutionStarted] message
2022-04-07T10:04:15.148990Z info: DurableTask.Core[51] 9794492e587b424f841cbf528b936d1a: Executing 'DaprTesting.EchoOrchestration' orchestration logic
2022-04-07T10:04:15.203410Z info: DurableTask.Core[52] 9794492e587b424f841cbf528b936d1a: Orchestration 'DaprTesting.EchoOrchestration' awaited and scheduled 1 durable operation(s).
2022-04-07T10:04:15.204665Z info: DurableTask.Core[49] 9794492e587b424f841cbf528b936d1a: Orchestration completed with a 'Completed' status and 14 bytes of output. Details:
2022-04-07T10:04:15.207384Z dbug: DurableTask.Core[28] TaskOrchestrationDispatcher-3841f49fafff4503a6f9e9308d2f4671-0: Finished processing work-item '9794492e587b424f841cbf528b936d1a'
Orchestration Completed! Raw output: "Hello, Dapr!"
```

## Viewing the workflow state in Redis

Assuming you're using Redis as the actor state store (the default configuration), you can use Redis commands to query the state. The simplest way to do this is to open a bash shell in the redis Docker container and issue commands from an interactive redis CLI. More details on interacting with Redis state stores can be found [here](https://docs.dapr.io/developing-applications/building-blocks/state-management/query-state-store/query-redis-store/).

Creating a Redis CLI terminal session can be done with the following command. Note that `dapr_redis` is the default name of the Redis container. You can change this value if your redis container has a different name.

```bash
docker run --rm -it --link dapr_redis redis redis-cli -h dapr_redis
```

Querying for all workflow instances:

```bash
KEYS myapp*
```

You can use the `KEYS myapp*` command to list all workflow instances. The following shows the output when two workflows have been created:

```
dapr_redis:6379> KEYS myapp*
1) "myapp2||DaprWorkflowScheduler||7f3c77692fb9439ea86d2c5cadf5db1e||state"
2) "myapp2||DaprWorkflowScheduler||9794492e587b424f841cbf528b936d1a||state"
3) "myapp2||DaprWorkflowScheduler||ad5f893fc3864b02bac0b16d771be368||state"
4) "myapp2||DaprWorkflowScheduler||2752d169aeab4381aa491355b87e9a40||state"
```

These keys correspond to Redis hash values. You can query the values using the `HGET {key} data` command where `{key}` is one of the keys listed in the previous `KEYS` command.

```
dapr_redis:6379> HGET "myapp2||DaprWorkflowScheduler||7f3c77692fb9439ea86d2c5cadf5db1e||state" data
"{\"orchestrationStatus\":1,\"name\":\"DaprTesting.EchoOrchestration\",\"instanceId\":\"7f3c77692fb9439ea86d2c5cadf5db1e\",\"createdTimeUtc\":\"2022-06-07T18:33:21.9898372Z\",\"inbox\":[{\"extensionData\":{},\"event\":{\"extensionData\":{},\"eventId\":-1,\"isPlayed\":true,\"timestamp\":\"2022-06-07T18:33:18.8953566Z\",\"eventType\":0},\"sequenceNumber\":0,\"orchestrationInstance\":{\"instanceId\":\"7f3c77692fb9439ea86d2c5cadf5db1e\",\"executionId\":\"4d2f01826dc94ae8a931ba8eba7e5206\",\"extensionData\":{}}}],\"history\":[],\"executionId\":\"4d2f01826dc94ae8a931ba8eba7e5206\",\"input\":\"\\\"Hello, Dapr!\\\"\",\"output\":\"\\\"Hello, Dapr!\\\"\",\"customStatus\":null,\"lastUpdatedTimeUtc\":\"2022-06-07T18:33:31.4642535Z\",\"completedTimeUtc\":null}"
```