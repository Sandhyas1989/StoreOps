using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Alerts;

public sealed class AlertsEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Get_alerts_returns_empty_list_when_no_alerts_raised()
    {
        var response = await Client.GetAsync("/api/alerts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.ReadAsync<List<AlertResponse>>());
    }

    [Fact]
    public async Task High_priority_activity_raises_alert_via_event_bus()
    {
        var activity = await Client.CreateActivityAsync("Freezer failure", priority: "High");

        var alerts = await (await Client.GetAsync("/api/alerts")).ReadAsync<List<AlertResponse>>();

        var alert = Assert.Single(alerts);
        Assert.Equal(activity.Id, alert.SourceId);
        Assert.Equal(AlertTypes.HighPriorityActivity, alert.Type);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal("activities", alert.SourceModule);
        Assert.Contains("Freezer failure", alert.Message);
    }

    [Fact]
    public async Task Low_priority_activity_does_not_raise_alert()
    {
        await Client.CreateActivityAsync("Tidy stockroom", priority: "Low");

        var alerts = await (await Client.GetAsync("/api/alerts")).ReadAsync<List<AlertResponse>>();

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task Get_alerts_filters_by_severity()
    {
        var critical = await RaiseAsync(AlertSeverity.Critical);
        await RaiseAsync(AlertSeverity.Info);
        await RaiseAsync(AlertSeverity.Warning);

        var all = await (await Client.GetAsync("/api/alerts")).ReadAsync<List<AlertResponse>>();
        var filtered = await (await Client.GetAsync("/api/alerts?severity=Critical")).ReadAsync<List<AlertResponse>>();

        Assert.Equal(3, all.Count);
        Assert.Equal(critical.Id, Assert.Single(filtered).Id);
    }

    [Fact]
    public async Task Get_alerts_with_unknown_severity_returns_validation_error()
    {
        var response = await Client.GetAsync("/api/alerts?severity=Catastrophic");

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Contains("severity", error.Details!.Keys);
    }

    private async Task<Alert> RaiseAsync(AlertSeverity severity)
    {
        using var scope = Factory.Services.CreateScope();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
        return await alerts.RaiseAsync(
            new RaiseAlertCommand("TEST_ALERT", severity, $"{severity} test alert", "tests", SourceId: null),
            CancellationToken.None);
    }
}
