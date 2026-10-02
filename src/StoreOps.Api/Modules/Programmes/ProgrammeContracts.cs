namespace StoreOps.Api.Modules.Programmes;

public sealed record CreateProgrammeRequest(
    string? Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record AddProgrammeMemberRequest(Guid? StaffId);

public sealed record ProgrammeResponse(
    Guid Id,
    string Name,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<Guid> MemberStaffIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ProgrammeResponse From(Programme programme) => new(
        programme.Id,
        programme.Name,
        programme.Description,
        programme.StartDate,
        programme.EndDate,
        programme.MemberStaffIds,
        programme.CreatedAt,
        programme.UpdatedAt);
}
