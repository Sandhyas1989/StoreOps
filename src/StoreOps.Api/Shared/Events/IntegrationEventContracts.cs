namespace StoreOps.Api.Shared.Events;

/// <summary>An event published by one module so other modules can react without a direct call.</summary>
public interface IIntegrationEvent
{
    DateTimeOffset OccurredAt { get; }
}

public interface IEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
