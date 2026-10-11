using StoreOps.Api.Modules.Activities.Persistence;

namespace StoreOps.Api.Modules.Activities;

public interface ISlaEvaluationService
{
    Task<SlaEvaluationResponse> EvaluateAsync(CancellationToken cancellationToken);
}

public sealed class SlaEvaluationService(
    IActivityRepository activities,
    ISlaRecordRepository slaRecords,
    TimeProvider clock) : ISlaEvaluationService
{
    public async Task<SlaEvaluationResponse> EvaluateAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var all = await activities.ListAsync(cancellationToken);

        var newlyBreached = new List<Guid>();
        foreach (var activity in all.Where(a => IsInBreach(a, now)).OrderBy(a => a.CreatedAt))
        {
            if (await slaRecords.TryAdvanceStageAsync(activity.Id, SlaStage.Breach, now, cancellationToken))
            {
                newlyBreached.Add(activity.Id);
            }
        }

        return new SlaEvaluationResponse(now, newlyBreached, []);
    }

    private static bool IsInBreach(Activity activity, DateTimeOffset now) =>
        activity is { Priority: ActivityPriority.High or ActivityPriority.Critical, Status: ActivityStatus.Pending or ActivityStatus.InProgress, DueAt: { } dueAt }
        && now > dueAt;
}
