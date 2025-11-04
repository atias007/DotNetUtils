using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace InfrastructureCore;

public interface IBoundedChannelPublisher<TMessage>
    where TMessage : class
{
    Task CompleteAsync();

    Task PublishAsync(TMessage message, CancellationToken cancellationToken);
}

public interface IBoundedChannelServiceOptions
{
    int Capacity { get; }
    int MaxParallelTasks { get; }
    TimeSpan ParallelWaitTimeout { get; }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoundedChannelService<TService, TMessage>(
        this IServiceCollection services,
        Action<BoundedChannelServiceOptions>? configureOptions = null)
        where TMessage : class
        where TService : BoundedChannelService<TMessage>
    {
        var options = new BoundedChannelServiceOptions();
        configureOptions?.Invoke(options);
        var channel = Channel.CreateBounded<TMessage>(new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        services.AddSingleton(channel);
        services.AddSingleton<IBoundedChannelServiceOptions>(options);
        services.AddHostedService<TService>();
        services.AddSingleton<IBoundedChannelPublisher<TMessage>, BoundedChannelPublisher<TMessage>>();
        return services;
    }
}

public abstract class BoundedChannelService<TMessage>(IServiceProvider serviceProvider) : BackgroundService
    where TMessage : class
{
    private readonly ILogger<BoundedChannelService<TMessage>> logger = serviceProvider.GetRequiredService<ILogger<BoundedChannelService<TMessage>>>();
    private readonly IBoundedChannelServiceOptions options = serviceProvider.GetRequiredService<IBoundedChannelServiceOptions>();
    private readonly ChannelReader<TMessage> reader = serviceProvider.GetRequiredService<Channel<TMessage>>().Reader;
    private SemaphoreSlim taskQueue = null!;

    public abstract Task HandledMessage(TMessage message, CancellationToken stoppingToken);

    public abstract Task OnUnhandledMessage(TMessage message);

    public abstract Task OnUnhandledMessages(IEnumerable<TMessage> messages);

    public async Task SafeOnUnhandledMessage(TMessage message)
    {
        try
        {
            await OnUnhandledMessage(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error at {Name}", nameof(SafeOnUnhandledMessage));
        }
    }

    public async Task SafeOnUnhandledMessages(IEnumerable<TMessage> messages)
    {
        try
        {
            await OnUnhandledMessages(messages).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error at {Name}", nameof(OnUnhandledMessages));
        }
    }

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        taskQueue = new(options.MaxParallelTasks, options.MaxParallelTasks);
        await SafeStartWaitingAndHadling(stoppingToken).ConfigureAwait(false);
        if (stoppingToken.IsCancellationRequested)
        {
            await FinishChannelQueueAsync().ConfigureAwait(false);
        }
    }

    private async Task FinishChannelQueueAsync()
    {
        await foreach (var message in reader.ReadAllAsync())
        {
            await OnUnhandledMessage(message).ConfigureAwait(false);
        }
    }

    private async Task<bool> SafeAcquiredLock(CancellationToken stoppingToken)
    {
        try
        {
            var ack = await taskQueue.WaitAsync(options.ParallelWaitTimeout, stoppingToken).ConfigureAwait(false);
            return ack;
        }
        catch (TaskCanceledException)
        {
            // === DO NOTHING === //
        }

        return false;
    }

    private async Task SafeHandleMessage(TMessage message, CancellationToken stoppingToken)
    {
        try
        {
            await HandledMessage(message, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await OnUnhandledMessage(message).ConfigureAwait(false);
        }
    }

    private async Task SafeReleaseLock()
    {
        try
        {
            taskQueue.Release();
        }
        catch
        {
            // === DO NOTHING === //
        }
    }

    private async Task SafeStartWaitingAndHadling(CancellationToken stoppingToken)
    {
        try
        {
            await StartWaitingAndHadling(stoppingToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // === DO NOTHING === //
        }
        catch (OperationCanceledException)
        {
            // === DO NOTHING === //
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error at {Name}", nameof(SafeStartWaitingAndHadling));
        }
    }

    private async Task StartWaitingAndHadling(CancellationToken stoppingToken)
    {
        await foreach (var message in reader.ReadAllAsync(stoppingToken))
        {
            var ack = false;
            try
            {
                ack = await SafeAcquiredLock(stoppingToken).ConfigureAwait(false);
            }
            catch
            {
                await OnUnhandledMessage(message);
                throw;
            }

            if (ack)
            {
                _ = SafeHandleMessage(message, stoppingToken)
                    .ContinueWith(_ => SafeReleaseLock());
            }
            else
            {
                await OnUnhandledMessage(message);
            }
        }
    }
}

public class BoundedChannelServiceOptions : IBoundedChannelServiceOptions
{
    public int Capacity { get; set; } = 200;
    public int MaxParallelTasks { get; set; } = 1;
    public TimeSpan ParallelWaitTimeout { get; set; } = TimeSpan.FromMinutes(1);
}

internal class BoundedChannelPublisher<TMessage>(Channel<TMessage> channel)
    : IBoundedChannelPublisher<TMessage>
    where TMessage : class
{
    private readonly ChannelWriter<TMessage> writer = channel.Writer;

    public async Task CompleteAsync()
    {
        writer.Complete();
    }

    public async Task PublishAsync(TMessage message, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(message, cancellationToken);
    }
}