using System.Net;
using System.Net.Http.Json;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Programmes;

public sealed class ProgrammesEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Get_programmes_returns_exactly_the_created_programmes()
    {
        var created = await CreateProgrammeAsync();

        var response = await Client.GetAsync("/api/programmes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var programmes = await response.ReadAsync<List<ProgrammeResponse>>();
        Assert.Equal([created.Id], programmes.Select(p => p.Id));
    }

    [Fact]
    public async Task Post_programme_creates_programme_with_no_members()
    {
        var response = await Client.PostAsJsonAsync("/api/programmes", new
        {
            name = "Autumn range reset",
            startDate = "2026-10-01",
            endDate = "2026-10-31",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ProgrammeResponse>();
        Assert.Equal("Autumn range reset", created.Name);
        Assert.Equal(new DateOnly(2026, 10, 1), created.StartDate);
        Assert.Empty(created.MemberStaffIds);
        Assert.Equal($"/api/programmes/{created.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Post_programme_with_blank_name_returns_validation_error()
    {
        var response = await Client.PostAsJsonAsync("/api/programmes", new { name = "   " });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["Name is required."], error.Details!["name"]);
    }

    [Fact]
    public async Task Post_programme_with_end_before_start_returns_validation_error()
    {
        var response = await Client.PostAsJsonAsync("/api/programmes", new
        {
            name = "Backwards",
            startDate = "2026-10-31",
            endDate = "2026-10-01",
        });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains("endDate", error.Details!.Keys);
    }

    [Fact]
    public async Task Post_member_adds_staff_to_programme_and_publishes_event()
    {
        var programme = await CreateProgrammeAsync();

        var response = await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = StaffSeed.AssociateId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<ProgrammeResponse>();
        Assert.Equal([StaffSeed.AssociateId], updated.MemberStaffIds);

        var published = Assert.Single(Factory.Events.Of<ProgrammeMemberAdded>());
        Assert.Equal(programme.Id, published.ProgrammeId);
        Assert.Equal(StaffSeed.AssociateId, published.StaffId);
    }

    [Fact]
    public async Task Post_duplicate_member_returns_conflict_and_keeps_single_membership()
    {
        var programme = await CreateProgrammeAsync();
        await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = StaffSeed.ShiftLeadId });

        var response = await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = StaffSeed.ShiftLeadId });

        var error = await response.ReadErrorAsync(HttpStatusCode.Conflict);
        Assert.Equal(ProgrammeService.MemberExistsCode, error.Code);
        var stored = await Client.GetFromJsonAsync<ProgrammeResponse>($"/api/programmes/{programme.Id}", TestJson.Options);
        Assert.Equal([StaffSeed.ShiftLeadId], stored!.MemberStaffIds);
        Assert.Single(Factory.Events.Of<ProgrammeMemberAdded>());
    }

    [Fact]
    public async Task Post_unknown_staff_member_returns_validation_error()
    {
        var programme = await CreateProgrammeAsync();

        var response = await Client.PostAsJsonAsync($"/api/programmes/{programme.Id}/members", new { staffId = Guid.NewGuid() });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains("staffId", error.Details!.Keys);
        Assert.Empty(Factory.Events.Of<ProgrammeMemberAdded>());
    }

    [Fact]
    public async Task Post_member_to_unknown_programme_returns_not_found()
    {
        var response = await Client.PostAsJsonAsync($"/api/programmes/{Guid.NewGuid()}/members", new { staffId = StaffSeed.AssociateId });

        var error = await response.ReadErrorAsync(HttpStatusCode.NotFound);
        Assert.Equal(ProgrammeService.NotFoundCode, error.Code);
    }

    private async Task<ProgrammeResponse> CreateProgrammeAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/programmes", new { name = "Food safety refresher" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<ProgrammeResponse>();
    }
}
