using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace CustomsCloud.InfrastructureCore.Worker;

public abstract class PeriodicalBatch<TMessage>(IServiceProvider serviceProvider) : BackgroundService
    where TMessage : class
{
    private int _locker;
    private Timer _timer = null!;
    private readonly ConcurrentQueue<TMessage> _queue = new();
    private readonly Channel<TMessage> _channel = serviceProvider.GetRequiredService<Channel<TMessage>>();
    private readonly ILogger<PeriodicalBatch<TMessage>> _logger = serviceProvider.GetRequiredService<ILogger<PeriodicalBatch<TMessage>>>();
    private readonly PeriodicalBatchOptions<TMessage> _options = serviceProvider.GetRequiredService<PeriodicalBatchOptions<TMessage>>();

    private AsyncRetryPolicy? _policy;

    protected AsyncRetryPolicy RetryPolicy
    {
        get
        {
            _policy ??= Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(_options.RetryCount, retryAttempt => TimeSpan.FromSeconds(0.5 + Math.Pow(2, retryAttempt - 1)));

            return _policy;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new Timer(_options.Period);
        _timer.Elapsed += TimerElapsed;
        _timer.Start();

        _logger.LogDebug("Execute periodical batch service {Name} (Batch Size: {BatchSize}, Period: {Period}, Retry: {Retry}, Retry Count: {RetryCount})",
            GetType().Name, _options.BatchSize, _options.Period, _options.Retry, _options.RetryCount);

        var reader = _channel.Reader;
        while (!reader.Completion.IsCompleted && await reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            if (reader.TryRead(out var message))
            {
                _queue.Enqueue(message);
                _ = CheckQueueSize();
            }
        }

        _channel.Writer.TryComplete();
    }

    private void TimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        SafeHandleQueue();
    }

    private async Task CheckQueueSize()
    {
        if (_queue.Count >= _options.BatchSize)
        {
            await Task.Run(SafeHandleQueue);
        }
    }

    private void SafeHandleQueue()
    {
        try
        {
            _timer?.Stop();
            if (0 != Interlocked.Exchange(ref _locker, 1)) { return; } // acquired the lock
            do
            {
                HandleBatch();
            }
            while (_queue.Count > _options.BatchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to handle PeriodicalBatch queue ({Name})", GetType().FullName);
        }
        finally
        {
            // Release the lock
            Interlocked.Exchange(ref _locker, 0);
            _timer.Start();
        }
    }

    private void HandleBatch()
    {
        if (_queue.IsEmpty)
        {
            return;
        }

        var chunk = new List<TMessage>(_options.BatchSize);
        for (var i = 0; i < _options.BatchSize; i++)
        {
            if (!_queue.TryDequeue(out var item))
            {
                break;
            }

            chunk.Add(item);
        }

        _ = Task.Run(async () => await HandleBatchInner(chunk))
            .ContinueWith(t =>
            {
                if (t.Exception == null) { return; }
                _logger.LogError(t.Exception, "Fail to handle PeriodicalBatch batch ({Name})", GetType().FullName);
            });
    }

    private Task HandleBatchInner(IEnumerable<TMessage> items)
    {
        if (_options.Retry)
        {
            return RetryPolicy.ExecuteAsync(() => HandleBatch(items));
        }
        else
        {
            return HandleBatch(items);
        }
    }

    protected abstract Task HandleBatch(IEnumerable<TMessage> items);
}

public static class PeriodicalBatchExtentions
{
    public static IServiceCollection AddPeriodicalBatchService<TService, TMessage>(this IServiceCollection services)
        where TService : PeriodicalBatch<TMessage>
        where TMessage : class
    {
        services.AddHostedService<TService>();
        services.AddSingleton<TService>();
        services.AddSingleton<PeriodicalBatchProducer<TMessage>>();
        services.AddSingleton(Channel.CreateUnbounded<TMessage>());
        services.AddSingleton<PeriodicalBatchOptions<TMessage>>(PeriodicalBatchOptions<TMessage>.Empty);

        return services;
    }

    public static IServiceCollection AddPeriodicalBatchService<TService, TMessage>(this IServiceCollection services, Action<PeriodicalBatchOptionsBuilder<TMessage>> options)
        where TService : PeriodicalBatch<TMessage>
        where TMessage : class
    {
        services.AddHostedService<TService>();
        services.AddSingleton<TService>();
        services.AddSingleton<PeriodicalBatchProducer<TMessage>>();
        services.AddSingleton(Channel.CreateUnbounded<TMessage>());

        services.AddSingleton<PeriodicalBatchOptions<TMessage>>(p =>
        {
            var builder = new PeriodicalBatchOptionsBuilder<TMessage>();
            options(builder);
            return builder.Build();
        });

        return services;
    }
}

public class PeriodicalBatchOptions<TMessage>
    where TMessage : class
{
    public int BatchSize { get; internal set; } = 300;
    public TimeSpan Period { get; internal set; } = TimeSpan.FromSeconds(3);
    public bool Retry { get; internal set; } = true;
    public int RetryCount { get; internal set; } = 3;

    internal static PeriodicalBatchOptions<TMessage> Empty => new();
}

public class PeriodicalBatchOptionsBuilder<TMessage>
    where TMessage : class
{
    private readonly PeriodicalBatchOptions<TMessage> _options = new();

    internal PeriodicalBatchOptionsBuilder()
    {
    }

    public PeriodicalBatchOptionsBuilder<TMessage> WithBatchSize(int batchSize)
    {
        _options.BatchSize = batchSize;
        return this;
    }

    public PeriodicalBatchOptionsBuilder<TMessage> WithPeriod(TimeSpan period)
    {
        _options.Period = period;
        return this;
    }

    public PeriodicalBatchOptionsBuilder<TMessage> WithoutRetry()
    {
        _options.Retry = false;
        return this;
    }

    public PeriodicalBatchOptionsBuilder<TMessage> WithRetryCount(int retryCount)
    {
        _options.RetryCount = retryCount;
        return this;
    }

    internal PeriodicalBatchOptions<TMessage> Build()
    {
        return _options;
    }
}

public class PeriodicalBatchProducer<T>(Channel<T> channel, ILogger<PeriodicalBatchProducer<T>> logger)
    where T : class
{
    public async Task PublishAsync(T message)
    {
        try
        {
            while (await channel.Writer.WaitToWriteAsync().ConfigureAwait(false))
            {
                if (channel.Writer.TryWrite(message)) { break; }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fail to publish message to PeriodicalBatch ({Name})", GetType().Name);
        }
    }
}
