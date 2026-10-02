namespace StoreOps.Api.Tests.Architecture;

/// <summary>Applies <see cref="ModuleRules"/> to the real API assembly and source.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Type[] ApiTypes = typeof(Program).Assembly.GetTypes();

    [Fact]
    public void Module_dependency_graph_has_no_cycles() =>
        Assert.Null(ModuleRules.FindCycle(ApiTypes));

    [Fact]
    public void Every_module_follows_controller_service_repository() =>
        Assert.Empty(ModuleRules.LayeringViolations(ApiTypes));

    [Fact]
    public void No_module_references_another_modules_repository() =>
        Assert.Empty(ModuleRules.RepositoryViolations(ApiTypes));

    [Fact]
    public void No_source_file_imports_another_modules_persistence_namespace()
    {
        var files = ModuleRules.LoadApiSourceFiles();
        Assert.Contains(files, f => f.Module == "Activities");

        Assert.Empty(ModuleRules.PersistenceImportViolations(files));
    }

    [Fact]
    public void Cross_module_constructor_dependencies_are_read_only_lookups() =>
        Assert.Empty(ModuleRules.CrossModuleDependencyViolations(ApiTypes));

    [Fact]
    public void Controllers_depend_on_services_not_repositories() =>
        Assert.Empty(ModuleRules.ControllerViolations(ApiTypes));

    [Fact]
    public void Reports_module_is_read_only_toward_other_modules()
    {
        Assert.Contains(ApiTypes, t => ModuleRules.ModuleOf(t) == "Reports");

        Assert.Empty(ModuleRules.ReadOnlyModuleViolations(ApiTypes, "Reports"));
    }

    [Fact]
    public void Lookup_contracts_expose_only_read_operations()
    {
        Assert.Contains(ApiTypes, ModuleRules.IsLookup);

        Assert.Empty(ModuleRules.LookupViolations(ApiTypes));
    }

    [Fact]
    public void Shared_infrastructure_does_not_depend_on_modules() =>
        Assert.Empty(ModuleRules.SharedViolations(ApiTypes));
}
