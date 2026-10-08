---
name: component-patterns
description: The StoreOps Controller, Service, Repository, AppError, IEventBus/event and handler patterns, copied from the real code in src/StoreOps.Api, plus the constraints for components that approved contracts introduce (options, hosted worker). Load for sprints that add or change these components.
---

# StoreOps component patterns

Copy these shapes. Rule IDs refer to architecture-principles.

## Controller (`Modules/Activities/ActivitiesController.cs`)
```csharp
[ApiController]
[Route("api/activities")]
public sealed class ActivitiesController(IActivityService activities) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<ActivityResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ActivityResponse.From(await activities.GetAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ActivityResponse>> Create(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var created = await activities.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ActivityResponse.From(created));
    }
}
```
- A controller has one dependency: its own module's `I*Service` (ARCH-01).
- It binds the request, calls the service once, maps to `*Response.From`, and picks the success status (`Ok`, `CreatedAtAction`, `NoContent`).
- It never contains `try`/`catch`, `throw`, `NotFound()`, `BadRequest()` or other business logic (ARCH-05).
- Route ids are `{id}` with a `Guid` parameter and **no** `:guid` constraint. A malformed id must reach model binding and return `VALIDATION_FAILED`, not a bare 404.
- A new route family gets a new controller class in the module, such as the planned `ActivitySlaController` for `api/activities/sla-evaluations`.

## Service (`Modules/Activities/ActivityService.cs`)
```csharp
public sealed class ActivityService(IActivityRepository repository, IStaffLookup staff, IEventBus eventBus, TimeProvider clock)
    : IActivityService, IActivityLookup
{
    public const string NotFoundCode = "ACTIVITY_NOT_FOUND";
    public const string ClosedCode = "ACTIVITY_CLOSED";

    public async Task<Activity> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(id, cancellationToken);                       // 404 via NotFoundError
        if (existing.IsClosed)
            throw new ConflictError(ClosedCode, $"Activity '{id}' is {existing.Status} and can no longer be changed.");
        // validate → ThrowIfAny → `with` → repository.UpdateAsync → return
    }

    private static NotFoundError NotFound(Guid id) => new(NotFoundCode, $"Activity '{id}' was not found.");
}
```
- The order inside a write method is always: **load (404) → state check (409) → validate (400) → build with `with` → persist → publish → return**.
- Publish only after a successful persist. Never publish on a failed path (`DeleteAsync` publishes `ActivityDeleted` only after `repository.DeleteAsync` returns true).
- Read-only lookup methods (`ListAsync`, `FindAsync`) live on the same class, return `T?`, and never throw for "missing" (ARCH-03).
- Error helpers are `private static` and return the `AppError` subtype (`NotFound`, `ProgrammeService.StaffIdError`).
- Business limits are `private const` fields (`MaxTitleLength = 200`).

## Repository (`Modules/Activities/Persistence/ActivityRepository.cs`)
```csharp
namespace StoreOps.Api.Modules.Activities.Persistence;

public sealed class InMemoryActivityRepository : IActivityRepository
{
    private readonly ConcurrentDictionary<Guid, Activity> _items = new();

    public Task<Activity?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(id, out var activity) ? activity : null);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryRemove(id, out _));
}
```
- Repositories live in `<Module>.Persistence` (ARCH-02), are registered as singletons, and contain no business rules.
- A "do this once" operation must be **atomic in the repository** (`TryAdd`, `TryRemove`, `AddOrUpdate` with a compare), returning `bool`. That's the pattern the approved contracts require for `ISlaRecordRepository.TryAdvanceStageAsync` and the alert de-duplication.
- Seed data lives in a static class next to the entity (`StaffSeed`), with fixed `Guid`s.
- Reports has no stored data. Its repository adapts lookups (`LookupReportDataRepository`) and is the only repository allowed to depend on other modules' lookups.

## AppError (`Shared/Errors/AppError.cs`)
```csharp
public abstract class AppError(string code, string message, int statusCode, IReadOnlyDictionary<string, string[]>? details = null) : Exception(message)
public sealed class ValidationError(string message, IReadOnlyDictionary<string, string[]>? details = null) : AppError(ErrorCodes.ValidationFailed, message, 400, details);
public sealed class NotFoundError(string code, string message) : AppError(code, message, 404);
public sealed class ConflictError(string code, string message) : AppError(code, message, 409);
```
- `AppErrorExceptionHandler` turns any `AppError` into `ErrorResponse(Code, Message, StatusCode, Details)`, and anything else into `500 INTERNAL_ERROR`.
- Services throw; controllers and services never catch (ARCH-05). Only background workers and other infrastructure components may catch *unexpected* exceptions, to log them and continue (see the Hosted worker entry below).
- If a sprint needs a new status code, add a subtype in `Shared/Errors` with its own test. Never throw a framework exception (ARCH-05).

## Events and the bus (`Shared/Events`, `Modules/Activities/ActivityEvents.cs`)
```csharp
public sealed record ActivityCreated(Guid ActivityId, string Title, string StoreId, ActivityPriority Priority, DateTimeOffset OccurredAt) : IIntegrationEvent;

await repository.AddAsync(activity, cancellationToken);
await eventBus.PublishAsync(new ActivityCreated(activity.Id, activity.Title, activity.StoreId, activity.Priority, now), cancellationToken);
```
- **The publishing module declares the event**, in `<Entity>Events.cs`.
- **Payload:** ids plus the values consumers need, so they never have to call back into the publisher. `OccurredAt` is the same `clock.GetUtcNow()` value the service used for the change.
- **Delivery:** `InMemoryEventBus` runs handlers in order, in the publisher's scope, and logs and swallows handler exceptions. The publisher's request never fails because of a consumer.

## Handler (`Modules/Alerts/HighPriorityActivityAlertHandler.cs`)
```csharp
public sealed class HighPriorityActivityAlertHandler(IAlertService alerts) : IEventHandler<ActivityCreated>
{
    public async Task HandleAsync(ActivityCreated integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.Priority != ActivityPriority.High) return;
        await alerts.RaiseAsync(new RaiseAlertCommand(AlertTypes.HighPriorityActivity, AlertSeverity.Warning,
            $"High-priority activity '{integrationEvent.Title}' created for store {integrationEvent.StoreId}.",
            SourceModule: "activities", SourceId: integrationEvent.ActivityId), cancellationToken);
    }
}
// AlertsModule: services.AddScoped<IEventHandler<ActivityCreated>, HighPriorityActivityAlertHandler>();
```
- **The handler lives in the consuming module** and calls only its own service, plus `*Lookup` interfaces for reads (ARCH-03, ARCH-04).
- **Filter first, then act.** The handler must be **idempotent**: the same event delivered twice gives the same end state.
- Alert type strings are constants in `AlertTypes`.

## Components introduced by approved contracts (not in the repo yet)
- **Options (S4):**
  - `SlaOptions` in `Modules/Activities`.
  - Registered with `AddOptions<SlaOptions>().BindConfiguration("Sla").Validate(...).ValidateOnStart()`.
  - Defaults live in `appsettings.json`. Inject `IOptions<SlaOptions>` into the service.
- **Hosted worker (S5):**
  - `SlaEvaluationWorker : BackgroundService`, which must **not** be named `*Service` (RISK-07, ARCH-01 rule).
  - Uses `PeriodicTimer(interval, timeProvider)`.
  - Creates a scope on each tick via `IServiceScopeFactory` and calls `ISlaEvaluationService`.
  - Logs and continues on exceptions.
  - Registered only when `Sla:WorkerEnabled` is true.
