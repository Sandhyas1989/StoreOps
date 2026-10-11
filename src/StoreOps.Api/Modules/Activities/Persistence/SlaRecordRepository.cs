using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities.Persistence;

public interface ISlaRecordRepository
{
    /// <summary>Records that the activity reached the stage. Returns false if it had already reached it.</summary>
    Task<bool> TryAdvanceStageAsync(Guid activityId, SlaStage stage, DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed class InMemorySlaRecordRepository : ISlaRecordRepository
{
    private readonly ConcurrentDictionary<(Guid ActivityId, SlaStage Stage), DateTimeOffset> _stages = new();

    public Task<bool> TryAdvanceStageAsync(Guid activityId, SlaStage stage, DateTimeOffset at, CancellationToken cancellationToken) =>
        Task.FromResult(_stages.TryAdd((activityId, stage), at));
}
