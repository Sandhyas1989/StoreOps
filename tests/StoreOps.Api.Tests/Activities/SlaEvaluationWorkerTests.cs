using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Activities;

public sealed class SlaEvaluationWorkerTests : ApiTestBase
{
    private static readonly DateTimeOffset WithinGraceDueAt = new(2026, 10, 1, 8, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task Worker_publishes_breach_on_each_interval_tick()
    {
        using var enabled = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Sla:WorkerEnabled", "true");
            builder.UseSetting("Sla:EvaluationInterval", "00:01:00");
        });
        using var client = enabled.CreateClient();
        var activity = await CreateAsync(client);
        Assert.Empty(Factory.Events.Of<ActivitySlaBreached>());

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var stopwatch = Stopwatch.StartNew();
        while (Factory.Events.Of<ActivitySlaBreached>().Count == 0 && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(50);

        var published = Assert.Single(Factory.Events.Of<ActivitySlaBreached>());
        Assert.Equal(activity.Id, published.ActivityId);
        Assert.Equal(SlaStage.Breach, published.Stage);
    }

    [Fact]
    public async Task Disabled_worker_does_not_evaluate()
    {
        var activity = await CreateAsync(Client);

        for (var i = 0; i < 5; i++)
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        var hostedServices = Factory.Services.GetServices<IHostedService>().ToList();
        Assert.NotEmpty(hostedServices);
        Assert.DoesNotContain(hostedServices, service => service is SlaEvaluationWorker);
        Assert.Empty(Factory.Events.Of<ActivitySlaBreached>());
        var response = await Client.PostAsync("/api/activities/sla-evaluations", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<SlaEvaluationResponse>();
        Assert.Equal([activity.Id], result.NewlyBreached);
        Assert.Empty(result.NewlyEscalated);
    }

    private static async Task<ActivityResponse> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/activities", new
        {
            title = "High SLA worker check",
            storeId = "STORE-001",
            priority = "High",
            dueAt = WithinGraceDueAt,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(WithinGraceDueAt, created.DueAt);
        return created;
    }
}
