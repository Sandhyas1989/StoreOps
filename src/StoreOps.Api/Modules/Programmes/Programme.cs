namespace StoreOps.Api.Modules.Programmes;

/// <summary>A store initiative (e.g. seasonal reset, training programme) that staff are enrolled in.</summary>
public sealed record Programme
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public required IReadOnlyList<Guid> MemberStaffIds { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
