using StoreOps.Api.Modules.Activities.Persistence;

namespace StoreOps.Api.Modules.Activities;

public static class ActivitiesModule
{
    public static IServiceCollection AddActivitiesModule(this IServiceCollection services)
    {
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddSingleton<ISlaRecordRepository, InMemorySlaRecordRepository>();
        services.AddScoped<ActivityService>();
        services.AddScoped<IActivityService>(sp => sp.GetRequiredService<ActivityService>());
        services.AddScoped<IActivityLookup>(sp => sp.GetRequiredService<ActivityService>());
        services.AddScoped<ISlaEvaluationService, SlaEvaluationService>();
        services.AddOptions<SlaOptions>()
            .BindConfiguration("Sla")
            .Validate(
                options => options.EscalationGracePeriod > TimeSpan.Zero,
                "Sla:EscalationGracePeriod must be greater than 00:00:00.")
            .ValidateOnStart();
        return services;
    }
}
