---
name: app-context
description: Ground-truth context for the StoreOps retail store-operations API. Covers its purpose, the five modules, key entities, the nine baseline endpoints, the three-layer structure, the .NET 8 stack with exact build/run/test commands, and why the harness exists (the four original AI failure modes). Load before planning, generating, reviewing, or evaluating any StoreOps change.
---

# StoreOps application context

This file describes the code in `src/StoreOps.Api` and `tests/StoreOps.Api.Tests`. If this file and the code disagree, the code wins and this file must be updated in the same change. The binding rules are in `.harness/skills/architecture-principles/SKILL.md`, referred to here by rule ID (ARCH-xx, COV-xx).

## 1. Retail purpose

StoreOps is a REST API for running the day-to-day operations of retail stores. It lets store teams:
- Track **activities**: units of store work such as restocks, price changes, planogram resets and compliance checks. Each has a priority, an optional assignee and an optional due time.
- Run **programmes**: store initiatives such as a seasonal range reset or a food-safety refresher, with staff enrolled as members.
- See **alerts** raised automatically when something needs attention.
- Pull **reports** that summarise activities, programmes and staff.

There is no database. All data is in memory and is lost when the process restarts. Staff are seeded at startup.

## 2. The five domain modules (plus Shared)

| Module | Folder | Owns | Writes | Reads from other modules | Publishes | Handles |
|---|---|---|---|---|---|---|
| **Activities** | `Modules/Activities` | `Activity` | create, patch, delete | `IStaffLookup` (assignee exists) | `ActivityCreated`, `ActivityDeleted` | none |
| **Programmes** | `Modules/Programmes` | `Programme` | create, add member | `IStaffLookup` (member exists) | `ProgrammeMemberAdded` | none |
| **Staff** | `Modules/Staff` | `StaffMember` (seeded) | none (read-only baseline) | none | none | none |
| **Alerts** | `Modules/Alerts` | `Alert` | raise (from event handlers only) | none | none | `ActivityCreated` → `HighPriorityActivityAlertHandler` |
| **Reports** | `Modules/Reports` | nothing (no stored data) | none (read-only, ARCH-06) | `IActivityLookup`, `IProgrammeLookup`, `IStaffLookup` via `IReportDataRepository` | none | none |
| Shared | `Shared/` | the error contract, the event bus | n/a | depends on no module (ARCH-07) | n/a | n/a |

Module dependency graph (acyclic): `Activities → Staff`, `Programmes → Staff`, `Alerts → Activities` (event contract only), `Reports → Activities, Programmes, Staff`.

## 3. Key entities

All entities are immutable `sealed record`s. Updates use `with` and are saved back through the module's repository.

**`Activity`** (`Modules/Activities/Activity.cs`)
- `Id: Guid`, `Title: string` (required, trimmed, max 200 characters), `Description: string?` (max 2000), `StoreId: string` (required).
- `Status: ActivityStatus`, one of `Pending`, `InProgress`, `Completed`, `Cancelled`. New activities start as `Pending`.
- `Priority: ActivityPriority`, one of `Low`, `Medium`, `High`. Defaults to `Medium`.
- `AssignedStaffId: Guid?` (must be an existing staff member), `DueAt: DateTimeOffset?`, `CreatedAt`, `UpdatedAt`.
- `IsClosed` is true when the status is `Completed` or `Cancelled`. A closed activity can't be changed: PATCH returns `409 ACTIVITY_CLOSED`.

**`Programme`** (`Modules/Programmes/Programme.cs`)
- `Id`, `Name` (required, trimmed, max 200), `Description?`.
- `StartDate: DateOnly?`, `EndDate: DateOnly?`. `EndDate` must be on or after `StartDate`.
- `MemberStaffIds: IReadOnlyList<Guid>`. Each member must be an existing staff member, with no duplicates (`409 PROGRAMME_MEMBER_EXISTS`).
- `CreatedAt`, `UpdatedAt`.

**`StaffMember`** (`Modules/Staff/StaffMember.cs`)
- `Id`, `Name`, `Role: StaffRole` (`StoreManager`, `ShiftLead`, `Associate`), `StoreId`.
- Three members are seeded with fixed ids in `StaffSeed`, all in `STORE-001`:
  - `StoreManagerId` `5e1f0000-0000-4000-8000-000000000001`
  - `ShiftLeadId` `…0002`
  - `AssociateId` `…0003`

**`Alert`** (`Modules/Alerts/Alert.cs`)
- `Id`, `Type: string` (e.g. `AlertTypes.HighPriorityActivity` = `HIGH_PRIORITY_ACTIVITY`), `Severity: AlertSeverity` (`Info`, `Warning`, `Critical`).
- `Message`, `SourceModule` (e.g. `"activities"`), `SourceId: Guid?`, `CreatedAt`.

**`StoreSummaryReport`** (`Modules/Reports/ReportContracts.cs`), a computed result that is never stored:
- `GeneratedAt`, `TotalActivities`, `ActivitiesByStatus` (a key for every status, including zero counts).
- `OpenHighPriorityActivities` (`High` and not closed), `TotalProgrammes`, `TotalProgrammeMemberships`, `TotalStaff`.

**Integration events** (declared by the publisher, implementing `IIntegrationEvent`):
- `ActivityCreated(ActivityId, Title, StoreId, Priority, OccurredAt)`
- `ActivityDeleted(ActivityId, OccurredAt)`
- `ProgrammeMemberAdded(ProgrammeId, StaffId, OccurredAt)`

JSON is camelCase and enums are sent as strings (e.g. `"priority": "High"`).

## 4. The nine baseline endpoints

| # | Method and route | Controller action | Success | Expected failures (`code`) |
|---|---|---|---|---|
| 1 | `GET /api/activities?status=&storeId=` | `ActivitiesController.List` | 200 `ActivityResponse[]`, oldest first | 400 `VALIDATION_FAILED` (bad `status`) |
| 2 | `POST /api/activities` | `ActivitiesController.Create` | 201 `ActivityResponse` + `Location`; publishes `ActivityCreated` | 400 `VALIDATION_FAILED` (`title`, `storeId`, `assignedStaffId`, `description`, `body`, bad enum) |
| 3 | `GET /api/activities/{id}` | `ActivitiesController.GetById` | 200 `ActivityResponse` | 400 `VALIDATION_FAILED` (malformed id), 404 `ACTIVITY_NOT_FOUND` |
| 4 | `PATCH /api/activities/{id}` | `ActivitiesController.Update` | 200 `ActivityResponse`; fields left out or null are unchanged | 400 `VALIDATION_FAILED`, 404 `ACTIVITY_NOT_FOUND`, 409 `ACTIVITY_CLOSED` |
| 5 | `DELETE /api/activities/{id}` | `ActivitiesController.Delete` | 204; publishes `ActivityDeleted` | 404 `ACTIVITY_NOT_FOUND` (no event) |
| 6 | `GET /api/programmes` | `ProgrammesController.List` | 200 `ProgrammeResponse[]` | none |
| 7 | `POST /api/programmes` | `ProgrammesController.Create` | 201 `ProgrammeResponse` + `Location` (no members) | 400 `VALIDATION_FAILED` (`name`, `endDate`, `body`) |
| 8 | `POST /api/programmes/{id}/members` body `{ "staffId" }` | `ProgrammesController.AddMember` | 200 `ProgrammeResponse`; publishes `ProgrammeMemberAdded` | 400 `VALIDATION_FAILED` (`staffId` missing or unknown), 404 `PROGRAMME_NOT_FOUND`, 409 `PROGRAMME_MEMBER_EXISTS` |
| 9 | `GET /api/alerts?severity=` | `AlertsController.List` | 200 `AlertResponse[]`, newest first | 400 `VALIDATION_FAILED` (bad `severity`) |

Supporting endpoints (not part of the baseline nine):
- `GET /api/programmes/{id}` (target of the `Location` header)
- `GET /api/staff`, `GET /api/staff/{id}` (`404 STAFF_NOT_FOUND`)
- `GET /api/reports/summary`
- `GET /health`
- `/swagger` (Development environment only)

Every error body has the shape `{ "code", "message", "statusCode", "details"? }` (ARCH-05). An unexpected failure returns `500 INTERNAL_ERROR` with no exception text.

## 5. Three-layer structure

Every module follows **Controller → Service → Repository** (ARCH-01):

| Layer | Naming and location | Responsibility | Must not |
|---|---|---|---|
| Controller | `<Module>/<Name>Controller.cs`, `[ApiController]`, `[Route("api/…")]`, constructor takes `I<Name>Service` | Bind the request, call the service, map the entity to a `*Response` record, choose the success status (`Ok`, `CreatedAtAction`, `NoContent`) | throw, catch, build error responses, touch repositories |
| Service | `<Module>/<Name>Service.cs`, implements `I<Name>Service` (and `I<Name>Lookup` if other modules read from it) | Validation (`ValidationErrorBuilder`), business rules, `AppError`s, `TimeProvider` timestamps, publishing events after a successful write | reference another module's service or repository; anything but `*Lookup` across modules |
| Repository | `<Module>/Persistence/<Name>Repository.cs`, `I<Name>Repository` + `InMemory<Name>Repository` (`ConcurrentDictionary`, singleton) | Store and retrieve records. No business logic. | be referenced outside its module (ARCH-02) |

Each module registers its services in `<Module>/<Module>Module.cs` (`Add<Module>Module`). `Program.cs` only calls those methods.

- **Lifetimes:** repositories are singletons. Services, lookups, event handlers and `IEventBus` are scoped.
- **Lookups:** `I*Lookup` is registered as the same scoped instance as the module's service.
- **Reports:** Reports has no data of its own, so its repository `LookupReportDataRepository` is a read-only adapter over the three lookups.

## 6. Stack and exact commands

- **Stack (Journal.md, Decision 001):**
  - .NET 8 (SDK pinned in `global.json` to `8.0.425`, `rollForward: latestFeature`), C# 12, ASP.NET Core Web API (controllers).
  - Tests: xUnit 2.9.2, `Microsoft.AspNetCore.Mvc.Testing` 8.0.11 (`WebApplicationFactory<Program>`).
  - API docs: Swashbuckle 6.9.0.
- **Compiler settings (`Directory.Build.props`):** nullable reference types on, nullable warnings treated as errors, implicit usings on. Don't use `!` to silence nullable warnings; use `ValidationErrorBuilder.RequireText` or an explicit check.

Run from the repository root:

```bash
dotnet build StoreOps.sln                     # expected: 0 Warning(s), 0 Error(s)
dotnet test StoreOps.sln                      # full suite (67 tests at baseline)
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"   # rule checks (18 at baseline)
dotnet run --project src/StoreOps.Api         # http://localhost:5080 (Development), Swagger at /swagger
```

- **Port and environment:** the port and `Development` environment come from `src/StoreOps.Api/Properties/launchSettings.json`.
- **Test environment:** tests run in the `Testing` environment, where Swagger is off.
- **Test isolation:** each endpoint test class derives from `ApiTestBase`, which gives every test a fresh `StoreOpsApiFactory` with empty stores and an `EventRecorder` for asserting published events.
- **Locked build output:** if `dotnet build` fails with `MSB3027 … being used by another process`, a running `StoreOps.Api` has locked `bin/`. Stop it first. Don't report that build as a code failure.
- **Docker** (multi-stage `Dockerfile`, runs as Production on port 8080, non-root user):
  ```bash
  docker build -t storeops-api .
  docker run --rm -p 8080:8080 storeops-api
  ```
  Docker isn't installed on the development machine (Journal.md), so Docker evidence can't be produced locally yet.
- **Coverage** (`coverlet.collector`): `dotnet test StoreOps.sln --collect:"XPlat Code Coverage"` writes `tests/StoreOps.Api.Tests/TestResults/<run-id>/coverage.cobertura.xml`. Baseline on 2026-10-02: service 92.2%, controllers 100%, shared 94.4%, overall 96.3%. All are above the COV-01..COV-04 thresholds; scope rules are in architecture-principles.

## 7. Why the harness exists

The harness (`.harness/`: `agents/`, `skills/`, `reviews/`, `output/`) makes AI-generated StoreOps changes prove that they follow the project's architecture and business rules, instead of relying on code that only compiles and returns plausible status codes.

The AI-Native Architect Build Track case study recorded **four failure modes** in AI-generated code. Each is mapped below to the StoreOps rule that now guards against it and the evidence that detects it.

| # | Original AI failure mode | What it looks like in StoreOps | Guarding rules | Detected by |
|---|---|---|---|---|
| 1 | **Direct imports from another module's repository**, bypassing the agreed service boundary and event bus. | `ProgrammeService` taking `IStaffRepository`, or `using StoreOps.Api.Modules.Staff.Persistence;` outside Staff. | ARCH-02, ARCH-03 | `No_module_references_another_modules_repository`, `No_source_file_imports_another_modules_persistence_namespace`, `Cross_module_constructor_dependencies_are_read_only_lookups` |
| 2 | **Raw `Error` throws in service methods**, bypassing the typed `AppError` hierarchy. | `throw new InvalidOperationException("Activity not found")` in `ActivityService`, which becomes a 500 instead of `404 ACTIVITY_NOT_FOUND`. | ARCH-05 | `grep -rnP "throw new (?!(ValidationError\|NotFoundError\|ConflictError)\b)"` prints nothing; error-path tests assert `error.Code` via `ReadErrorAsync` |
| 3 | **Tests that asserted HTTP status codes but did not verify business-rule compliance.** | A conflict test checking only `409` without proving the closed activity is unchanged. | TEST-01, COV-01..04 (coverage only counts together with TEST-01 assertions) | TEST-01 checks A–D: the status-only detector script, the range-check grep, and review of collection asserts and new tests |
| 4 | **Missing event bus integration**, with state changes written directly to sibling module repositories. | `ActivityService` calling `IAlertService.RaiseAsync` or writing to `IAlertRepository` instead of publishing `ActivityCreated`. | ARCH-04, ARCH-02, ARCH-07 | `ActivitiesEndpointsTests.*publishes_event`, `AlertsEndpointsTests.High_priority_activity_raises_alert_via_event_bus`, `InMemoryEventBusTests`, plus the ARCH-02/03 checks above |

Failure mode 3 has already happened once in this repository. The first generated baseline had `Get_alerts_filters_by_severity`, which ran `Assert.All` over an alert list that could never contain a Critical alert, so it always passed. Five other tests checked status codes without checking state. The 2026-10-02 baseline audit found them, and they were rewritten to assert stored state, error codes, exact counts and published events. Treat any new test that would pass with the feature deleted as a defect.

The rule tests are themselves tested: `ModuleRulesSelfTests` runs every rule against the deliberately broken modules in `Architecture/ViolationFixtures.cs`. A rule whose self-test fails gives no evidence.

## 8. Out of scope for the baseline

- The SLA-breach demonstration feature isn't implemented. Don't add it unless a sprint explicitly asks for it. `Activity.DueAt` already exists, but nothing evaluates it yet.
- No persistence beyond memory, no authentication, no multi-store routing beyond the `storeId` field and filter.
