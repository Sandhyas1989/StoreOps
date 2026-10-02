using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Alerts.Persistence;

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Alert alert, CancellationToken cancellationToken);
}

public sealed class InMemoryAlertRepository : IAlertRepository
{
    private readonly ConcurrentDictionary<Guid, Alert> _items = new();

    public Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Alert>>(_items.Values.ToList());

    public Task AddAsync(Alert alert, CancellationToken cancellationToken)
    {
        _items[alert.Id] = alert;
        return Task.CompletedTask;
    }
}
