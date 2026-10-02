using StoreOps.Api.Modules.Staff.Persistence;
using StoreOps.Api.Shared.Errors;

namespace StoreOps.Api.Modules.Staff;

/// <summary>Read-only contract other modules may depend on for staff lookups.</summary>
public interface IStaffLookup
{
    Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken);

    Task<StaffMember?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public interface IStaffService
{
    Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken);

    Task<StaffMember> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class StaffService(IStaffRepository repository) : IStaffService, IStaffLookup
{
    public const string NotFoundCode = "STAFF_NOT_FOUND";

    public Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken) =>
        repository.ListAsync(cancellationToken);

    public Task<StaffMember?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public async Task<StaffMember> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new NotFoundError(NotFoundCode, $"Staff member '{id}' was not found.");
}
