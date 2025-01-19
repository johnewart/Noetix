namespace Noetix.LLM.Requests;

using System;
using System.Threading.Tasks;

public class RetryPolicy(
    int maxAttempts = 3,
    TimeSpan? initialDelay = null,
    TimeSpan? maxDelay = null,
    RetryPolicy.BackoffStrategy backoffStrategy = RetryPolicy.BackoffStrategy.Exponential,
    Func<Exception, bool> shouldRetry = null)
{
    private readonly TimeSpan initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
    private readonly TimeSpan maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
    private readonly Func<Exception, bool> shouldRetry = shouldRetry ?? (ex => true);

    public enum BackoffStrategy
    {
        Constant,
        Linear,
        Exponential
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        int attempt = 1;
        Exception lastException = null;

        while (attempt <= maxAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!shouldRetry(ex) || attempt == maxAttempts)
                {
                    throw;
                }

                TimeSpan delay = CalculateDelay(attempt);
                await Task.Delay(delay);
                attempt++;
            }
        }

        throw lastException;
    }

    public async Task ExecuteAsync(Func<Task> operation)
    {
        int attempt = 1;
        Exception lastException = null;

        while (attempt <= maxAttempts)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (!shouldRetry(ex) || attempt == maxAttempts)
                {
                    throw;
                }

                TimeSpan delay = CalculateDelay(attempt);
                await Task.Delay(delay);
                attempt++;
            }
        }

        throw lastException;
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        TimeSpan delay = backoffStrategy switch
        {
            BackoffStrategy.Constant => initialDelay,
            BackoffStrategy.Linear => TimeSpan.FromTicks(initialDelay.Ticks * attempt),
            BackoffStrategy.Exponential => TimeSpan.FromTicks(initialDelay.Ticks * (long)Math.Pow(2, attempt - 1)),
            _ => initialDelay
        };

        return TimeSpan.FromTicks(Math.Min(delay.Ticks, maxDelay.Ticks));
    }
}