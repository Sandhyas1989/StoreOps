using System.Net;
using System.Net.Http.Json;
using System.Text;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Activities;

public sealed class ActivitiesEndpointsTests : ApiTestBase
{
    [Fact]
    public async Task Get_activities_returns_exactly_the_created_activities_in_creation_order()
    {
        var first = await Client.CreateActivityAsync("Restock aisle 4");
        var second = await Client.CreateActivityAsync("Restock aisle 5");

        var response = await Client.GetAsync("/api/activities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activities = await response.ReadAsync<List<ActivityResponse>>();
        Assert.Equal([first.Id, second.Id], activities.Select(a => a.Id));
    }

    [Fact]
    public async Task Post_activity_creates_pending_activity_and_publishes_event()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new
        {
            title = "  Price change: dairy  ",
            storeId = "STORE-001",
            priority = "Low",
            assignedStaffId = StaffSeed.AssociateId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ActivityResponse>();
        Assert.Equal("Price change: dairy", created.Title);
        Assert.Equal(ActivityStatus.Pending, created.Status);
        Assert.Equal(ActivityPriority.Low, created.Priority);
        Assert.Equal(StaffSeed.AssociateId, created.AssignedStaffId);
        Assert.Equal($"/api/activities/{created.Id}", response.Headers.Location?.AbsolutePath);

        var published = Assert.Single(Factory.Events.Of<ActivityCreated>());
        Assert.Equal(created.Id, published.ActivityId);
        Assert.Equal(ActivityPriority.Low, published.Priority);
    }

    [Fact]
    public async Task Post_activity_without_required_fields_returns_validation_error_and_stores_nothing()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new { description = "no title" });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Equal(["storeId", "title"], error.Details!.Keys.Order());
        Assert.Empty(await Client.GetFromJsonAsync<List<ActivityResponse>>("/api/activities", TestJson.Options) ?? []);
        Assert.Empty(Factory.Events.Of<ActivityCreated>());
    }

    [Fact]
    public async Task Post_activity_with_blank_title_returns_validation_error()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new { title = "   ", storeId = "STORE-001" });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(["Title is required."], error.Details!["title"]);
    }

    [Fact]
    public async Task Post_activity_with_unknown_staff_returns_validation_error()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new
        {
            title = "Fridge check",
            storeId = "STORE-001",
            assignedStaffId = Guid.NewGuid(),
        });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains("assignedStaffId", error.Details!.Keys);
    }

    [Fact]
    public async Task Post_activity_with_unknown_enum_value_reports_field_without_type_names()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new { title = "x", storeId = "STORE-001", priority = "Urgent" });

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Equal([ModelStateErrorFactory.ValueInvalidMessage], error.Details!["priority"]);
        Assert.DoesNotContain(error.Details.Values.SelectMany(m => m), m => m.Contains("System."));
        Assert.Empty(await Client.GetFromJsonAsync<List<ActivityResponse>>("/api/activities", TestJson.Options) ?? []);
        Assert.Empty(Factory.Events.Of<ActivityCreated>());
    }

    [Fact]
    public async Task Post_activity_with_critical_priority_stores_and_publishes_critical()
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new
        {
            title = "Cold-chain check",
            storeId = "STORE-001",
            priority = "Critical",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(ActivityPriority.Critical, created.Priority);
        Assert.Equal("Cold-chain check", created.Title);

        var fetched = await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options);
        Assert.Equal(ActivityPriority.Critical, fetched?.Priority);
        Assert.Equal(created, fetched);

        var published = Assert.Single(Factory.Events.Of<ActivityCreated>());
        Assert.Equal(created.Id, published.ActivityId);
        Assert.Equal(ActivityPriority.Critical, published.Priority);
    }

    [Fact]
    public async Task Patch_activity_to_critical_priority_updates_priority_only()
    {
        var created = await Client.CreateActivityAsync("Stock count", priority: "Low");
        Assert.Equal(ActivityPriority.Low, created.Priority);
        Assert.Equal(ActivityStatus.Pending, created.Status);

        var response = await Client.PatchAsJsonAsync($"/api/activities/{created.Id}", new { priority = "Critical" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(ActivityPriority.Critical, updated.Priority);
        Assert.Equal(created.Title, updated.Title);
        Assert.Equal(created.Status, updated.Status);

        var fetched = await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options);
        Assert.Equal(ActivityPriority.Critical, fetched?.Priority);
        Assert.Equal(updated, fetched);
    }

    [Theory]
    [InlineData("{bad json")]
    [InlineData("null")]
    [InlineData("")]
    public async Task Post_activity_with_invalid_body_returns_sanitised_body_error(string body)
    {
        var response = await Client.PostAsync("/api/activities", new StringContent(body, Encoding.UTF8, "application/json"));

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        var detail = Assert.Single(error.Details!);
        Assert.Equal(ModelStateErrorFactory.BodyField, detail.Key);
        Assert.Equal([ModelStateErrorFactory.BodyInvalidMessage], detail.Value);
    }

    [Fact]
    public async Task Get_activity_by_id_returns_activity()
    {
        var created = await Client.CreateActivityAsync("Planogram reset");

        var response = await Client.GetAsync($"/api/activities/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Get_unknown_activity_returns_typed_not_found_error()
    {
        var response = await Client.GetAsync($"/api/activities/{Guid.NewGuid()}");

        var error = await response.ReadErrorAsync(HttpStatusCode.NotFound);
        Assert.Equal(ActivityService.NotFoundCode, error.Code);
    }

    [Fact]
    public async Task Get_activity_with_malformed_id_returns_validation_error()
    {
        var response = await Client.GetAsync("/api/activities/not-a-guid");

        var error = await response.ReadErrorAsync(HttpStatusCode.BadRequest);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        Assert.Contains("id", error.Details!.Keys);
    }

    [Fact]
    public async Task Patch_activity_updates_only_supplied_fields()
    {
        var created = await Client.CreateActivityAsync("Stock count");

        var response = await Client.PatchAsJsonAsync($"/api/activities/{created.Id}", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(ActivityStatus.InProgress, updated.Status);
        Assert.Equal(created.Title, updated.Title);
        Assert.Equal(created.Priority, updated.Priority);
        Assert.Equal(updated, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options));
    }

    [Fact]
    public async Task Patch_closed_activity_returns_conflict_and_leaves_activity_unchanged()
    {
        var created = await Client.CreateActivityAsync("Stock count");
        var closeResponse = await Client.PatchAsJsonAsync($"/api/activities/{created.Id}", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.ReadAsync<ActivityResponse>();
        Assert.Equal(ActivityStatus.Completed, closed.Status);

        var response = await Client.PatchAsJsonAsync($"/api/activities/{created.Id}", new { title = "Reopen", status = "Pending" });

        var error = await response.ReadErrorAsync(HttpStatusCode.Conflict);
        Assert.Equal(ActivityService.ClosedCode, error.Code);
        Assert.Equal(closed, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options));
    }

    [Fact]
    public async Task Patch_unknown_activity_returns_not_found()
    {
        var response = await Client.PatchAsJsonAsync($"/api/activities/{Guid.NewGuid()}", new { title = "x" });

        var error = await response.ReadErrorAsync(HttpStatusCode.NotFound);
        Assert.Equal(ActivityService.NotFoundCode, error.Code);
    }

    [Fact]
    public async Task Delete_activity_removes_it_from_store_and_publishes_event()
    {
        var kept = await Client.CreateActivityAsync("Keep me");
        var deleted = await Client.CreateActivityAsync("Delete me");

        var deleteResponse = await Client.DeleteAsync($"/api/activities/{deleted.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/activities/{deleted.Id}")).StatusCode);
        var remaining = await Client.GetFromJsonAsync<List<ActivityResponse>>("/api/activities", TestJson.Options);
        Assert.Equal([kept.Id], remaining!.Select(a => a.Id));
        Assert.Equal(deleted.Id, Assert.Single(Factory.Events.Of<ActivityDeleted>()).ActivityId);
    }

    [Fact]
    public async Task Delete_unknown_activity_returns_not_found_and_publishes_nothing()
    {
        var response = await Client.DeleteAsync($"/api/activities/{Guid.NewGuid()}");

        var error = await response.ReadErrorAsync(HttpStatusCode.NotFound);
        Assert.Equal(ActivityService.NotFoundCode, error.Code);
        Assert.Empty(Factory.Events.Of<ActivityDeleted>());
    }
}
