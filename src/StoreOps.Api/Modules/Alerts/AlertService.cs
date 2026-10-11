using StoreOps.Api.Modules.Alerts.Persistence;

namespace StoreOps.Api.Modules.Alerts;

public interface IAlertService
{
    Task<IReadOnlyList<Alert>> ListAsync(AlertSeverity? severity, Guid? recipientStaffId, CancellationToken cancellationToken);

    /// <summary>Raises an alert; a repeat for the same (Type, SourceId, RecipientStaffId) returns the stored alert.</summary>
    Task<Alert> RaiseAsync(RaiseAlertCommand command, CancellationToken cancellationToken);
}

public sealed class AlertService(IAlertRepository repository, TimeProvider clock) : IAlertService
{
    public async Task<IReadOnlyList<Alert>> ListAsync(AlertSeverity? severity, Guid? recipientStaffId, CancellationToken cancellationToken)
    {
        var alerts = await repository.ListAsync(cancellationToken);
        return alerts
            .Where(a => severity is null || a.Severity == severity)
            .Where(a => recipientStaffId is null || a.RecipientStaffId == recipientStaffId)
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
            RecipientStaffId = command.RecipientStaffId,
            RecipientRole = command.RecipientRole,
            CreatedAt = clock.GetUtcNow(),
        };

        return await repository.TryAddAsync(alert, cancellationToken);
    }
}
