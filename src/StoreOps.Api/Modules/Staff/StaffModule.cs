using StoreOps.Api.Modules.Staff.Persistence;

namespace StoreOps.Api.Modules.Staff;

public static class StaffModule
{
    public static IServiceCollection AddStaffModule(this IServiceCollection services)
    {
        services.AddSingleton<IStaffRepository, InMemoryStaffRepository>();
        services.AddScoped<StaffService>();
        services.AddScoped<IStaffService>(sp => sp.GetRequiredService<StaffService>());
        services.AddScoped<IStaffLookup>(sp => sp.GetRequiredService<StaffService>());
        return services;
    }
}
