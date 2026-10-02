using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StoreOps.Api.Shared.Events;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Shared;

public sealed class InMemoryEventBusTests
{
    private sealed record TestEvent(DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed class FailingHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent integrationEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated unexpected handler failure.");
    }

    [Fact]
    public async Task Publish_delivers_event_to_every_registered_handler()
    {
        var recorder = new EventRecorder();
        await using var provider = BuildProvider(recorder, typeof(RecordingEventHandler<TestEvent>), typeof(RecordingEventHandler<TestEvent>));
        var integrationEvent = new TestEvent(DateTimeOffset.UnixEpoch);

        await new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance).PublishAsync(integrationEvent);

        Assert.Equal([integrationEvent, integrationEvent], recorder.Of<TestEvent>());
    }

    [Fact]
    public async Task Failing_handler_does_not_fail_publisher_or_block_other_handlers()
    {
        var recorder = new EventRecorder();
        await using var provider = BuildProvider(recorder, typeof(FailingHandler), typeof(RecordingEventHandler<TestEvent>));

        await new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance).PublishAsync(new TestEvent(DateTimeOffset.UnixEpoch));

        Assert.Single(recorder.Of<TestEvent>());
    }

    [Fact]
    public async Task Publish_without_handlers_completes()
    {
        await using var provider = BuildProvider(new EventRecorder());

        await new InMemoryEventBus(provider, NullLogger<InMemoryEventBus>.Instance).PublishAsync(new TestEvent(DateTimeOffset.UnixEpoch));
    }

    private static ServiceProvider BuildProvider(EventRecorder recorder, params Type[] handlerTypes)
    {
        var services = new ServiceCollection().AddSingleton(recorder);
        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(typeof(IEventHandler<TestEvent>), handlerType);
        }

        return services.BuildServiceProvider();
    }
}
