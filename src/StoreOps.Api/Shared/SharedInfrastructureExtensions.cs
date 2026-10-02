using Microsoft.Extensions.DependencyInjection.Extensions;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Shared;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IEventBus, InMemoryEventBus>();
        services.AddExceptionHandler<AppErrorExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
