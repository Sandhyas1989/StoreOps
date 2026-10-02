namespace StoreOps.Api.Modules.Activities;

public enum ActivityStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled,
}

public enum ActivityPriority
{
    Low,
    Medium,
    High,
}

/// <summary>A unit of store work, such as a restock, price change, or compliance check.</summary>
public sealed record Activity
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required string StoreId { get; init; }

    public required ActivityStatus Status { get; init; }

    public required ActivityPriority Priority { get; init; }

    public Guid? AssignedStaffId { get; init; }

    public DateTimeOffset? DueAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public bool IsClosed => Status is ActivityStatus.Completed or ActivityStatus.Cancelled;
}
