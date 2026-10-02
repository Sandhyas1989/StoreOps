using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Activities.Persistence;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> ListAsync(CancellationToken cancellationToken);

    Task<Activity?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Activity activity, CancellationToken cancellationToken);

    Task UpdateAsync(Activity activity, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class InMemoryActivityRepository : IActivityRepository
{
    private readonly ConcurrentDictionary<Guid, Activity> _items = new();

    public Task<IReadOnlyList<Activity>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Activity>>(_items.Values.ToList());

    public Task<Activity?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var activity) ? activity : null);

    public Task AddAsync(Activity activity, CancellationToken cancellationToken)
    {
        _items[activity.Id] = activity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Activity activity, CancellationToken cancellationToken)
    {
        _items[activity.Id] = activity;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryRemove(id, out _));
}
