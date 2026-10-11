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
            .Validate(
                options => options.EvaluationInterval > TimeSpan.Zero,
                "Sla:EvaluationInterval must be greater than 00:00:00.")
            .ValidateOnStart();

        if (IsWorkerEnabled(services))
            services.AddHostedService<SlaEvaluationWorker>();

        return services;
    }

    /// <summary>
    /// The worker is registered only when <c>Sla:WorkerEnabled</c> is true (default). The decision is needed while services are
    /// being registered, before the host is built. The host builder registers its configuration as a factory that ignores its
    /// argument, so the factory is invoked with an empty provider to read the flag.
    /// </summary>
    private static bool IsWorkerEnabled(IServiceCollection services)
    {
        var registration = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IConfiguration));
        using var empty = new ServiceCollection().BuildServiceProvider();
        var configuration = registration?.ImplementationInstance ?? registration?.ImplementationFactory?.Invoke(empty);
        return (configuration as IConfiguration)?.GetValue("Sla:WorkerEnabled", true) ?? true;
    }
}
