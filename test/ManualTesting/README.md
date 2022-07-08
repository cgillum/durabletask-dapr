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
2022-06-20T11:46:30.965669Z info: DurableTask.Core[11] Durable task hub worker is starting
2022-06-20T11:46:31.154072Z info: Microsoft.Hosting.Lifetime[14] Now listening on: http://localhost:5000
2022-06-20T11:46:31.155023Z info: Microsoft.Hosting.Lifetime[14] Now listening on: https://localhost:5001
2022-06-20T11:46:31.156860Z info: Microsoft.Hosting.Lifetime[0] Application started. Press Ctrl+C to shut down.
2022-06-20T11:46:31.157933Z info: Microsoft.Hosting.Lifetime[0] Hosting environment: Production
2022-06-20T11:46:31.157960Z info: Microsoft.Hosting.Lifetime[0] Content root path: C:\GitHub\durabletask-dapr\test\DaprTesting\bin\Debug\net6.0\
2022-06-20T11:46:31.202991Z info: DurableTask.Core[11] Durable task hub worker started successfully after 201ms
Press [ENTER] to create an orchestration instance.
```

Press the [Enter] key to start a workflow that executes three activities in sequence. The debug output should look something like the following:

```
2022-06-20T11:46:39.196304Z info: DurableTask.Core[40] Scheduling orchestration 'DaprTesting.HelloCitiesOrchestration' with instance ID = '03c538d0603f4f3e942995eaf00503f9' and 0 bytes of input
2022-06-20T11:46:39.603370Z info: DurableTask.Dapr[3] 03c538d0603f4f3e942995eaf00503f9: Fetching workflow state.
2022-06-20T11:46:39.763185Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9: Creating reminder 'init' with due time 00:00:00 and recurrence -00:00:00.0010000.
Started orchestration with ID = '03c538d0603f4f3e942995eaf00503f9' and waiting for it to complete...
2022-06-20T11:46:40.020441Z info: DurableTask.Core[43] Waiting up to 300 seconds for instance '03c538d0603f4f3e942995eaf00503f9' to complete, fail, or be terminated
2022-06-20T11:46:40.083354Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9: Reminder 'init' fired.
2022-06-20T11:46:40.112238Z info: DurableTask.Core[51] 03c538d0603f4f3e942995eaf00503f9: Executing 'DaprTesting.HelloCitiesOrchestration' orchestration logic
2022-06-20T11:46:40.262931Z info: DurableTask.Core[52] 03c538d0603f4f3e942995eaf00503f9: Orchestration 'DaprTesting.HelloCitiesOrchestration' awaited and scheduled 1 durable operation(s).
2022-06-20T11:46:40.264729Z info: DurableTask.Core[46] 03c538d0603f4f3e942995eaf00503f9: Scheduling activity [DaprTesting.SayHello#0] with 0 bytes of input
2022-06-20T11:46:40.272677Z info: DurableTask.Dapr[7] 03c538d0603f4f3e942995eaf00503f9: Scheduling 1 activity tasks: DaprTesting.SayHello#0
2022-06-20T11:46:40.361957Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9:activity:0: Creating reminder 'execute' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:40.436473Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9:activity:0: Reminder 'execute' fired.
2022-06-20T11:46:40.554816Z info: DurableTask.Core[60] 03c538d0603f4f3e942995eaf00503f9: Starting task activity [DaprTesting.SayHello#0]
2022-06-20T11:46:40.599043Z info: DurableTask.Core[61] 03c538d0603f4f3e942995eaf00503f9: Task activity [DaprTesting.SayHello#0] completed successfully
2022-06-20T11:46:40.636674Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9: Creating reminder 'task-completed' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:40.696998Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9: Reminder 'task-completed' fired.
2022-06-20T11:46:40.698480Z info: DurableTask.Core[51] 03c538d0603f4f3e942995eaf00503f9: Executing 'DaprTesting.HelloCitiesOrchestration' orchestration logic
2022-06-20T11:46:40.715871Z info: DurableTask.Core[52] 03c538d0603f4f3e942995eaf00503f9: Orchestration 'DaprTesting.HelloCitiesOrchestration' awaited and scheduled 1 durable operation(s).
2022-06-20T11:46:40.715944Z info: DurableTask.Core[46] 03c538d0603f4f3e942995eaf00503f9: Scheduling activity [DaprTesting.SayHello#1] with 0 bytes of input
2022-06-20T11:46:40.716053Z info: DurableTask.Dapr[7] 03c538d0603f4f3e942995eaf00503f9: Scheduling 1 activity tasks: DaprTesting.SayHello#1
2022-06-20T11:46:40.741876Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9:activity:1: Creating reminder 'execute' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:40.783975Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9:activity:1: Reminder 'execute' fired.
2022-06-20T11:46:40.798599Z info: DurableTask.Core[60] 03c538d0603f4f3e942995eaf00503f9: Starting task activity [DaprTesting.SayHello#1]
2022-06-20T11:46:40.799441Z info: DurableTask.Core[61] 03c538d0603f4f3e942995eaf00503f9: Task activity [DaprTesting.SayHello#1] completed successfully
2022-06-20T11:46:40.900538Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9: Creating reminder 'task-completed' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:40.935004Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9: Reminder 'task-completed' fired.
2022-06-20T11:46:40.949429Z info: DurableTask.Core[51] 03c538d0603f4f3e942995eaf00503f9: Executing 'DaprTesting.HelloCitiesOrchestration' orchestration logic
2022-06-20T11:46:40.952257Z info: DurableTask.Core[52] 03c538d0603f4f3e942995eaf00503f9: Orchestration 'DaprTesting.HelloCitiesOrchestration' awaited and scheduled 1 durable operation(s).
2022-06-20T11:46:40.952293Z info: DurableTask.Core[46] 03c538d0603f4f3e942995eaf00503f9: Scheduling activity [DaprTesting.SayHello#2] with 0 bytes of input
2022-06-20T11:46:40.952327Z info: DurableTask.Dapr[7] 03c538d0603f4f3e942995eaf00503f9: Scheduling 1 activity tasks: DaprTesting.SayHello#2
2022-06-20T11:46:40.973304Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9:activity:2: Creating reminder 'execute' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:41.034533Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9:activity:2: Reminder 'execute' fired.
2022-06-20T11:46:41.036540Z info: DurableTask.Core[60] 03c538d0603f4f3e942995eaf00503f9: Starting task activity [DaprTesting.SayHello#2]
2022-06-20T11:46:41.036611Z info: DurableTask.Core[61] 03c538d0603f4f3e942995eaf00503f9: Task activity [DaprTesting.SayHello#2] completed successfully
2022-06-20T11:46:41.146714Z info: DurableTask.Dapr[1] 03c538d0603f4f3e942995eaf00503f9: Creating reminder 'task-completed' with due time 00:00:00 and recurrence -00:00:00.0010000.
2022-06-20T11:46:41.192156Z info: DurableTask.Dapr[2] 03c538d0603f4f3e942995eaf00503f9: Reminder 'task-completed' fired.
2022-06-20T11:46:41.192773Z info: DurableTask.Core[51] 03c538d0603f4f3e942995eaf00503f9: Executing 'DaprTesting.HelloCitiesOrchestration' orchestration logic
2022-06-20T11:46:41.196710Z info: DurableTask.Core[52] 03c538d0603f4f3e942995eaf00503f9: Orchestration 'DaprTesting.HelloCitiesOrchestration' awaited and scheduled 1 durable operation(s).
2022-06-20T11:46:41.199325Z info: DurableTask.Core[49] 03c538d0603f4f3e942995eaf00503f9: Orchestration completed with a 'Completed' status and 46 bytes of output. Details:
Orchestration Completed! Raw output: "Hello, Tokyo! Hello, London! Hello, Seattle!"
Press [ENTER] to exit.
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

You can use the `KEYS myapp*` command to list all workflow instances. The following shows the output when three workflows have been created:

```
dapr_redis:6379> KEYS myapp*
1) "myapp2||WorkflowActor||96eb655fb257481bb7c0d7b6a64b2622||state"
2) "myapp2||WorkflowActor||9a66a0d88d29493482f47e24993da8a9||state"
3) "myapp2||WorkflowActor||03c538d0603f4f3e942995eaf00503f9||state"
```

These keys correspond to Redis hash values. You can query the values using the `HGET {key} data` command where `{key}` is one of the keys listed in the previous `KEYS` command.

```
dapr_redis:6379> HGET "myapp2||WorkflowActor||03c538d0603f4f3e942995eaf00503f9||state" data
"{\"orchestrationStatus\":1,\"instanceId\":\"03c538d0603f4f3e942995eaf00503f9\",\"input\":null,\"customStatus\":null,\"inbox\":[],\"history\":[{\"eventId\":-1,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:40.1086205Z\",\"eventType\":12,\"extensionData\":null},{\"eventType\":0,\"extensionData\":{},\"eventId\":-1,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:39.1952429Z\"},{\"eventId\":0,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.2641071Z\",\"eventType\":4,\"extensionData\":null},{\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.2651619Z\",\"eventType\":13,\"extensionData\":null,\"eventId\":-1},{\"eventId\":-1,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:40.6980195Z\",\"eventType\":12,\"extensionData\":null},{\"timestamp\":\"2022-06-20T23:46:40.6365553Z\",\"eventType\":5,\"extensionData\":null,\"eventId\":-1,\"isPlayed\":true},{\"eventId\":1,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.7159287Z\",\"eventType\":4,\"extensionData\":null},{\"eventType\":13,\"extensionData\":null,\"eventId\":-1,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.7159729Z\"},{\"eventId\":-1,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:40.9493094Z\",\"eventType\":12,\"extensionData\":null},{\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.9005211Z\",\"eventType\":5,\"extensionData\":null,\"eventId\":-1},{\"eventType\":4,\"extensionData\":null,\"eventId\":2,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.9522903Z\"},{\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:40.9522987Z\",\"eventType\":13,\"extensionData\":null,\"eventId\":-1},{\"eventType\":12,\"extensionData\":null,\"eventId\":-1,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:41.1927511Z\"},{\"eventId\":-1,\"isPlayed\":true,\"timestamp\":\"2022-06-20T23:46:41.146698Z\",\"eventType\":5,\"extensionData\":null},{\"eventId\":3,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:41.1985663Z\",\"eventType\":1,\"extensionData\":null},{\"eventId\":-1,\"isPlayed\":false,\"timestamp\":\"2022-06-20T23:46:41.2005877Z\",\"eventType\":13,\"extensionData\":null}],\"name\":\"DaprTesting.HelloCitiesOrchestration\",\"createdTimeUtc\":\"2022-06-20T23:46:39.8561888Z\",\"lastUpdatedTimeUtc\":\"2022-06-20T23:46:41.20076Z\",\"completedTimeUtc\":\"2022-06-20T23:46:41.20076Z\",\"executionId\":\"dede42cbb8b345cba8857a221265a60d\",\"output\":\"\\\"Hello, Tokyo! Hello, London! Hello, Seattle!\\\"\",\"timers\":[],\"sequenceNumber\":4}"
```

## Clearing workflow state in Redis

To delete all workflow state is redis, simply delete all the data in the redis DB (WARNING: This deletes all state, not just workflow state).

```
dapr_redis:6379> FLUSHDB
```