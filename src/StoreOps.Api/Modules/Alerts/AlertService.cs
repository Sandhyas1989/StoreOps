using StoreOps.Api.Modules.Alerts.Persistence;

namespace StoreOps.Api.Modules.Alerts;

public interface IAlertService
{
    Task<IReadOnlyList<Alert>> ListAsync(AlertSeverity? severity, CancellationToken cancellationToken);

    Task<Alert> RaiseAsync(RaiseAlertCommand command, CancellationToken cancellationToken);
}

public sealed class AlertService(IAlertRepository repository, TimeProvider clock) : IAlertService
{
    public async Task<IReadOnlyList<Alert>> ListAsync(AlertSeverity? severity, CancellationToken cancellationToken)
    {
        var alerts = await repository.ListAsync(cancellationToken);
        return alerts
            .Where(a => severity is null || a.Severity == severity)
            .OrderByDescending(a => a.CreatedAt)
            .ToList();
    }

    public async Task<Alert> RaiseAsync(RaiseAlertCommand command, CancellationToken cancellationToken)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Type = command.Type,
            Severity = command.Severity,
            Message = command.Message,
            SourceModule = command.SourceModule,
            SourceId = command.SourceId,
            CreatedAt = clock.GetUtcNow(),
        };

        await repository.AddAsync(alert, cancellationToken);
        return alert;
    }
}
