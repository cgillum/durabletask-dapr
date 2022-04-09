
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

        DaprOrchestrationService service = new(new DaprOptions());
        await service.CreateIfNotExistsAsync();

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(builder => builder.TimestampFormat = "o");
        });

        TaskHubWorker worker = new(service, loggerFactory);
        worker.AddTaskOrchestrations(typeof(NoOpOrchestration));
        await worker.StartAsync();

        Console.WriteLine("Press [ENTER] to create an orchestration instance.");
        Console.ReadLine();

        TaskHubClient client = new(service, null, loggerFactory);
        await client.CreateOrchestrationInstanceAsync(typeof(NoOpOrchestration), "Hello, Dapr!");

        Console.WriteLine("Press [ENTER] to exit.");
        Console.ReadLine();
    }
}

class NoOpOrchestration : TaskOrchestration<string, string>
{
    public override Task<string> RunTask(OrchestrationContext context, string input)
    {
        return Task.FromResult(input);
    }
}

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