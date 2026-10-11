namespace StoreOps.Api.Modules.Activities;

/// <summary>SLA stages an activity can reach; each stage is reached at most once per activity.</summary>
public enum SlaStage
{
    Breach,
    Escalation,
}

/// <summary>Result of one SLA evaluation: the activity ids whose stage advanced in this call.</summary>
public sealed record SlaEvaluationResponse(
    DateTimeOffset EvaluatedAt,
    IReadOnlyList<Guid> NewlyBreached,
    IReadOnlyList<Guid> NewlyEscalated);
