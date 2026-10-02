using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-memory with its own in-memory stores. Adds recording handlers alongside the real ones
/// so tests can assert which integration events were published.
/// </summary>
public sealed class StoreOpsApiFactory : WebApplicationFactory<Program>
{
    public EventRecorder Events { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Events);
            services.AddScoped<IEventHandler<ActivityCreated>, RecordingEventHandler<ActivityCreated>>();
            services.AddScoped<IEventHandler<ActivityDeleted>, RecordingEventHandler<ActivityDeleted>>();
            services.AddScoped<IEventHandler<ProgrammeMemberAdded>, RecordingEventHandler<ProgrammeMemberAdded>>();
        });
    }
}
