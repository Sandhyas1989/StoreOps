using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-memory with its own in-memory stores. Adds recording handlers alongside the real ones
/// so tests can assert which integration events were published, and replaces the system clock with
/// <see cref="Clock"/> so tests control time.
/// </summary>
public sealed class StoreOpsApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset ClockStart = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public EventRecorder Events { get; } = new();

    /// <summary>Starts at 09:00:00Z, moves forward only, and adds 1 ms on every read.</summary>
    public FakeTimeProvider Clock { get; } = new(ClockStart) { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Sla:WorkerEnabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton(Events);
            services.AddScoped<IEventHandler<ActivityCreated>, RecordingEventHandler<ActivityCreated>>();
            services.AddScoped<IEventHandler<ActivityDeleted>, RecordingEventHandler<ActivityDeleted>>();
            services.AddScoped<IEventHandler<ActivitySlaBreached>, RecordingEventHandler<ActivitySlaBreached>>();
            services.AddScoped<IEventHandler<ProgrammeMemberAdded>, RecordingEventHandler<ProgrammeMemberAdded>>();
        });
    }
}
