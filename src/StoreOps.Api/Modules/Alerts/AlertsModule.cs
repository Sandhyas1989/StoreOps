using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Alerts.Persistence;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Alerts;

public static class AlertsModule
{
    public static IServiceCollection AddAlertsModule(this IServiceCollection services)
    {
        services.AddSingleton<IAlertRepository, InMemoryAlertRepository>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IEventHandler<ActivityCreated>, HighPriorityActivityAlertHandler>();
        services.AddScoped<IEventHandler<ActivitySlaBreached>, SlaBreachAlertHandler>();
        return services;
    }
}
