namespace StoreOps.Api.Modules.Alerts;

public sealed record RaiseAlertCommand(
    string Type,
    AlertSeverity Severity,
    string Message,
    string SourceModule,
    Guid? SourceId);

public sealed record AlertResponse(
    Guid Id,
    string Type,
    AlertSeverity Severity,
    string Message,
    string SourceModule,
    Guid? SourceId,
    DateTimeOffset CreatedAt)
{
    public static AlertResponse From(Alert alert) => new(
        alert.Id,
        alert.Type,
        alert.Severity,
        alert.Message,
        alert.SourceModule,
        alert.SourceId,
        alert.CreatedAt);
}
