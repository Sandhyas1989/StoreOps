// Deliberately broken modules used only by ModuleRulesSelfTests to prove each rule detects violations.
// They live in the test assembly, so the real API rule checks (which scan only the API assembly) never see them.

namespace StoreOps.Api.Modules.FixtureAlpha.Persistence
{
    public interface IFixtureAlphaRepository;
}

namespace StoreOps.Api.Modules.FixtureAlpha
{
    using StoreOps.Api.Modules.FixtureAlpha.Persistence;
    using StoreOps.Api.Modules.FixtureBeta;

    public interface IFixtureAlphaService;

    public sealed record FixtureAlphaData(int Value);

    public sealed class FixtureAlphaController
    {
        public FixtureAlphaController(IFixtureAlphaService service)
        {
        }
    }

    // Valid on its own: own repository plus another module's lookup. Creates Alpha -> Beta.
    public sealed class FixtureAlphaService : IFixtureAlphaService
    {
        public FixtureAlphaService(IFixtureAlphaRepository repository, IFixtureBetaLookup beta)
        {
        }
    }
}

namespace StoreOps.Api.Modules.FixtureBeta
{
    using StoreOps.Api.Modules.FixtureAlpha;
    using StoreOps.Api.Modules.FixtureAlpha.Persistence;

    // Violation: a lookup exposing a write operation.
    public interface IFixtureBetaLookup
    {
        Task DeleteEverythingAsync();
    }

    // Violations: controller depends on a repository; it does not use a Beta service.
    public sealed class FixtureBetaController
    {
        public FixtureBetaController(IFixtureAlphaRepository repository)
        {
        }
    }

    // Violations: uses another module's repository and non-lookup service (Beta -> Alpha closes a cycle),
    // and has no repository of its own.
    public sealed class FixtureBetaService
    {
        public FixtureBetaService(IFixtureAlphaRepository repository, IFixtureAlphaService alpha)
        {
        }
    }

    // Allowed: another module's data contract in a record constructor is not an injected dependency.
    public sealed record FixtureBetaReport(FixtureAlphaData Data);

    // Violation: repository outside the Persistence namespace.
    public interface IFixtureBetaRepository;
}

namespace StoreOps.Api.Modules.FixtureReadOnly
{
    using StoreOps.Api.Modules.FixtureAlpha;
    using StoreOps.Api.Shared.Events;

    // Violations for a read-only module: publishes events and uses a non-lookup service.
    public sealed class FixtureReadOnlyService
    {
        public FixtureReadOnlyService(IEventBus eventBus, IFixtureAlphaService alpha)
        {
        }
    }
}

namespace StoreOps.Api.Shared.FixtureShared
{
    using StoreOps.Api.Modules.FixtureAlpha;

    // Violation: shared infrastructure depending on a module.
    public sealed class FixtureSharedThing
    {
        public FixtureSharedThing(IFixtureAlphaService alpha)
        {
        }
    }
}
