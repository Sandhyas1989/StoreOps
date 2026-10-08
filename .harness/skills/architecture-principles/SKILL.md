---
name: architecture-principles
description: Numbered, testable StoreOps architecture rules (ARCH-01..ARCH-07), the test-quality rule (TEST-01) and coverage gates (COV-01..COV-04), mapped to the four original AI failure modes, each with rationale, allowed and prohibited StoreOps examples, the exact evaluator evidence, and hard-gate status. Use when planning, generating, reviewing, or evaluating any change under src/StoreOps.Api or tests/StoreOps.Api.Tests.
---

# StoreOps architecture principles

These rules describe the StoreOps code as it exists in `src/StoreOps.Api`. Every rule names the check that proves it. If a rule and the code disagree, the change is wrong, unless the change updates this file, `ModuleRules.cs`, and `Journal.md` together.

## Vocabulary used by the rules

| Term | Exact meaning in StoreOps |
|---|---|
| Module | First namespace segment after `StoreOps.Api.Modules.`: `Activities`, `Programmes`, `Staff`, `Alerts`, `Reports`. Folder: `src/StoreOps.Api/Modules/<Module>/`. |
| Shared | Anything in namespace `StoreOps.Api.Shared*` (`Shared/Errors`, `Shared/Events`, `SharedInfrastructureExtensions`). |
| Repository | Interface `I<Name>Repository` and its implementation, in namespace `StoreOps.Api.Modules.<Module>.Persistence`. |
| Service | Class `<Name>Service` implementing `I<Name>Service`, in the module root namespace. |
| Lookup | Interface `I<Name>Lookup` in the module root namespace, implemented by that module's service and registered as the same scoped instance (see `ActivitiesModule.AddActivitiesModule`). |
| Integration event | `sealed record … : IIntegrationEvent`, declared by the publishing module (`ActivityEvents.cs`, `ProgrammeEvents.cs`). |

## How the evaluator runs the evidence

Run from the repository root:

```bash
dotnet build StoreOps.sln                                                           # must report 0 Warning(s), 0 Error(s)
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"   # 18 tests at baseline
dotnet test StoreOps.sln                                                            # full suite
```

`ModuleRulesSelfTests` runs each rule against the broken modules in `Architecture/ViolationFixtures.cs` and must find every planted violation. If any self-test fails, a passing `ModuleBoundaryTests` result is **not** evidence and the evaluator must report the affected rules as `BLOCKED`.

Verdicts per rule: `PASS`, `FAIL` (evidence shows a violation), or `BLOCKED` (the evidence could not be produced). A hard-gate rule that is `FAIL` or `BLOCKED` fails the sprint.

## Failure modes these rules guard

The harness exists because of four failure modes recorded in AI-generated code (AI-Native Architect Build Track case study; see `app-context` section 7). Each one has an explicit hard-gate rule here:

| # | Original AI failure mode | Primary rule | Supporting rules |
|---|---|---|---|
| FM-1 | Direct imports from another module's repository, bypassing the agreed service boundary and event bus | **ARCH-02** | ARCH-03 |
| FM-2 | Raw `Error` throws in service methods, bypassing the typed `AppError` hierarchy | **ARCH-05** | none |
| FM-3 | Tests that asserted HTTP status codes but did not verify business-rule compliance | **TEST-01** | COV-01..COV-04 (coverage counts only with TEST-01 assertions) |
| FM-4 | Missing event bus integration, with state changes written directly to sibling module repositories | **ARCH-04** | ARCH-02, ARCH-03 |

A review that finds any of these patterns reports the rule ID and the FM number.

---

## ARCH-01: Controller → Service → Repository in every module

**Rule.**
- Every module has at least one `*Controller`, one `*Service` class and one repository.
- Each controller's constructor takes its own module's `I*Service`.
- Each service's constructor takes its own module's `I*Repository` from `<Module>.Persistence`.
- Controllers never take a repository.
- Any type whose name contains `Repository` lives in `<Module>.Persistence`.

**Rationale.** HTTP concerns (routing, status codes, `CreatedAtAction`) stay in controllers. Business rules (validation, state transitions, event publishing) stay in services. Storage stays in repositories. That keeps the in-memory stores swappable without touching business code.

**Allowed (current code):**
```csharp
// Modules/Activities/ActivitiesController.cs
public sealed class ActivitiesController(IActivityService activities) : ControllerBase

// Modules/Activities/ActivityService.cs
public sealed class ActivityService(IActivityRepository repository, IStaffLookup staff, IEventBus eventBus, TimeProvider clock)

// Modules/Reports/ReportService.cs. Reports owns no data, so its repository is a read-only adapter over lookups.
public sealed class ReportService(IReportDataRepository repository, TimeProvider clock)
```

**Prohibited:**
```csharp
public sealed class ActivitiesController(IActivityRepository repository) : ControllerBase   // controller skips the service
public sealed class ReportService(IActivityLookup activities, ...)                        // service with no own repository
namespace StoreOps.Api.Modules.Alerts; public interface IAlertRepository { ... }         // repository outside .Persistence
```

**Evaluator evidence.**
- `ModuleBoundaryTests.Every_module_follows_controller_service_repository` passes.
- `ModuleBoundaryTests.Controllers_depend_on_services_not_repositories` passes.
- Self-tests `Detects_layering_violations` and `Detects_controller_depending_on_repository` pass.

**Hard gate:** Yes.

---

## ARCH-02: No module references or imports another module's repository

**Rule.**
- No type in module A references a type in module B whose name contains `Repository` or whose namespace is `StoreOps.Api.Modules.B.Persistence`.
- No `.cs` file under `src/StoreOps.Api` names another module's `Persistence` namespace, either in a `using` or fully qualified.
- `Program.cs` and `Shared` may not name any `Persistence` namespace. Each module registers its own repository in its `<Module>Module.cs`.

**Guards:** FM-1 (primary), FM-4 (no direct writes to a sibling's repository).

**Rationale.** Repositories are a module's private storage. Reading or writing another module's store bypasses its validation, its `IsClosed` and duplicate-member rules, and its events.

**Allowed:**
```csharp
// Modules/Activities/ActivitiesModule.cs: a module importing its own persistence
using StoreOps.Api.Modules.Activities.Persistence;
services.AddSingleton<IActivityRepository, InMemoryActivityRepository>();
```

**Prohibited:**
```csharp
// Modules/Programmes/ProgrammeService.cs
using StoreOps.Api.Modules.Staff.Persistence;
public sealed class ProgrammeService(IProgrammeRepository repository, IStaffRepository staffRepository, ...)

// any file outside Alerts
StoreOps.Api.Modules.Alerts.Persistence.IAlertRepository alerts;
```

**Evaluator evidence.**
- `ModuleBoundaryTests.No_module_references_another_modules_repository` passes (reflection over signatures).
- `ModuleBoundaryTests.No_source_file_imports_another_modules_persistence_namespace` passes (source scan; catches `using` directives that reflection cannot see).
- Self-tests `Detects_cross_module_repository_reference` and `Detects_import_of_another_modules_persistence_namespace` pass.

**Hard gate:** Yes.

---

## ARCH-03: Cross-module reads go through the target module's `*Lookup` contract

**Rule.**
- A constructor parameter whose type comes from another module must be an interface named `I*Lookup`.
- Other modules' data contracts (records, enums such as `Activity` or `ActivityStatus`) may appear in signatures.
- Another module's `I*Service`, concrete `*Service`, `*Repository`, `*Controller` or `*Handler` may not appear in a constructor.
- Every `*Lookup` method name starts with `Find`, `List` or `Get` and has no side effects.
- A lookup returns `null` for a missing item (`FindAsync`). It never throws `NotFoundError`. The calling module decides which error applies.

Current lookups:

| Lookup | Implemented by | Methods | Consumers |
|---|---|---|---|
| `IStaffLookup` | `StaffService` | `ListAsync`, `FindAsync` | `ActivityService`, `ProgrammeService`, `LookupReportDataRepository` |
| `IActivityLookup` | `ActivityService` | `ListAsync`, `FindAsync` | `LookupReportDataRepository` |
| `IProgrammeLookup` | `ProgrammeService` | `ListAsync`, `FindAsync` | `LookupReportDataRepository` |

**Guards:** FM-1 and FM-4 (supporting). Because lookups can only read, no cross-module dependency can perform a write.

**Rationale.** The target module's service stays the only door to its data. Because lookups can only read, no cross-module call can cause a write, which is what makes ARCH-04 enforceable.

**Allowed:**
```csharp
// ActivityService: validating an assignee, with the caller choosing the error
if (staffId is { } id && await staff.FindAsync(id, cancellationToken) is null)
    errors.Add("assignedStaffId", $"Staff member '{id}' does not exist.");
```

**Prohibited:**
```csharp
public sealed class ProgrammeService(IStaffService staff, ...)          // full service instead of lookup
public sealed class ReportService(ActivityService activities, ...)      // concrete cross-module service
public interface IActivityLookup { Task CloseAsync(Guid id, CancellationToken ct); }  // write on a lookup
```

**Evaluator evidence.**
- `ModuleBoundaryTests.Cross_module_constructor_dependencies_are_read_only_lookups` passes.
- `ModuleBoundaryTests.Lookup_contracts_expose_only_read_operations` passes.
- Self-tests `Detects_cross_module_non_lookup_dependency` (including the allowed `FixtureBetaReport` data-record case) and `Detects_lookup_with_write_operation` pass.
- Manual: for any new `*Lookup` method, the implementing service method does not call a repository `Add`, `Update` or `Delete` method or `PublishAsync`.

**Hard gate:** Yes.

---

## ARCH-04: Cross-module side effects only through `IEventBus`

**Rule.**
- When a change in one module must cause a state change in another, the source service publishes an integration event through `IEventBus.PublishAsync` after its own repository write succeeds.
- The receiving module reacts in an `IEventHandler<TEvent>` it owns and registers in its own `<Module>Module.cs`.
- The event record is declared in the publishing module.
- Handlers call only their own module's service or repository.
- `InMemoryEventBus` logs and swallows handler failures, so a handler can't fail or roll back the publishing request. Handlers must not rely on an `AppError` reaching the client.

Current events:

| Event | Published by | Handled by |
|---|---|---|
| `ActivityCreated` | `ActivityService.CreateAsync` | `Alerts.HighPriorityActivityAlertHandler` (raises `HIGH_PRIORITY_ACTIVITY` / `Warning` when `Priority == High`) |
| `ActivityDeleted` | `ActivityService.DeleteAsync` (only after a successful delete) | none yet |
| `ProgrammeMemberAdded` | `ProgrammeService.AddMemberAsync` | none yet |

**Guards:** FM-4 (primary).

**Rationale.** Publishers stay unaware of their consumers. That keeps the module graph acyclic (ARCH-07), and new reactions can be added without editing the publisher.

**Allowed:**
```csharp
// ActivityService.CreateAsync: persist, then publish
await repository.AddAsync(activity, cancellationToken);
await eventBus.PublishAsync(new ActivityCreated(activity.Id, activity.Title, activity.StoreId, activity.Priority, now), cancellationToken);

// AlertsModule.AddAlertsModule
services.AddScoped<IEventHandler<ActivityCreated>, HighPriorityActivityAlertHandler>();
```

**Prohibited:**
```csharp
// ActivityService calling the Alerts module directly
public sealed class ActivityService(..., IAlertService alerts)
await alerts.RaiseAsync(new RaiseAlertCommand(...), ct);

// publishing before the write, or on a failed operation
await eventBus.PublishAsync(new ActivityDeleted(id, now), ct);
if (!await repository.DeleteAsync(id, ct)) throw NotFound(id);
```

**Evaluator evidence.**
- ARCH-03 evidence passes. Cross-module constructor dependencies can only be read-only lookups, so a direct cross-module write cannot compile into the dependency graph.
- Event publication is asserted through `StoreOpsApiFactory.Events`:
  - `ActivitiesEndpointsTests.Post_activity_creates_pending_activity_and_publishes_event`
  - `Delete_activity_removes_it_from_store_and_publishes_event`
  - `Delete_unknown_activity_returns_not_found_and_publishes_nothing`
  - `ProgrammesEndpointsTests.Post_member_adds_staff_to_programme_and_publishes_event`
  - `Post_unknown_staff_member_returns_validation_error` (asserts no event)
- The cross-module effect is asserted end to end: `AlertsEndpointsTests.High_priority_activity_raises_alert_via_event_bus` and `Low_priority_activity_does_not_raise_alert`.
- Bus semantics: `InMemoryEventBusTests` (all handlers called; a failing handler isolated; no handlers is fine).
- Every new event or handler needs a test of the same kind (published on success, not published on failure, handler effect visible through the consumer's endpoint).

**Hard gate:** Yes.

---

## ARCH-05: Typed `AppError` contract for every expected failure

**Rule.**
- Services signal expected failures only by throwing an `AppError` subtype from `Shared/Errors/AppError.cs`:

  | Type | Status | `code` |
  |---|---|---|
  | `ValidationError` | 400 | always `VALIDATION_FAILED`, with field `details` |
  | `NotFoundError` | 404 | `<ENTITY>_NOT_FOUND` |
  | `ConflictError` | 409 | state-specific code |

- Error codes are `SCREAMING_SNAKE_CASE` `public const string` fields on the owning service:
  - `ActivityService.NotFoundCode` = `ACTIVITY_NOT_FOUND`
  - `ActivityService.ClosedCode` = `ACTIVITY_CLOSED`
  - `ProgrammeService.NotFoundCode` = `PROGRAMME_NOT_FOUND`
  - `ProgrammeService.MemberExistsCode` = `PROGRAMME_MEMBER_EXISTS`
  - `StaffService.NotFoundCode` = `STAFF_NOT_FOUND`
  - Shared codes live in `ErrorCodes`.
- A status code with no subtype yet means adding a new subtype in `Shared/Errors`, not throwing something else.
- Field validation collects all errors with `ValidationErrorBuilder` (`Add`, `RequireText`, `ThrowIfAny`) and uses camelCase field keys matching the request JSON.
- **Scope:** this rule applies to **controllers and services**, for expected business errors.
- Controllers never throw, never catch, and never build error responses (`NotFound()`, `BadRequest()`, `Conflict()`, `StatusCode()`, `Problem()`).
- Services never catch exceptions.
- **Exception for background workers** (`BackgroundService` types such as the planned `SlaEvaluationWorker`) and other infrastructure components that aren't controllers or services:
  - they **may** catch *unexpected* exceptions around a unit of work, to log them and keep running;
  - they must not decide business outcomes in the catch block, convert exceptions into business results, or swallow an `AppError` to skip a business rule;
  - business rules and `AppError` behaviour stay in the services they call.
- Every error body is `ErrorResponse { code, message, statusCode, details? }`. `statusCode` must equal the HTTP status.
- Anything that isn't an `AppError` becomes `INTERNAL_ERROR` / 500 with no exception text.
- Model-binding failures go through `ModelStateErrorFactory`. A broken body is reported under the `body` key, a bad field value under its field name, and no parser text or CLR type names appear.

**Guards:** FM-2 (primary).

**Rationale.** Clients and tests branch on a stable `code`, not on message text or ad-hoc status codes. Raw exceptions turn expected outcomes into 500s and leak internals.

**Allowed:**
```csharp
// ActivityService.UpdateAsync
if (existing.IsClosed)
    throw new ConflictError(ClosedCode, $"Activity '{id}' is {existing.Status} and can no longer be changed.");

// helper factories must return an AppError subtype
private static NotFoundError NotFound(Guid id) => new(NotFoundCode, $"Activity '{id}' was not found.");
```

**Prohibited:**
```csharp
throw new InvalidOperationException("Activity not found");                 // raw exception for an expected failure
throw new KeyNotFoundException();                                          // same
public async Task<IActionResult> GetById(Guid id) { ... return NotFound(); } // controller-built error, wrong body shape
catch (Exception ex) { return StatusCode(500, ex.Message); }               // swallows the contract and leaks internals
throw new ValidationError("Title is required.");                           // no field details; use ValidationErrorBuilder
catch (ConflictError) { /* skip */ }                                       // in a worker: swallowing an AppError to bypass a business rule
```

**Allowed in a background worker only (infrastructure, not a controller or service):**
```csharp
try { await evaluator.EvaluateAsync(stoppingToken); }                      // the service applies all business rules and AppErrors
catch (Exception ex) when (ex is not OperationCanceledException)
{
    logger.LogError(ex, "SLA evaluation tick failed");                       // log and continue with the next tick
}
```

**Evaluator evidence.** Each of these must print nothing:
```bash
grep -rnP "throw new (?!(ValidationError|NotFoundError|ConflictError)\b)" src/StoreOps.Api --include=*.cs | grep -v /obj/
grep -rnE "\b(NotFound|BadRequest|Conflict|StatusCode|Problem|UnprocessableEntity)\(" src/StoreOps.Api/Modules --include=*Controller.cs
grep -nE "\bcatch\b" src/StoreOps.Api/Modules/*/*Controller.cs src/StoreOps.Api/Modules/*/*Service.cs
```

- **Worker check (review):** every `catch` outside controllers and services (`grep -rnE "\bcatch\b" src/StoreOps.Api/Modules --include=*.cs`) must be in a background worker or another infrastructure component. Each one must only log and continue (or stop on cancellation), must not catch an `AppError` subtype by type, and must not change state. Any other use is a `FAIL`.

- For `grep -rnP "throw (?!new)\w+\(" src/StoreOps.Api --include=*.cs`, every hit must be a helper whose declared return type is an `AppError` subtype. Baseline hits: `ActivityService.NotFound` (`NotFoundError`) and `ProgrammeService.StaffIdError` (`ValidationError`).
- Every error-path test reads through `TestJson.ReadErrorAsync`, which asserts that the body's `statusCode` matches the HTTP status, and checks `error.Code` against the service constant, never a string literal.
- The body-sanitising tests pass: `Post_activity_with_invalid_body_returns_sanitised_body_error` (3 cases) and `Post_activity_with_unknown_enum_value_reports_field_without_type_names`.

**Hard gate:** Yes.

---

## ARCH-06: Reports are read-only toward activities, programmes and staff

**Rule.**
- The Reports module never references `IEventBus`.
- It never references another module's `I*Service`, concrete `*Service`, `*Controller` or any non-`*Lookup` interface.
- Its only data source is `IReportDataRepository`, whose single method is `LoadAsync` and whose implementation `LookupReportDataRepository` depends only on `IActivityLookup`, `IProgrammeLookup` and `IStaffLookup`.
- Reports endpoints are `GET` only.
- Generating a report changes no source data and publishes no event.

**Rationale.** Reports must be safe to call at any rate. A report that mutates or emits events would couple read traffic to write side effects.

**Allowed:**
```csharp
// Reports/Persistence/ReportDataRepository.cs
public async Task<ReportSourceData> LoadAsync(CancellationToken ct) => new(
    await activities.ListAsync(ct), await programmes.ListAsync(ct), await staff.ListAsync(ct));
```

**Prohibited:**
```csharp
public sealed class ReportService(IReportDataRepository repository, IEventBus eventBus, ...)    // side-effect capability
public sealed class LookupReportDataRepository(IActivityService activities, ...)               // write-capable dependency
[HttpPost("summary/refresh")] public Task<IActionResult> Refresh(...)                          // mutating Reports route
```

**Evaluator evidence.**
- `ModuleBoundaryTests.Reports_module_is_read_only_toward_other_modules` passes. It also asserts that Reports types exist, so an empty scan can't pass.
- Self-test `Detects_read_only_module_with_side_effect_dependencies` passes.
- `ReadOnlyModulesEndpointsTests.Report_summary_does_not_change_source_data` passes (the activity is unchanged and exactly one `ActivityCreated` event exists).
- `ReadOnlyModulesEndpointsTests.Report_summary_aggregates_exact_counts_from_other_modules` passes.
- `grep -rnE "\[Http(Post|Put|Patch|Delete)" src/StoreOps.Api/Modules/Reports` prints nothing.

**Hard gate:** Yes.

---

## ARCH-07: Acyclic module graph; Shared depends on no module

**Rule.** Module dependencies, read from type signatures, must form a directed acyclic graph. The baseline edges are:
- `Activities → Staff`
- `Programmes → Staff`
- `Alerts → Activities` (the `ActivityCreated` contract only)
- `Reports → Activities, Programmes, Staff`

No type in `StoreOps.Api.Shared*` references a module type. A new edge needs a check that it doesn't close a cycle. When a reverse dependency is needed, publish an event (ARCH-04) instead of adding the edge.

**Rationale.** Cycles make modules impossible to change or test alone. Shared depending on a module would turn Shared into a hidden cycle hub.

**Allowed:** `Alerts` handling `ActivityCreated` (`Alerts → Activities`), while `Activities` has no reference to `Alerts`.

**Prohibited:** `Staff` taking `IActivityLookup` to show a staff member's activities, because that creates `Activities → Staff → Activities`. Also prohibited: `Shared/Errors` referencing `ActivityService.NotFoundCode`.

**Evaluator evidence.**
- `ModuleBoundaryTests.Module_dependency_graph_has_no_cycles` passes. On failure it prints the cycle path.
- `ModuleBoundaryTests.Shared_infrastructure_does_not_depend_on_modules` passes.
- Self-tests `Detects_module_cycle` and `Detects_shared_depending_on_module` pass.

**Hard gate:** Yes.

---

## TEST-01: Tests assert business-rule outcomes, not only HTTP status codes

**Guards:** FM-3 (primary).

**Rule.** Every test that exercises StoreOps behaviour asserts the business outcome:
1. **Success on a write path** (POST, PATCH, DELETE): assert the returned values, assert the stored state through a follow-up `GET` (or the absence of the item after a delete), and assert the published events through `Factory.Events.Of<TEvent>()` wherever the operation publishes (ARCH-04 table).
2. **Expected failure:**
   - assert `error.Code` against the service constant (`ActivityService.ClosedCode`, `ProgrammeService.MemberExistsCode`, …) or `ErrorCodes.*`, never a string literal;
   - for `VALIDATION_FAILED`, assert the `Details` field keys;
   - for a rejected write, assert that the stored state is unchanged and that no event was published.
3. **Filters and queries:** seed at least one item the filter must include and one it must exclude, then assert the exact result. `Assert.All` or `Assert.DoesNotContain` over a collection only counts if the same test first proves the collection holds the case being checked.
4. **Counts are exact.** Tests derive from `ApiTestBase` (a fresh API per test), so `Assert.True(x >= n)` style range checks are not allowed.
5. **A status-only test** is allowed only when the status itself is the contract, and the test name must say so. Baseline allowlist:
   - `SwaggerTests.Swagger_is_not_exposed_outside_development`: 404 means "not exposed".
   - `InMemoryEventBusTests.Publish_without_handlers_completes`: completing without an exception is the contract.

   Adding to this list needs a reason in the sprint review.

**Rationale.** FM-3 lets broken business rules ship behind green tests. It has already happened in this repository: the first baseline's `Get_alerts_filters_by_severity` ran `Assert.All` over a list that could never contain a Critical alert, and five more tests checked status codes without checking state (2026-10-02 audit).

**Allowed (current code):**
```csharp
// ActivitiesEndpointsTests.Patch_closed_activity_returns_conflict_and_leaves_activity_unchanged
var error = await response.ReadErrorAsync(HttpStatusCode.Conflict);
Assert.Equal(ActivityService.ClosedCode, error.Code);
Assert.Equal(closed, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options));

// AlertsEndpointsTests.Get_alerts_filters_by_severity: seeds Critical, Info and Warning, then asserts the exact match
Assert.Equal(3, all.Count);
Assert.Equal(critical.Id, Assert.Single(filtered).Id);
```

**Prohibited (the original baseline tests):**
```csharp
// status only: does not prove the activity is gone from the list or that ActivityDeleted was published
Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

// vacuous: no Critical alert can exist, so this passes on an empty list
var alerts = await (await _client.GetAsync("/api/alerts?severity=Critical")).ReadAsync<List<AlertResponse>>();
Assert.All(alerts, a => Assert.Equal(AlertSeverity.Critical, a.Severity));

// range check on shared state
Assert.True(report.TotalActivities >= 1);
```

**Evaluator evidence.**
- **Check A (automated, status-only detector).** Run the script below from the repository root. It must print only the allowlisted tests (baseline output: exactly the two allowlisted tests). Any other name is a `FAIL`.
  ```python
  import re, glob, pathlib
  STATUS = re.compile(r"StatusCode|ReadErrorAsync")
  for p in sorted(glob.glob("tests/StoreOps.Api.Tests/**/*Tests.cs", recursive=True)):
      t = pathlib.Path(p).read_text(encoding="utf-8")
      for m in re.finditer(r"\[(?:Fact|Theory)\][^{]*?public async Task (\w+)\([^)]*\)\s*\{", t, re.S):
          i, depth = m.end(), 1
          while depth:
              depth += {"{": 1, "}": -1}.get(t[i], 0); i += 1
          asserts = re.findall(r"(?:Assert\.\w+|ReadErrorAsync)\([^;]*;", t[m.end():i])
          if not [a for a in asserts if not STATUS.search(a)]:
              print(f"{pathlib.Path(p).name}: {m.group(1)}")
  ```
- **Check B (range checks).** `grep -rnE "Assert\.True\([^;]*(>=|<=| > | < )" tests/StoreOps.Api.Tests --include=*.cs` prints nothing.
- **Check C (manual).** Review every `Assert.All` / `Assert.DoesNotContain` hit from `grep -rnE "Assert\.(All|DoesNotContain)\(" tests/StoreOps.Api.Tests --include=*Tests.cs`. Each must be in a test that seeds or first asserts the case. At baseline the only non-architecture hit is `ActivitiesEndpointsTests.cs` (`Post_activity_with_unknown_enum_value_reports_field_without_type_names`), which first asserts `error.Details!["priority"]`.
- **Check D (manual, new tests).** For each new or changed test, the reviewer states which business rule it proves, and confirms that removing the rule's line in the service would make the test fail.

**Hard gate:** Yes. Checks A and B are automated; C and D are review checks recorded in `.harness/reviews/`.

---

## COV-01..COV-04: Line-coverage thresholds

| ID | Scope (classes in `StoreOps.Api`, aggregated by top-level class) | Minimum line coverage | Hard gate |
|---|---|---|---|
| COV-01 | Service layer: classes in `StoreOps.Api.Modules.*` named `*Service` or `*Handler` | **80%** | Yes |
| COV-02 | Controllers: classes named `*Controller` | **70%** | Yes |
| COV-03 | Shared utilities: classes in `StoreOps.Api.Shared*` | **60%** | Yes |
| COV-04 | Overall: every class in the `StoreOps.Api` assembly | **70%** | Yes |

**Rationale.**
- The service layer holds the business rules, so it needs the highest bar.
- Controllers are thin and are mostly exercised through `WebApplicationFactory` tests.
- Shared code includes defensive branches, such as the 500 path in `AppErrorExceptionHandler`, that are hard to trigger through the API.

**How it is measured.**
- Use line coverage (cobertura `line-rate`) from the full `dotnet test StoreOps.sln` run.
- Aggregate as covered lines divided by coverable lines across the scope. Don't average per-class percentages.
- Fold compiler-generated nested entries (`ActivityService/<CreateAsync>d__9`, `<>c__DisplayClass…`) into their outer class.
- Thresholds are inclusive (`>=`). Test assemblies and the fixture types in `ViolationFixtures.cs` are not in scope.

**Allowed:** a new `ProgrammeService.RemoveMemberAsync` that ships with endpoint tests for the success, not-found and not-a-member paths, keeping COV-01 at 80% or above.

**Prohibited:**
- Raising coverage with tests that only assert status codes or deserialise without asserting values. Coverage counts only together with the behaviour assertions required by TEST-01.
- Excluding classes with `[ExcludeFromCodeCoverage]` to pass a gate.

**Evaluator evidence.**
- The collector is `coverlet.collector` 6.0.2, referenced in `tests/StoreOps.Api.Tests/StoreOps.Api.Tests.csproj`. Delete old results first so the newest report is unambiguous:
  ```bash
  rm -rf tests/StoreOps.Api.Tests/TestResults
  dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
  # report: tests/StoreOps.Api.Tests/TestResults/<run-id>/coverage.cobertura.xml (package "StoreOps.Api" only)
  ```
- From that file, the evaluator computes each scope as follows:
  - Take every `<class>` in package `StoreOps.Api`.
  - Map the class name to its top-level class (the text before the first `/`).
  - Collect the unique `(filename, line number)` pairs for the scope; a pair is covered if any entry has `hits > 0`.
  - Report `covered/total = %` next to the threshold.
- If the report is missing or the test run fails, the verdict is `BLOCKED`.
- Baseline measured on 2026-10-02 (67 tests):

  | Gate | Covered / coverable | Result |
  |---|---|---|
  | COV-01 service | 190 / 206 = **92.2%** (6 classes) | PASS |
  | COV-02 controllers | 39 / 39 = **100%** (5 classes) | PASS |
  | COV-03 shared | 101 / 107 = **94.4%** (10 classes) | PASS |
  | COV-04 overall | 570 / 592 = **96.3%** (52 classes) | PASS |

  Classes below 100% at baseline:
  - `ActivityService` 83/93
  - `ProgrammeService` 52/58
  - `AppErrorExceptionHandler` 11/15 (the unexpected-500 path is never triggered)
  - `ModelStateErrorFactory` 33/35

  A drop of more than 5 points in any scope against these numbers must be explained in the sprint review, even if it stays above the threshold.
- Any `[ExcludeFromCodeCoverage]` in `src/` is a `FAIL` for COV-04 unless `Journal.md` records the decision.
