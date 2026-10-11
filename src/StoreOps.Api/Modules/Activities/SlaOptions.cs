namespace StoreOps.Api.Modules.Activities;

/// <summary>SLA settings bound from the "Sla" configuration section.</summary>
public sealed class SlaOptions
{
    /// <summary>How long after an activity's due time an open breach is escalated.</summary>
    public TimeSpan EscalationGracePeriod { get; set; } = TimeSpan.FromMinutes(30);
}
