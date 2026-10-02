using StoreOps.Api.Modules.Programmes.Persistence;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Programmes;

/// <summary>Read-only contract other modules may depend on for programme lookups.</summary>
public interface IProgrammeLookup
{
    Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken);

    Task<Programme?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public interface IProgrammeService
{
    Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken);

    Task<Programme> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Programme> CreateAsync(CreateProgrammeRequest request, CancellationToken cancellationToken);

    Task<Programme> AddMemberAsync(Guid id, AddProgrammeMemberRequest request, CancellationToken cancellationToken);
}

public sealed class ProgrammeService(
    IProgrammeRepository repository,
    IStaffLookup staff,
    IEventBus eventBus,
    TimeProvider clock) : IProgrammeService, IProgrammeLookup
{
    public const string NotFoundCode = "PROGRAMME_NOT_FOUND";
    public const string MemberExistsCode = "PROGRAMME_MEMBER_EXISTS";

    private const int MaxNameLength = 200;

    public Task<IReadOnlyList<Programme>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public Task<Programme?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public async Task<Programme> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new NotFoundError(NotFoundCode, $"Programme '{id}' was not found.");

    public async Task<Programme> CreateAsync(CreateProgrammeRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrorBuilder();
        var name = errors.RequireText("name", request.Name, "Name is required.");
        if (name.Length > MaxNameLength)
        {
            errors.Add("name", $"Name must be at most {MaxNameLength} characters.");
        }

        if (request is { StartDate: { } start, EndDate: { } end } && end < start)
        {
            errors.Add("endDate", "End date must be on or after the start date.");
        }

        errors.ThrowIfAny();

        var now = clock.GetUtcNow();
        var programme = new Programme
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MemberStaffIds = [],
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.AddAsync(programme, cancellationToken);
        return programme;
    }

    public async Task<Programme> AddMemberAsync(Guid id, AddProgrammeMemberRequest request, CancellationToken cancellationToken)
    {
        var programme = await GetAsync(id, cancellationToken);

        if (request.StaffId is not { } staffId)
        {
            throw StaffIdError("Staff id is required.");
        }

        if (await staff.FindAsync(staffId, cancellationToken) is null)
        {
            throw StaffIdError($"Staff member '{staffId}' does not exist.");
        }

        if (programme.MemberStaffIds.Contains(staffId))
        {
            throw new ConflictError(MemberExistsCode, $"Staff member '{staffId}' is already a member of programme '{id}'.");
        }

        var now = clock.GetUtcNow();
        var updated = programme with
        {
            MemberStaffIds = [.. programme.MemberStaffIds, staffId],
            UpdatedAt = now,
        };

        await repository.UpdateAsync(updated, cancellationToken);
        await eventBus.PublishAsync(new ProgrammeMemberAdded(id, staffId, now), cancellationToken);
        return updated;
    }

    private static ValidationError StaffIdError(string message) =>
        new("The request is invalid.", new Dictionary<string, string[]> { ["staffId"] = [message] });
}
