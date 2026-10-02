using StoreOps.Api.Modules.Reports.Persistence;

namespace StoreOps.Api.Modules.Reports;

public static class ReportsModule
{
    public static IServiceCollection AddReportsModule(this IServiceCollection services)
    {
        services.AddScoped<IReportDataRepository, LookupReportDataRepository>();
        services.AddScoped<IReportService, ReportService>();
        return services;
    }
}
