using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NflCoverage.SharedKernel;

/// <summary>Marker for events that cross module boundaries. They live in a module's Contracts project.</summary>
public interface IIntegrationEvent;

public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}

public interface IHandles<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

/// <summary>
/// In-process, fire-and-forget bus. Events are queued and dispatched on a background loop, each handler in its
/// own DI scope. Delivery is not durable: handlers are expected to be idempotent recomputations, so a lost event
/// is repaired by the next scheduled refresh.
/// </summary>
internal sealed class InProcessEventBus(IServiceScopeFactory scopes, ILogger<InProcessEventBus> logger)
    : BackgroundService, IEventBus
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent =>
        _queue.Writer.WriteAsync(
            async (services, ct) =>
            {
                foreach (var handler in services.GetServices<IHandles<TEvent>>())
                {
                    try
                    {
                        await handler.HandleAsync(@event, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Handler {Handler} failed for {Event}", handler.GetType().Name, @event);
                    }
                }
            },
            cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var dispatch in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            await dispatch(scope.ServiceProvider, stoppingToken);
        }
    }
}

public static class EventBusServiceCollectionExtensions
{
    public static IServiceCollection AddInProcessEventBus(this IServiceCollection services)
    {
        services.AddSingleton<InProcessEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<InProcessEventBus>());
        services.AddHostedService(sp => sp.GetRequiredService<InProcessEventBus>());
        return services;
    }
}
