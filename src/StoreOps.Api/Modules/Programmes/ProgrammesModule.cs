using StoreOps.Api.Modules.Programmes.Persistence;

namespace StoreOps.Api.Modules.Programmes;

public static class ProgrammesModule
{
    public static IServiceCollection AddProgrammesModule(this IServiceCollection services)
    {
        services.AddSingleton<IProgrammeRepository, InMemoryProgrammeRepository>();
        services.AddScoped<ProgrammeService>();
        services.AddScoped<IProgrammeService>(sp => sp.GetRequiredService<ProgrammeService>());
        services.AddScoped<IProgrammeLookup>(sp => sp.GetRequiredService<ProgrammeService>());
        return services;
    }
}
