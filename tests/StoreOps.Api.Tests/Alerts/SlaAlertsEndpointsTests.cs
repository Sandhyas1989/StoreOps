using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Shared.Events;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Alerts;

public sealed class SlaAlertsEndpointsTests : ApiTestBase
{
    private const string EvaluationsPath = "/api/activities/sla-evaluations";

    private static readonly DateTimeOffset DueAt = At(8, 45);

    [Fact]
    public async Task Breach_creates_warning_alert_for_store_shift_lead()
    {
        var activity = await CreateAsync("Freezer temp log", "STORE-001");

        var result = await EvaluateAsync();

        Assert.Equal([activity.Id], result.NewlyBreached);
        var alerts = await GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.Type == AlertTypes.SlaBreach);
        Assert.Equal(activity.Id, alert.SourceId);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal("activities", alert.SourceModule);
        Assert.Equal(StaffSeed.ShiftLeadId, alert.RecipientStaffId);
        Assert.Equal(StaffRole.ShiftLead, alert.RecipientRole);
        Assert.Contains("Freezer temp log", alert.Message);
        Assert.DoesNotContain(alerts, a => a.Type == AlertTypes.SlaEscalation);
    }

    [Fact]
    public async Task Breach_in_store_without_shift_lead_creates_no_alert()
    {
        var activity = await CreateAsync("Orphan store check", "STORE-999");

        var result = await EvaluateAsync();

        Assert.Equal([activity.Id], result.NewlyBreached);
        var published = Assert.Single(Factory.Events.Of<ActivitySlaBreached>());
        Assert.Equal(activity.Id, published.ActivityId);
        Assert.Equal(SlaStage.Breach, published.Stage);
        var alerts = await GetAlertsAsync();
        Assert.Single(alerts, a => a.Type == AlertTypes.HighPriorityActivity && a.SourceId == activity.Id);
        Assert.DoesNotContain(alerts, a => a.Type == AlertTypes.SlaBreach && a.SourceId == activity.Id);
    }

    [Fact]
    public async Task Escalation_creates_critical_alert_for_store_manager()
    {
        var activity = await CreateAsync("Freezer temp log", "STORE-001");
        var breach = await EvaluateAsync();
        Assert.Equal([activity.Id], breach.NewlyBreached);
        Assert.Single(await GetAlertsAsync(), a => a.Type == AlertTypes.SlaBreach && a.SourceId == activity.Id);

        Factory.Clock.SetUtcNow(At(9, 15));
        var escalation = await EvaluateAsync();

        Assert.Equal([activity.Id], escalation.NewlyEscalated);
        var alerts = await GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.Type == AlertTypes.SlaEscalation);
        Assert.Equal(activity.Id, alert.SourceId);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal(StaffSeed.StoreManagerId, alert.RecipientStaffId);
        Assert.Equal(StaffRole.StoreManager, alert.RecipientRole);
        Assert.Equal(
            2,
            alerts.Count(a => a.SourceId == activity.Id && a.Type is AlertTypes.SlaBreach or AlertTypes.SlaEscalation));
    }

    [Fact]
    public async Task Duplicate_breach_events_create_one_alert_per_recipient()
    {
        var activity = await CreateAsync("Freezer temp log", "STORE-001");
        await EvaluateAsync();
        var recorded = Assert.Single(Factory.Events.Of<ActivitySlaBreached>(), e => e.Stage == SlaStage.Breach);
        Assert.Equal(activity.Id, recorded.ActivityId);

        using (var scope = Factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await bus.PublishAsync(recorded, CancellationToken.None);
            await bus.PublishAsync(recorded, CancellationToken.None);
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.Empty((await EvaluateAsync()).NewlyBreached);
        }

        var alerts = await GetAlertsAsync();
        var alert = Assert.Single(alerts, a => a.Type == AlertTypes.SlaBreach && a.SourceId == activity.Id);
        Assert.Equal(StaffSeed.ShiftLeadId, alert.RecipientStaffId);
        Assert.Equal(3, Factory.Events.Of<ActivitySlaBreached>().Count);
    }

    [Fact]
    public async Task Alerts_filter_by_recipient_and_combine_with_severity()
    {
        var activity = await CreateAsync("Freezer temp log", "STORE-001");
        await EvaluateAsync();
        Factory.Clock.SetUtcNow(At(9, 15));
        await EvaluateAsync();

        var all = await GetAlertsAsync();
        Assert.Equal(3, all.Count);
        Assert.Single(all, a => a.Type == AlertTypes.HighPriorityActivity && a.RecipientStaffId is null);

        var shiftLead = await GetAlertsAsync($"?recipientStaffId={StaffSeed.ShiftLeadId}");
        var breach = Assert.Single(shiftLead);
        Assert.Equal(AlertTypes.SlaBreach, breach.Type);
        Assert.Equal(activity.Id, breach.SourceId);
        Assert.Equal(StaffSeed.ShiftLeadId, breach.RecipientStaffId);

        var managerWarnings = await GetAlertsAsync($"?recipientStaffId={StaffSeed.StoreManagerId}&severity=Warning");
        Assert.Empty(managerWarnings);

        var managerCritical = await GetAlertsAsync($"?recipientStaffId={StaffSeed.StoreManagerId}&severity=Critical");
        var escalation = Assert.Single(managerCritical);
        Assert.Equal(AlertTypes.SlaEscalation, escalation.Type);

        Assert.Empty(await GetAlertsAsync($"?recipientStaffId={Guid.NewGuid()}"));
    }

    [Fact]
    public async Task Malformed_recipient_filter_returns_validation_error()
    {
        var response = await Client.GetAsync("/api/alerts?recipientStaffId=not-a-guid");

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains("recipientStaffId", error.Details!.Keys);
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 10, 1, hour, minute, 0, TimeSpan.Zero);

    private async Task<ActivityResponse> CreateAsync(string title, string storeId)
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new { title, storeId, priority = "High", dueAt = DueAt });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(DueAt, created.DueAt);
        return created;
    }

    private async Task<SlaEvaluationResponse> EvaluateAsync()
    {
        var response = await Client.PostAsync(EvaluationsPath, content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SlaEvaluationResponse>();
    }

    private async Task<List<AlertResponse>> GetAlertsAsync(string query = "") =>
        await (await Client.GetAsync("/api/alerts" + query)).ReadAsync<List<AlertResponse>>();
}
