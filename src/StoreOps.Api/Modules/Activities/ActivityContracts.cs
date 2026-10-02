namespace StoreOps.Api.Modules.Activities;

public sealed record CreateActivityRequest(
    string? Title,
    string? Description,
    string? StoreId,
    ActivityPriority? Priority,
    Guid? AssignedStaffId,
    DateTimeOffset? DueAt);

/// <summary>Partial update: omitted (null) fields are left unchanged.</summary>
public sealed record UpdateActivityRequest(
    string? Title,
    string? Description,
    ActivityStatus? Status,
    ActivityPriority? Priority,
    Guid? AssignedStaffId,
    DateTimeOffset? DueAt);

public sealed record ActivityFilter(ActivityStatus? Status = null, string? StoreId = null)
{
    public static ActivityFilter None { get; } = new();
}

public sealed record ActivityResponse(
    Guid Id,
    string Title,
    string? Description,
    string StoreId,
    ActivityStatus Status,
    ActivityPriority Priority,
    Guid? AssignedStaffId,
    DateTimeOffset? DueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ActivityResponse From(Activity activity) => new(
        activity.Id,
        activity.Title,
        activity.Description,
        activity.StoreId,
        activity.Status,
        activity.Priority,
        activity.AssignedStaffId,
        activity.DueAt,
        activity.CreatedAt,
        activity.UpdatedAt);
}
