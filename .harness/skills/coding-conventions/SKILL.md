---
name: coding-conventions
description: C# 12 / .NET 8 conventions used in StoreOps (naming, file layout, nullable references, async/await, dependency injection, immutability, DTO validation), with real examples from src/StoreOps.Api. Load for any sprint that changes C# code.
---

# StoreOps coding conventions

Match the existing code. When this file and the code disagree, follow the code and report the difference in the summary's known gaps.

## Files and naming
- **One file-scoped namespace per file**, matching the folder: `namespace StoreOps.Api.Modules.Activities;`, and `…Activities.Persistence;` for repositories.
- **Types are `sealed`** unless they're designed for inheritance (only `AppError` is abstract).
- **Name patterns:**

  | Kind | Pattern | Example |
  |---|---|---|
  | Entity | singular noun, `sealed record` | `Activity`, `Programme`, `StaffMember`, `Alert` |
  | Request DTO | `<Verb><Entity>Request` | `CreateActivityRequest`, `UpdateActivityRequest`, `AddProgrammeMemberRequest` |
  | Response DTO | `<Entity>Response` with `static From(entity)` | `ActivityResponse.From(activity)` |
  | Service | `I<Name>Service` and `<Name>Service` | `IActivityService` / `ActivityService` |
  | Lookup | `I<Name>Lookup`, implemented by the service | `IActivityLookup` |
  | Repository | `I<Name>Repository` and `InMemory<Name>Repository` | `IActivityRepository` / `InMemoryActivityRepository` |
  | Event | past-tense record | `ActivityCreated`, `ProgrammeMemberAdded` |
  | Handler | `<Purpose>Handler` | `HighPriorityActivityAlertHandler` |
  | DI registration | `<Module>Module.Add<Module>Module` | `ActivitiesModule.AddActivitiesModule` |
  | Error code | `public const string <Name>Code` on the owning service, value `SCREAMING_SNAKE` | `ActivityService.ClosedCode = "ACTIVITY_CLOSED"` |

- **Naming:**
  - Private fields are `_camelCase` (`_items`, `_errors`). Constants are `PascalCase` (`MaxTitleLength`).
  - Service and repository methods end in `Async` (`CreateAsync`, `TryAdvanceStageAsync`). Controller actions don't (`List`, `GetById`, `Create`, `Update`, `Delete`, `AddMember`).
- **Grouping in one file:**
  - A file may hold an interface plus its single implementation (`ActivityRepository.cs`, `ActivityService.cs`).
  - Related DTOs share `<Entity>Contracts.cs`; events share `<Entity>Events.cs`.
- **Usings:** sorted, `System.*` first, no unused usings. Implicit usings are on, so don't add `using System;` and similar.
- **Comments:** XML `/// <summary>` only where intent isn't obvious (see `AppError`, `InMemoryEventBus`). No commented-out code.

## Nullable references
- `Nullable` is enabled and nullable warnings are errors (`Directory.Build.props`). The build must report 0 warnings.
- **No `!` null-forgiving operator in `src/`.** Use a checked value instead:
  ```csharp
  var title = errors.RequireText("title", request.Title, "Title is required.");   // returns string, never null
  ```
- Mark optional values `?` (`string? Description`, `Guid? AssignedStaffId`). Lookups return `T?` (`FindAsync`). Services return non-null `T`, or throw `NotFoundError` (`GetAsync`).
- Use pattern matching for null checks: `if (staffId is { } id && …)`, `request is { StartDate: { } start, EndDate: { } end }`.

## async / await
- Every I/O-shaped method returns `Task` or `Task<T>`, even when it's in memory (`Task.FromResult`, `Task.CompletedTask`), so storage can be swapped later.
- `CancellationToken cancellationToken` is the **last** parameter and is passed to every async call. Controllers take it as an action parameter.
- Never use `.Result`, `.Wait()` or `async void`. Don't call `ConfigureAwait`; the code base doesn't use it.
- Expression-bodied async pass-throughs are fine:
  ```csharp
  public async Task<Activity> GetAsync(Guid id, CancellationToken cancellationToken) =>
      await repository.GetAsync(id, cancellationToken) ?? throw NotFound(id);
  ```

## Dependency injection
- **Use primary constructors** for injection (`public sealed class ActivityService(IActivityRepository repository, IStaffLookup staff, IEventBus eventBus, TimeProvider clock)`). Don't copy the parameters into fields.
- **Inject interfaces.** The only concrete types injected are `TimeProvider` and `ILogger<T>`.
- **Lifetimes:**
  - Repositories: `AddSingleton`, because they hold the in-memory state.
  - Services, lookups and handlers: `AddScoped`.
  - `IEventBus`: scoped (registered in `SharedInfrastructureExtensions`).
- **A service that also implements a lookup** is registered once and exposed through both interfaces, so both resolve to the same instance:
  ```csharp
  services.AddScoped<ActivityService>();
  services.AddScoped<IActivityService>(sp => sp.GetRequiredService<ActivityService>());
  services.AddScoped<IActivityLookup>(sp => sp.GetRequiredService<ActivityService>());
  ```
- **Time:** never call `DateTimeOffset.UtcNow`. Inject `TimeProvider` and call `clock.GetUtcNow()`. It's registered with `TryAddSingleton(TimeProvider.System)` so tests can replace it.
- **Registration lives only in `<Module>Module.cs`.** `Program.cs` only calls `Add<Module>Module()`.

## Immutability
- **Entities are `sealed record`s with `required … { get; init; }` properties.** Change them with `with`, then save through the repository:
  ```csharp
  var updated = existing with { Status = request.Status ?? existing.Status, UpdatedAt = clock.GetUtcNow() };
  await repository.UpdateAsync(updated, cancellationToken);
  ```
- **Collections:**
  - Expose collections as `IReadOnlyList<T>`.
  - Build new lists with collection expressions: `MemberStaffIds = [.. programme.MemberStaffIds, staffId]`.
  - Return `.ToList()` copies from repositories, never the backing dictionary.
- DTOs and events are positional `sealed record`s.
- Derived values are get-only computed properties (`Activity.IsClosed`).

## DTO validation
- **Request DTOs use nullable positional parameters** (`CreateActivityRequest(string? Title, …)`). That way a missing field reaches the service instead of being rejected by `[ApiController]`'s implicit-required rule.
- **Validation happens in the service**, collected with `ValidationErrorBuilder`, and thrown once:
  ```csharp
  var errors = new ValidationErrorBuilder();
  var name = errors.RequireText("name", request.Name, "Name is required.");
  if (name.Length > MaxNameLength) errors.Add("name", $"Name must be at most {MaxNameLength} characters.");
  if (request is { StartDate: { } start, EndDate: { } end } && end < start) errors.Add("endDate", "End date must be on or after the start date.");
  errors.ThrowIfAny();
  ```
- Field keys are camelCase JSON names (`title`, `storeId`, `assignedStaffId`). Trim text; limits are `const` fields on the service.
- Don't use DataAnnotations (`[Required]`, `[StringLength]`) on DTOs. Binding failures (malformed JSON, bad enum, bad `Guid`) are mapped by `ModelStateErrorFactory` (see api-integration).
- Cross-module existence checks use a lookup and add a field error (`assignedStaffId`, `staffId`). They don't throw `NotFoundError`.
