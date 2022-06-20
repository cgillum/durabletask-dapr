
using Dapr.Actors;
using Dapr.Actors.Runtime;
using DurableTask.Core;
using DurableTask.Dapr;
using Microsoft.Extensions.Logging;

namespace DaprTesting;

static class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting up...");

        // Write internal log messages to the console.
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddThh:mm:ss.ffffffZ ";
            });

            builder.AddFilter("DurableTask", LogLevel.Information);

            // ASP.NET Core logs to warning since they can otherwise be noisy.
            // This should be increased if it's necessary to debug interactions with Dapr.
            builder.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        });

        DaprOrchestrationService service = new(new DaprOptions
        {
            LoggerFactory = loggerFactory,
        });

        // This is currently a no-op, but we do it anyways since that's the pattern
        // DTFx apps are normally supposed to follow.
        await service.CreateIfNotExistsAsync();

        // Register a very simple orchestration and start the worker.
        TaskHubWorker worker = new(service, loggerFactory)
        {
            // This setting is required for failures to work correctly
            ErrorPropagationMode = ErrorPropagationMode.UseFailureDetails,
        };

        worker.AddTaskOrchestrations(
            typeof(EchoOrchestration),
            typeof(SleepOrchestration),
            typeof(HelloCitiesOrchestration));

        worker.AddTaskActivities(
            typeof(SayHello));

        await worker.StartAsync();

        // Need to give time for the actor runtime to finish initializing before we can safely communicate with them.
        Thread.Sleep(TimeSpan.FromSeconds(5));

        Console.WriteLine("Press [ENTER] to create an orchestration instance.");
        Console.ReadLine();

        TaskHubClient client = new(service, null, loggerFactory);

        ////OrchestrationInstance instance = await client.CreateOrchestrationInstanceAsync(
        ////    typeof(EchoOrchestration),
        ////    input: "Hello, Dapr!");
        ////OrchestrationInstance instance = await client.CreateOrchestrationInstanceAsync(
        ////    typeof(SleepOrchestration),
        ////    input: TimeSpan.FromSeconds(10));
        OrchestrationInstance instance = await client.CreateOrchestrationInstanceAsync(
            typeof(HelloCitiesOrchestration),
            input: null);

        Console.WriteLine($"Started orchestration with ID = '{instance.InstanceId}' and waiting for it to complete...");

        OrchestrationState state = await client.WaitForOrchestrationAsync(instance, TimeSpan.FromMinutes(5));
        Console.WriteLine($"Orchestration {state.OrchestrationStatus}! Raw output: {state.Output}");

        Console.WriteLine("Press [ENTER] to exit.");
        Console.ReadLine();
    }
}

/// <summary>
/// Simple orchestration that just saves the input as the output.
/// </summary>
class EchoOrchestration : TaskOrchestration<string, string>
{
    public override Task<string> RunTask(OrchestrationContext context, string input)
    {
        return Task.FromResult(input);
    }
}

/// <summary>
/// Simple orchestration that sleeps for a given number of seconds.
/// </summary>
class SleepOrchestration : TaskOrchestration<TimeSpan, TimeSpan>
{
    public override Task<TimeSpan> RunTask(OrchestrationContext context, TimeSpan delayInput)
    {
        return context.CreateTimer(context.CurrentUtcDateTime.Add(delayInput), delayInput);
    }
}

class HelloCitiesOrchestration : TaskOrchestration<string, string>
{
    public override async Task<string> RunTask(OrchestrationContext context, string input)
    {
        string result = "";
        result += await context.ScheduleTask<string>(typeof(SayHello), "Tokyo") + " ";
        result += await context.ScheduleTask<string>(typeof(SayHello), "London") + " ";
        result += await context.ScheduleTask<string>(typeof(SayHello), "Seattle");
        return result;
    }
}

class SayHello : TaskActivity<string, string>
{
    protected override string Execute(TaskContext context, string input)
    {
        return $"Hello, {input}!";
    }
}

// This is just for ad-hoc testing. It's not actually part of this project in any way.
public interface IMyActor : IActor
{
    Task DoSomethingAsync();
}

public class MyActor : Actor, IMyActor, IRemindable
{
    public MyActor(ActorHost host) 
        : base(host)
    { }

    async Task IMyActor.DoSomethingAsync()
    {
        // Deliver a reminder notification every 5 seconds
        await this.RegisterReminderAsync(
            "MyReminder",
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
    }

    Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
    {
        this.Logger.LogWarning("[{timestamp}] Reminder {reminderName} was called!", DateTime.UtcNow.ToString("o"), reminderName);
        return Task.CompletedTask;
    }
}