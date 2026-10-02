using StoreOps.Api.Modules.FixtureAlpha;
using StoreOps.Api.Modules.FixtureBeta;
using StoreOps.Api.Shared.FixtureShared;

namespace StoreOps.Api.Tests.Architecture;

/// <summary>Proves each architecture rule fails on a known violation, so a passing rule is meaningful.</summary>
public sealed class ModuleRulesSelfTests
{
    private static readonly Type[] FixtureTypes = typeof(FixtureAlphaService).Assembly.GetTypes()
        .Where(t => t.Namespace?.StartsWith("StoreOps.Api.Modules.Fixture", StringComparison.Ordinal) == true
            || t.Namespace?.StartsWith("StoreOps.Api.Shared.Fixture", StringComparison.Ordinal) == true)
        .ToArray();

    [Fact]
    public void Detects_module_cycle()
    {
        var cycle = ModuleRules.FindCycle(FixtureTypes);

        Assert.NotNull(cycle);
        Assert.Contains("FixtureAlpha", cycle);
        Assert.Contains("FixtureBeta", cycle);
    }

    [Fact]
    public void Detects_layering_violations()
    {
        var violations = ModuleRules.LayeringViolations(FixtureTypes);

        Assert.Contains(violations, v => v.Contains(nameof(FixtureBetaController)) && v.Contains("service"));
        Assert.Contains(violations, v => v.Contains(nameof(FixtureBetaService)) && v.Contains("repository"));
        Assert.Contains(violations, v => v.Contains(nameof(IFixtureBetaRepository)) && v.Contains("outside"));
        Assert.Contains(violations, v => v.StartsWith("FixtureReadOnly: has no controller", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.StartsWith($"{typeof(FixtureAlphaService).FullName}:", StringComparison.Ordinal));
    }

    [Fact]
    public void Detects_cross_module_repository_reference()
    {
        var violations = ModuleRules.RepositoryViolations(FixtureTypes);

        Assert.Contains(violations, v => v.StartsWith(typeof(FixtureBetaService).FullName!, StringComparison.Ordinal));
    }

    [Fact]
    public void Detects_cross_module_non_lookup_dependency()
    {
        var violations = ModuleRules.CrossModuleDependencyViolations(FixtureTypes);

        Assert.Contains(violations, v => v == $"{typeof(FixtureBetaService).FullName} -> {typeof(IFixtureAlphaService).FullName}");
        Assert.DoesNotContain(violations, v => v.StartsWith($"{typeof(FixtureAlphaService).FullName} ->", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.StartsWith($"{typeof(FixtureBetaReport).FullName} ->", StringComparison.Ordinal));
    }

    [Fact]
    public void Detects_controller_depending_on_repository()
    {
        var violations = ModuleRules.ControllerViolations(FixtureTypes);

        Assert.Contains(violations, v => v.Contains(nameof(FixtureBetaController)));
    }

    [Fact]
    public void Detects_read_only_module_with_side_effect_dependencies()
    {
        var violations = ModuleRules.ReadOnlyModuleViolations(FixtureTypes, "FixtureReadOnly");

        Assert.Contains(violations, v => v.EndsWith("IEventBus", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.EndsWith(nameof(IFixtureAlphaService), StringComparison.Ordinal));
    }

    [Fact]
    public void Detects_lookup_with_write_operation()
    {
        var violations = ModuleRules.LookupViolations(FixtureTypes);

        Assert.Contains(violations, v => v.EndsWith(nameof(IFixtureBetaLookup.DeleteEverythingAsync), StringComparison.Ordinal));
    }

    [Fact]
    public void Detects_shared_depending_on_module()
    {
        var violations = ModuleRules.SharedViolations(FixtureTypes);

        Assert.Contains(violations, v => v.Contains(nameof(FixtureSharedThing)));
    }

    [Fact]
    public void Detects_import_of_another_modules_persistence_namespace()
    {
        SourceFile[] files =
        [
            new("Modules/Activities/Ok.cs", "Activities", "using StoreOps.Api.Modules.Activities.Persistence;"),
            new("Modules/Reports/Bad.cs", "Reports", "using StoreOps.Api.Modules.Activities.Persistence;"),
            new("Modules/Alerts/Qualified.cs", "Alerts", "StoreOps.Api.Modules.Staff.Persistence.IStaffRepository repo;"),
            new("Program.cs", null, "using StoreOps.Api.Modules.Programmes.Persistence;"),
        ];

        var violations = ModuleRules.PersistenceImportViolations(files);

        Assert.Equal(
            [
                "Modules/Reports/Bad.cs -> Activities.Persistence",
                "Modules/Alerts/Qualified.cs -> Staff.Persistence",
                "Program.cs -> Programmes.Persistence",
            ],
            violations);
    }
}
