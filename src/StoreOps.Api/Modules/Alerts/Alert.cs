using StoreOps.Api.Modules.Staff;

namespace StoreOps.Api.Modules.Alerts;

public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}

public sealed record Alert
{
    public required Guid Id { get; init; }

    /// <summary>Machine-readable alert type, e.g. <c>HIGH_PRIORITY_ACTIVITY</c>.</summary>
    public required string Type { get; init; }

    public required AlertSeverity Severity { get; init; }

    public required string Message { get; init; }

    public required string SourceModule { get; init; }

    public Guid? SourceId { get; init; }

    public Guid? RecipientStaffId { get; init; }

    public StaffRole? RecipientRole { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public static class AlertTypes
{
    public const string HighPriorityActivity = "HIGH_PRIORITY_ACTIVITY";

    public const string SlaBreach = "SLA_BREACH";

    public const string SlaEscalation = "SLA_ESCALATION";
}
