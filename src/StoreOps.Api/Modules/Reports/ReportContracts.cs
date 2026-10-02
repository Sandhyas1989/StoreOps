namespace StoreOps.Api.Modules.Reports;

public sealed record StoreSummaryReport(
    DateTimeOffset GeneratedAt,
    int TotalActivities,
    IReadOnlyDictionary<string, int> ActivitiesByStatus,
    int OpenHighPriorityActivities,
    int TotalProgrammes,
    int TotalProgrammeMemberships,
    int TotalStaff);
