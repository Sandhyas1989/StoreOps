using System.Net;
using System.Net.Http.Json;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Reports;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Reports;

public sealed class ReadOnlyModulesEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Get_staff_returns_seeded_staff()
    {
        var staff = await (await Client.GetAsync("/api/staff")).ReadAsync<List<StaffResponse>>();

        Assert.Equal(StaffSeed.All.Select(s => s.Id).Order(), staff.Select(s => s.Id).Order());
        Assert.Contains(staff, s => s.Id == StaffSeed.StoreManagerId && s.Role == StaffRole.StoreManager);
    }

    [Fact]
    public async Task Get_unknown_staff_returns_not_found()
    {
        var response = await Client.GetAsync($"/api/staff/{Guid.NewGuid()}");

        var error = await response.ReadErrorAsync(HttpStatusCode.NotFound);
        Assert.Equal(StaffService.NotFoundCode, error.Code);
    }

    [Fact]
    public async Task Report_summary_aggregates_exact_counts_from_other_modules()
    {
        await Client.CreateActivityAsync("Urgent recall", priority: "High");
        var closedHigh = await Client.CreateActivityAsync("Spill cleaned", priority: "High");
        await Client.PatchAsJsonAsync($"/api/activities/{closedHigh.Id}", new { status = "Completed" });
        await Client.CreateActivityAsync("Face up shelves", priority: "Low");

        var programme = await (await Client.PostAsJsonAsync("/api/programmes", new { name = "Induction" }))
            .ReadAsync<ProgrammeResponse>();
        await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = StaffSeed.AssociateId });
        await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = StaffSeed.ShiftLeadId });

        var report = await (await Client.GetAsync("/api/reports/summary")).ReadAsync<StoreSummaryReport>();

        Assert.Equal(3, report.TotalActivities);
        Assert.Equal(2, report.ActivitiesByStatus[nameof(ActivityStatus.Pending)]);
        Assert.Equal(1, report.ActivitiesByStatus[nameof(ActivityStatus.Completed)]);
        Assert.Equal(0, report.ActivitiesByStatus[nameof(ActivityStatus.InProgress)]);
        Assert.Equal(1, report.OpenHighPriorityActivities);
        Assert.Equal(1, report.TotalProgrammes);
        Assert.Equal(2, report.TotalProgrammeMemberships);
        Assert.Equal(StaffSeed.All.Count, report.TotalStaff);
    }

    [Fact]
    public async Task Report_summary_does_not_change_source_data()
    {
        var activity = await Client.CreateActivityAsync("Stock count");

        await Client.GetAsync("/api/reports/summary");

        var after = await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{activity.Id}", TestJson.Options);
        Assert.Equal(activity, after);
        Assert.Single(Factory.Events.Of<ActivityCreated>());
    }
}
