using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Programmes.Persistence;

public interface IProgrammeRepository
{
    Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken);

    Task<Programme?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Programme programme, CancellationToken cancellationToken);

    Task UpdateAsync(Programme programme, CancellationToken cancellationToken);
}

public sealed class InMemoryProgrammeRepository : IProgrammeRepository
{
    private readonly ConcurrentDictionary<Guid, Programme> _items = new();

    public Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Programme>>(_items.Values.OrderBy(p => p.CreatedAt).ToList());

    public Task<Programme?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var programme) ? programme : null);

    public Task AddAsync(Programme programme, CancellationToken cancellationToken)
    {
        _items[programme.Id] = programme;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Programme programme, CancellationToken cancellationToken)
    {
        _items[programme.Id] = programme;
        return Task.CompletedTask;
    }
}
