using System.Net;
using System.Net.Http.Json;
using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Activities;

public sealed class SlaEvaluationEndpointsTests : ApiTestBase
{
    private const string EvaluationsPath = "/api/activities/sla-evaluations";

    private static readonly DateTimeOffset WithinGraceDueAt = At(8, 45);

    [Fact]
    public async Task Evaluation_reports_overdue_high_priority_activity_as_breached()
    {
        var activity = await CreateAsync("High", WithinGraceDueAt);

        var t0 = Factory.Clock.GetUtcNow();
        var response = await Client.PostAsync(EvaluationsPath, content: null);
        var t1 = Factory.Clock.GetUtcNow();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<SlaEvaluationResponse>();
        Assert.Equal([activity.Id], result.NewlyBreached);
        Assert.Empty(result.NewlyEscalated);
        Assert.InRange(result.EvaluatedAt, t0.AddTicks(1), t1.AddTicks(-1));
        Assert.Equal(activity, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{activity.Id}", TestJson.Options));
    }

    [Fact]
    public async Task Evaluation_breaches_high_and_critical_and_ignores_low_and_medium()
    {
        var high = await CreateAsync("High", WithinGraceDueAt);
        var critical = await CreateAsync("Critical", WithinGraceDueAt);
        await CreateAsync("Low", WithinGraceDueAt);
        await CreateAsync("Medium", WithinGraceDueAt);

        var result = await EvaluateAsync();

        Assert.Equal(new[] { high.Id, critical.Id }.Order(), result.NewlyBreached.Order());
    }

    [Fact]
    public async Task Evaluation_ignores_completed_and_cancelled_activities()
    {
        var pending = await CreateAsync("High", WithinGraceDueAt);
        var inProgress = await CreateAsync("High", WithinGraceDueAt);
        var completed = await CreateAsync("High", WithinGraceDueAt);
        var cancelled = await CreateAsync("High", WithinGraceDueAt);
        await PatchStatusAsync(inProgress.Id, ActivityStatus.InProgress);
        await PatchStatusAsync(completed.Id, ActivityStatus.Completed);
        await PatchStatusAsync(cancelled.Id, ActivityStatus.Cancelled);

        var result = await EvaluateAsync();

        Assert.Equal(new[] { pending.Id, inProgress.Id }.Order(), result.NewlyBreached.Order());
    }

    [Fact]
    public async Task Evaluation_breaches_only_after_due_time_passes()
    {
        var dueAt = At(9, 5);
        var early = await CreateAsync("High", dueAt);
        var later = await CreateAsync("High", At(10, 0));
        var undated = await CreateAsync("High", dueAt: null);

        Factory.Clock.SetUtcNow(dueAt.AddMilliseconds(-100));
        var first = await EvaluateAsync();
        Factory.Clock.SetUtcNow(dueAt.AddMilliseconds(100));
        var second = await EvaluateAsync();
        Factory.Clock.SetUtcNow(At(10, 0).AddMilliseconds(100));
        var third = await EvaluateAsync();

        Assert.Empty(first.NewlyBreached);
        Assert.Equal([early.Id], second.NewlyBreached);
        Assert.Equal([later.Id], third.NewlyBreached);
        Assert.Null(undated.DueAt);
    }

    [Fact]
    public async Task Repeated_evaluation_does_not_report_same_breach_twice()
    {
        var activity = await CreateAsync("High", WithinGraceDueAt);
        var earlier = await EvaluateAsync();
        Assert.Equal([activity.Id], earlier.NewlyBreached);

        var response = await Client.PostAsync(EvaluationsPath, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadAsync<SlaEvaluationResponse>();
        Assert.Empty(result.NewlyBreached);
        Assert.Empty(result.NewlyEscalated);
    }

    [Fact]
    public async Task Concurrent_evaluations_report_breach_exactly_once()
    {
        var activity = await CreateAsync("High", WithinGraceDueAt);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Client.PostAsync(EvaluationsPath, content: null)));

        Assert.Equal(Enumerable.Repeat(HttpStatusCode.OK, 10), responses.Select(r => r.StatusCode));
        var results = await Task.WhenAll(responses.Select(r => r.ReadAsync<SlaEvaluationResponse>()));
        Assert.Equal([activity.Id], results.SelectMany(r => r.NewlyBreached));
        Assert.Single(results, r => r.NewlyBreached.Contains(activity.Id));
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 10, 1, hour, minute, 0, TimeSpan.Zero);

    private async Task<ActivityResponse> CreateAsync(string priority, DateTimeOffset? dueAt)
    {
        var response = await Client.PostAsJsonAsync("/api/activities", new
        {
            title = $"{priority} SLA check",
            storeId = "STORE-001",
            priority,
            dueAt,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsync<ActivityResponse>();
        Assert.Equal(dueAt, created.DueAt);
        return created;
    }

    private async Task PatchStatusAsync(Guid id, ActivityStatus status)
    {
        var response = await Client.PatchAsJsonAsync($"/api/activities/{id}", new { status = status.ToString() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, (await response.ReadAsync<ActivityResponse>()).Status);
    }

    private async Task<SlaEvaluationResponse> EvaluateAsync()
    {
        var response = await Client.PostAsync(EvaluationsPath, content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SlaEvaluationResponse>();
    }
}
