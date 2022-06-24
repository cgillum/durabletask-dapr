// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DurableTask.Dapr;

static class Helpers
{
    public static async Task ParallelForEachAsync<T>(this IEnumerable<T> items, int maxConcurrency, Func<T, Task> action)
    {
        if (items.TryGetNonEnumeratedCount(out int count) && count == 0)
        {
            return;
        }

        static async Task InvokeThrottledAction(T item, Func<T, Task> action, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        }

        using var semaphore = new SemaphoreSlim(maxConcurrency);
        List<Task> tasks = count > 0 ? new(count) : new();
        foreach (T item in items)
        {
            tasks.Add(InvokeThrottledAction(item, action, semaphore));
        }

        await Task.WhenAll(tasks);
    }

    public static Task ParallelForEachAsync<T>(this IEnumerable<T> items, Func<T, Task> action)
    {
        // Choosing a max concurrency is a tradeoff between throughput and the overhead of thread creation.
        // A conservative value of 4 feels like a safe default.
        return ParallelForEachAsync(items, maxConcurrency: 4, action);
    }

    public static ILogger CreateLoggerForDaprProvider(this ILoggerFactory? factory)
    {
        return factory?.CreateLogger("DurableTask.Dapr") ?? NullLogger.Instance;
    }

    public static TimeSpan PositiveOrZero(this TimeSpan timeSpan)
    {
        return timeSpan > TimeSpan.Zero ? timeSpan : TimeSpan.Zero;
    }
}
