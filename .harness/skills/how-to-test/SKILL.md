---
name: how-to-test
description: How StoreOps tests are written and run (xUnit, WebApplicationFactory via ApiTestBase/StoreOpsApiFactory, TestJson helpers, EventRecorder, unit tests for Shared), with the business-rule, negative-path and event-payload assertion patterns TEST-01 requires and the COV-01..04 coverage gates. Load for every sprint.
---

# How to test StoreOps

## Layout and naming
- **Location:** `tests/StoreOps.Api.Tests/<Module>/<Name>Tests.cs` (`Activities/ActivitiesEndpointsTests.cs`). Shared unit tests go in `Shared/`, rule tests in `Architecture/`, infrastructure in `Infrastructure/`.
- **Class:** `public sealed class <Name>Tests : ApiTestBase`. `using Xunit` is global.
- **Method names** follow `<Action>_<condition>_<outcome>`, for example `Patch_closed_activity_returns_conflict_and_leaves_activity_unchanged`. The name must match the AC's `EVIDENCE` line exactly.
- **Body:** arrange, act and assert, separated by blank lines. Use `[Theory]` + `[InlineData]` for variants of one rule (`Post_activity_with_invalid_body_returns_sanitised_body_error`).

## Infrastructure (`tests/StoreOps.Api.Tests/Infrastructure`)
| Type | Use |
|---|---|
| `ApiTestBase` | A new `StoreOpsApiFactory` and `HttpClient` **per test**, so stores start empty and counts are exact. Exposes `Factory` and `Client`. |
| `StoreOpsApiFactory` | `WebApplicationFactory<Program>`, environment `Testing`. Registers recording handlers for `ActivityCreated`, `ActivityDeleted` and `ProgrammeMemberAdded`. Exposes `Events` (an `EventRecorder`). Register new events here too. |
| `EventRecorder` / `RecordingEventHandler<T>` | `Factory.Events.Of<TEvent>()` returns the published events, in order. |
| `TestJson` | `Options` (web defaults + string enums); `response.ReadAsync<T>()`; `response.ReadErrorAsync(HttpStatusCode)`, which asserts the status **and** `body.statusCode`; `Client.CreateActivityAsync(title, priority)` |

- **Seeding through a service:** create a scope and call the module's service (`AlertsEndpointsTests.RaiseAsync`), not a repository:
  ```csharp
  using var scope = Factory.Services.CreateScope();
  await scope.ServiceProvider.GetRequiredService<IAlertService>().RaiseAsync(new RaiseAlertCommand("TEST_ALERT", severity, "…", "tests", SourceId: null), CancellationToken.None);
  ```
- **Changing configuration or services for one test:** `Factory.WithWebHostBuilder(b => b.UseEnvironment(...) / b.UseSetting(...))` (see `SwaggerTests`). Dispose the root factory.
- **Unit tests for Shared code** build a `ServiceCollection` directly and use `NullLogger<T>.Instance` (`InMemoryEventBusTests`).
- **Controllable time** (approved S2): `Microsoft.Extensions.TimeProvider.Testing` `FakeTimeProvider`, exposed as `Factory.Clock`, starting at 09:00:00Z with `AutoAdvanceAmount = 1 ms`. It moves forward only. Follow the test clock convention in `sprint-2-contract.md` §1. Never use `Thread.Sleep` or `Task.Delay` to wait for time.

## Required assertions (TEST-01)
**Business rule:** assert the resulting state, not just the response.
```csharp
var updated = await response.ReadAsync<ActivityResponse>();
Assert.Equal(ActivityStatus.InProgress, updated.Status);
Assert.Equal(updated, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options));
```

**Negative path:** assert the code constant and details, the unchanged state, and that no event was published.
```csharp
var error = await response.ReadErrorAsync(HttpStatusCode.Conflict);
Assert.Equal(ActivityService.ClosedCode, error.Code);
Assert.Equal(closed, await Client.GetFromJsonAsync<ActivityResponse>($"/api/activities/{created.Id}", TestJson.Options));
// validation: Assert.Equal(["storeId", "title"], error.Details!.Keys.Order());
// no side effect: Assert.Empty(Factory.Events.Of<ActivityDeleted>());
```

**Event payload:** use exactly one event and check every field.
```csharp
var published = Assert.Single(Factory.Events.Of<ProgrammeMemberAdded>());
Assert.Equal(programme.Id, published.ProgrammeId);
Assert.Equal(StaffSeed.AssociateId, published.StaffId);
```

**Cross-module effect:** observe it through the consumer's endpoint (`High_priority_activity_raises_alert_via_event_bus`).

**Filters:** seed both a matching and a non-matching item, then assert the exact result:
```csharp
Assert.Equal(3, all.Count);
Assert.Equal(critical.Id, Assert.Single(filtered).Id);
```

**Forbidden:**
- status-only tests (outside the TEST-01 allowlist);
- `Assert.True(x >= n)`;
- `Assert.All` or `Assert.DoesNotContain` over a collection the test never proved non-empty;
- string-literal error codes;
- comparing exact "now" timestamps;
- skipped tests (`Skip =`) or deleted assertions from earlier sprints.

## Running and evidence
```bash
dotnet build StoreOps.sln                                                        # 0 Warning(s), 0 Error(s)
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
dotnet test StoreOps.sln --filter "FullyQualifiedName~SlaEvaluationEndpointsTests"   # one class while iterating
```
- **Coverage gates** (hard, from the use case; computed as specified in architecture-principles COV-01..04):
  - service layer (`*Service`, `*Handler` in Modules): **≥ 80%**
  - controllers: **≥ 70%**
  - Shared: **≥ 60%**
  - overall `StoreOps.Api`: **≥ 70%**
- **Baseline (2026-10-02):** 92.2 / 100 / 94.4 / 96.3%, 67 tests. Report the new values each sprint.
- **TEST-01 checks:** run checks A (the status-only detector script) and B (the range-check grep) from architecture-principles.
- **Locked build output:** if the build fails with `MSB3027` (file locked), a running `StoreOps.Api` holds `bin/`. Stop it, or build with `--artifacts-path .verify-artifacts` inside the repo and delete that folder afterwards. Don't report that as a code failure.
