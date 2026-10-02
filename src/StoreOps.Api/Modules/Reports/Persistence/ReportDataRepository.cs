using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Programmes;
using StoreOps.Api.Modules.Staff;

namespace StoreOps.Api.Modules.Reports.Persistence;

/// <summary>The data a report is computed from.</summary>
public sealed record ReportSourceData(
    IReadOnlyList<Activity> Activities,
    IReadOnlyList<Programme> Programmes,
    IReadOnlyList<StaffMember> StaffMembers);

/// <summary>
/// Read-only data access for reports. Reports own no data, so this repository reads through the read-only
/// *Lookup contracts of activities, programmes, and staff, and exposes no write operations.
/// </summary>
public interface IReportDataRepository
{
    Task<ReportSourceData> LoadAsync(CancellationToken cancellationToken);
}

public sealed class LookupReportDataRepository(
    IActivityLookup activities,
    IProgrammeLookup programmes,
    IStaffLookup staff) : IReportDataRepository
{
    public async Task<ReportSourceData> LoadAsync(CancellationToken cancellationToken) => new(
        await activities.ListAsync(cancellationToken),
        await programmes.ListAsync(cancellationToken),
        await staff.ListAsync(cancellationToken));
}
