using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Activities;

public sealed record ActivityCreated(
    Guid ActivityId,
    string Title,
    string StoreId,
    ActivityPriority Priority,
    DateTimeOffset OccurredAt) : IIntegrationEvent;

public sealed record ActivityDeleted(Guid ActivityId, DateTimeOffset OccurredAt) : IIntegrationEvent;

public sealed record ActivitySlaBreached(
    Guid ActivityId,
    string Title,
    string StoreId,
    ActivityPriority Priority,
    DateTimeOffset DueAt,
    Guid? AssignedStaffId,
    SlaStage Stage,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
