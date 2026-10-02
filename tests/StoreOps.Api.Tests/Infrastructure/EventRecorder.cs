using System.Collections.Concurrent;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Tests.Infrastructure;

public sealed class EventRecorder
{
    private readonly ConcurrentQueue<IIntegrationEvent> _events = new();

    public void Record(IIntegrationEvent integrationEvent) => _events.Enqueue(integrationEvent);

    public IReadOnlyList<TEvent> Of<TEvent>()
        where TEvent : IIntegrationEvent => _events.OfType<TEvent>().ToList();
}

public sealed class RecordingEventHandler<TEvent>(EventRecorder recorder) : IEventHandler<TEvent>
    where TEvent : IIntegrationEvent
{
    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        recorder.Record(integrationEvent);
        return Task.CompletedTask;
    }
}
