using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Reports.Persistence;

namespace StoreOps.Api.Modules.Reports;

public interface IReportService
{
    Task<StoreSummaryReport> GetStoreSummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Read-only aggregation over data loaded by <see cref="IReportDataRepository"/>.</summary>
public sealed class ReportService(IReportDataRepository repository, TimeProvider clock) : IReportService
{
    public async Task<StoreSummaryReport> GetStoreSummaryAsync(CancellationToken cancellationToken)
    {
        var data = await repository.LoadAsync(cancellationToken);

        var byStatus = Enum.GetValues<ActivityStatus>()
            .ToDictionary(status => status.ToString(), status => data.Activities.Count(a => a.Status == status));

        return new StoreSummaryReport(
            clock.GetUtcNow(),
            data.Activities.Count,
            byStatus,
            data.Activities.Count(a => a is { IsClosed: false, Priority: ActivityPriority.High }),
            data.Programmes.Count,
            data.Programmes.Sum(p => p.MemberStaffIds.Count),
            data.StaffMembers.Count);
    }
}
