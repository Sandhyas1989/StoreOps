namespace StoreOps.Api.Shared.Events;

/// <summary>
/// In-process event bus. Handlers run sequentially within the publisher's request scope. A failing handler is
/// logged and does not fail the publisher, because the publisher's own change has already been committed.
/// </summary>
public sealed class InMemoryEventBus(IServiceProvider services, ILogger<InMemoryEventBus> logger) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(integrationEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handler {Handler} failed for event {Event}", handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
