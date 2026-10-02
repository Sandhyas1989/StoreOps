using StoreOps.Api.Modules.Activities.Persistence;

namespace StoreOps.Api.Modules.Activities;

public static class ActivitiesModule
{
    public static IServiceCollection AddActivitiesModule(this IServiceCollection services)
    {
        services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
        services.AddScoped<ActivityService>();
        services.AddScoped<IActivityService>(sp => sp.GetRequiredService<ActivityService>());
        services.AddScoped<IActivityLookup>(sp => sp.GetRequiredService<ActivityService>());
        return services;
    }
}
