namespace StoreOps.Api.Modules.Alerts.Persistence;

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the alert atomically and returns it. When the alert has both a source and a recipient and one already
    /// exists for the same (Type, SourceId, RecipientStaffId), nothing is stored and the existing alert is returned.
    /// </summary>
    Task<Alert> TryAddAsync(Alert alert, CancellationToken cancellationToken);
}

public sealed class InMemoryAlertRepository : IAlertRepository
{
    private readonly object _gate = new();
    private readonly List<Alert> _items = [];

    public Task<IReadOnlyList<Alert>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Alert>>(_items.ToList());
        }
    }

    public Task<Alert> TryAddAsync(Alert alert, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (alert.SourceId is not null && alert.RecipientStaffId is not null)
            {
                var existing = _items.FirstOrDefault(a =>
                    a.Type == alert.Type && a.SourceId == alert.SourceId && a.RecipientStaffId == alert.RecipientStaffId);
                if (existing is not null)
                {
                    return Task.FromResult(existing);
                }
            }

            _items.Add(alert);
            return Task.FromResult(alert);
        }
    }
}
