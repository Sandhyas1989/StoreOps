using Microsoft.Extensions.Options;
using StoreOps.Api.Modules.Activities.Persistence;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Activities;

public interface ISlaEvaluationService
{
    Task<SlaEvaluationResponse> EvaluateAsync(CancellationToken cancellationToken);
}

public sealed class SlaEvaluationService(
    IActivityRepository activities,
    ISlaRecordRepository slaRecords,
    IEventBus eventBus,
    IOptions<SlaOptions> options,
    TimeProvider clock) : ISlaEvaluationService
{
    public async Task<SlaEvaluationResponse> EvaluateAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var all = await activities.ListAsync(cancellationToken);

        var gracePeriod = options.Value.EscalationGracePeriod;

        var newlyBreached = new List<Guid>();
        var newlyEscalated = new List<Guid>();
        foreach (var activity in all.Where(a => IsInBreach(a, now)).OrderBy(a => a.CreatedAt))
        {
            if (await slaRecords.TryAdvanceStageAsync(activity.Id, SlaStage.Breach, now, cancellationToken))
            {
                newlyBreached.Add(activity.Id);
                await PublishAsync(activity, SlaStage.Breach, now, cancellationToken);
            }

            if (now >= activity.DueAt.GetValueOrDefault() + gracePeriod
                && await slaRecords.TryAdvanceStageAsync(activity.Id, SlaStage.Escalation, now, cancellationToken))
            {
                newlyEscalated.Add(activity.Id);
                await PublishAsync(activity, SlaStage.Escalation, now, cancellationToken);
            }
        }

        return new SlaEvaluationResponse(now, newlyBreached, newlyEscalated);
    }

    private Task PublishAsync(Activity activity, SlaStage stage, DateTimeOffset now, CancellationToken cancellationToken) =>
        eventBus.PublishAsync(
            new ActivitySlaBreached(
                activity.Id,
                activity.Title,
                activity.StoreId,
                activity.Priority,
                activity.DueAt.GetValueOrDefault(),
                activity.AssignedStaffId,
                stage,
                now),
            cancellationToken);

    private static bool IsInBreach(Activity activity, DateTimeOffset now) =>
        activity is { Priority: ActivityPriority.High or ActivityPriority.Critical, Status: ActivityStatus.Pending or ActivityStatus.InProgress, DueAt: { } dueAt }
        && now > dueAt;
}
