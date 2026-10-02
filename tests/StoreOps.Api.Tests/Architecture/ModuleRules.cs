using System.Reflection;
using System.Text.RegularExpressions;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Tests.Architecture;

public sealed record SourceFile(string RelativePath, string? Module, string Content);

/// <summary>
/// Module rules expressed as functions that return violations, so they can run against the real API and against
/// deliberately broken fixtures. Type dependencies are read from signatures (base types, interfaces, fields,
/// properties, constructor and method signatures, including compiler-generated closures and async state
/// machines). Persistence imports are also checked in source, because <c>using</c> directives are not visible
/// to reflection.
/// </summary>
public static partial class ModuleRules
{
    public const string ModulesNamespace = "StoreOps.Api.Modules.";
    public const string SharedNamespace = "StoreOps.Api.Shared";
    public const string PersistenceSegment = "Persistence";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Returns a description of the first module cycle found, or null when the graph is acyclic.</summary>
    public static string? FindCycle(IEnumerable<Type> types)
    {
        var graph = ModuleTypes(types)
            .GroupBy(t => ModuleOf(t)!)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(ReferencedTypes).Select(ModuleOf).OfType<string>().Where(m => m != g.Key).ToHashSet());

        var visited = new HashSet<string>();
        var path = new List<string>();

        string? Visit(string module)
        {
            if (path.Contains(module))
            {
                return string.Join(" -> ", path.SkipWhile(m => m != module).Append(module));
            }

            if (!visited.Add(module))
            {
                return null;
            }

            path.Add(module);
            foreach (var dependency in graph.TryGetValue(module, out var deps) ? deps : [])
            {
                if (Visit(dependency) is { } cycle)
                {
                    return cycle;
                }
            }

            path.RemoveAt(path.Count - 1);
            return null;
        }

        return graph.Keys.Select(Visit).FirstOrDefault(cycle => cycle is not null);
    }

    /// <summary>Every module has a controller that uses its own service, and a service that uses its own repository.</summary>
    public static IReadOnlyList<string> LayeringViolations(IEnumerable<Type> types)
    {
        var violations = new List<string>();
        foreach (var module in ModuleTypes(types).Where(t => !t.IsNested).GroupBy(t => ModuleOf(t)!))
        {
            var controllers = module.Where(t => IsConcreteClass(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal)).ToList();
            var services = module.Where(t => IsConcreteClass(t) && t.Name.EndsWith("Service", StringComparison.Ordinal)).ToList();

            if (controllers.Count == 0)
            {
                violations.Add($"{module.Key}: has no controller");
            }

            if (services.Count == 0)
            {
                violations.Add($"{module.Key}: has no service");
            }

            violations.AddRange(controllers
                .Where(c => !ConstructorDependencies(c).Any(d => d.IsInterface && ModuleOf(d) == module.Key && d.Name.EndsWith("Service", StringComparison.Ordinal)))
                .Select(c => $"{c.FullName}: does not depend on a {module.Key} service"));

            violations.AddRange(services
                .Where(s => !ConstructorDependencies(s).Any(d => d.IsInterface && ModuleOf(d) == module.Key && IsPersistence(d) && d.Name.EndsWith("Repository", StringComparison.Ordinal)))
                .Select(s => $"{s.FullName}: does not depend on a {module.Key}.{PersistenceSegment} repository"));

            violations.AddRange(module
                .Where(t => t.Name.Contains("Repository") && !IsPersistence(t))
                .Select(t => $"{t.FullName}: repository is outside {module.Key}.{PersistenceSegment}"));
        }

        return violations;
    }

    public static IReadOnlyList<string> RepositoryViolations(IEnumerable<Type> types) =>
        ModuleTypes(types)
            .SelectMany(t => ReferencedTypes(t)
                .Where(r => IsOtherModule(t, r) && (r.Name.Contains("Repository") || IsPersistence(r)))
                .Select(r => $"{t.FullName} -> {r.FullName}"))
            .Distinct()
            .ToList();

    /// <summary>
    /// Injected cross-module dependencies must be *Lookup contracts. Another module's data contracts (records,
    /// enums) may still appear in constructors, e.g. in report data records.
    /// </summary>
    public static IReadOnlyList<string> CrossModuleDependencyViolations(IEnumerable<Type> types) =>
        ModuleTypes(types)
            .SelectMany(t => ConstructorDependencies(t)
                .Where(d => IsOtherModule(t, d) && !IsLookup(d) && (d.IsInterface || IsComponentName(d)))
                .Select(d => $"{t.FullName} -> {d.FullName}"))
            .Distinct()
            .ToList();

    public static IReadOnlyList<string> ControllerViolations(IEnumerable<Type> types) =>
        types
            .Where(t => t.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(t => ConstructorDependencies(t)
                .Where(d => d.Name.Contains("Repository"))
                .Select(d => $"{t.FullName} -> {d.FullName}"))
            .ToList();

    /// <summary>A read-only module may not publish events, and may use other modules only through *Lookup contracts.</summary>
    public static IReadOnlyList<string> ReadOnlyModuleViolations(IEnumerable<Type> types, string module) =>
        ModuleTypes(types)
            .Where(t => ModuleOf(t) == module)
            .SelectMany(t => ReferencedTypes(t)
                .Where(r => r == typeof(IEventBus)
                    || (IsOtherModule(t, r) && r.IsInterface && !IsLookup(r))
                    || (IsOtherModule(t, r) && (r.Name.EndsWith("Service", StringComparison.Ordinal) || r.Name.EndsWith("Controller", StringComparison.Ordinal))))
                .Select(r => $"{t.FullName} -> {r.FullName}"))
            .Distinct()
            .ToList();

    public static IReadOnlyList<string> LookupViolations(IEnumerable<Type> types) =>
        ModuleTypes(types)
            .Where(IsLookup)
            .SelectMany(l => l.GetMethods())
            .Where(m => !(m.Name.StartsWith("Find", StringComparison.Ordinal)
                || m.Name.StartsWith("List", StringComparison.Ordinal)
                || m.Name.StartsWith("Get", StringComparison.Ordinal)))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .ToList();

    public static IReadOnlyList<string> SharedViolations(IEnumerable<Type> types) =>
        types
            .Where(t => t.Namespace?.StartsWith(SharedNamespace, StringComparison.Ordinal) == true)
            .SelectMany(t => ReferencedTypes(t)
                .Where(r => ModuleOf(r) is not null)
                .Select(r => $"{t.FullName} -> {r.FullName}"))
            .Distinct()
            .ToList();

    /// <summary>No source file may import or name another module's Persistence namespace.</summary>
    public static IReadOnlyList<string> PersistenceImportViolations(IEnumerable<SourceFile> files) =>
        files
            .SelectMany(f => PersistenceReference().Matches(f.Content)
                .Select(m => m.Groups["module"].Value)
                .Where(target => target != f.Module)
                .Distinct()
                .Select(target => $"{f.RelativePath} -> {target}.{PersistenceSegment}"))
            .ToList();

    public static IReadOnlyList<SourceFile> LoadApiSourceFiles()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "StoreOps.sln")))
        {
            root = root.Parent;
        }

        if (root is null)
        {
            throw new DirectoryNotFoundException("Could not locate StoreOps.sln above the test output directory.");
        }

        var apiRoot = Path.Combine(root.FullName, "src", "StoreOps.Api");
        return Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(apiRoot, path).Replace('\\', '/'))
            .Where(relative => !relative.StartsWith("obj/", StringComparison.Ordinal) && !relative.StartsWith("bin/", StringComparison.Ordinal))
            .Select(relative => new SourceFile(
                relative,
                relative.StartsWith("Modules/", StringComparison.Ordinal) ? relative.Split('/')[1] : null,
                File.ReadAllText(Path.Combine(apiRoot, relative))))
            .ToList();
    }

    public static string? ModuleOf(Type type)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ModulesNamespace, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[ModulesNamespace.Length..];
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest[..dot];
    }

    public static bool IsLookup(Type type) =>
        type.IsInterface && type.Name.EndsWith("Lookup", StringComparison.Ordinal);

    [GeneratedRegex(@"StoreOps\.Api\.Modules\.(?<module>\w+)\.Persistence\b")]
    private static partial Regex PersistenceReference();

    private static IEnumerable<Type> ModuleTypes(IEnumerable<Type> types) => types.Where(t => ModuleOf(t) is not null);

    private static bool IsConcreteClass(Type type) => type is { IsClass: true, IsAbstract: false };

    private static bool IsComponentName(Type type) =>
        new[] { "Service", "Repository", "Controller", "Handler" }.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal));

    private static bool IsPersistence(Type type) =>
        ModuleOf(type) is { } module
        && type.Namespace is { } ns
        && (ns == $"{ModulesNamespace}{module}.{PersistenceSegment}"
            || ns.StartsWith($"{ModulesNamespace}{module}.{PersistenceSegment}.", StringComparison.Ordinal));

    private static bool IsOtherModule(Type owner, Type dependency) =>
        ModuleOf(dependency) is { } module && module != ModuleOf(owner);

    private static IEnumerable<Type> ConstructorDependencies(Type type) =>
        type.GetConstructors(Declared)
            .SelectMany(c => c.GetParameters())
            .SelectMany(p => Expand(p.ParameterType))
            .Distinct();

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var direct = new List<Type>();
        if (type.BaseType is not null)
        {
            direct.Add(type.BaseType);
        }

        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetFields(Declared).Select(f => f.FieldType));
        direct.AddRange(type.GetProperties(Declared).Select(p => p.PropertyType));
        direct.AddRange(type.GetConstructors(Declared).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType));
        foreach (var method in type.GetMethods(Declared))
        {
            direct.Add(method.ReturnType);
            direct.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }

        return direct.SelectMany(Expand).Distinct();
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.IsGenericParameter)
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (var element in Expand(type.GetElementType()!))
            {
                yield return element;
            }

            yield break;
        }

        if (!type.IsGenericType)
        {
            yield return type;
            yield break;
        }

        yield return type.GetGenericTypeDefinition();
        foreach (var argument in type.GetGenericArguments().SelectMany(Expand))
        {
            yield return argument;
        }
    }
}
