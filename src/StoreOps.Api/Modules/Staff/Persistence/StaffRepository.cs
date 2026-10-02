using System.Collections.Concurrent;

namespace StoreOps.Api.Modules.Staff.Persistence;

public interface IStaffRepository
{
    Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken);

    Task<StaffMember?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class InMemoryStaffRepository : IStaffRepository
{
    private readonly ConcurrentDictionary<Guid, StaffMember> _items = new(StaffSeed.All.ToDictionary(s => s.Id));

    public Task<IReadOnlyList<StaffMember>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StaffMember>>(_items.Values.OrderBy(s => s.Name).ToList());

    public Task<StaffMember?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var member) ? member : null);
}
