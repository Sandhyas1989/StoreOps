STATUS: ACCEPTED
# S2: SLA evaluation endpoint detects breaches, FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Activities.
- **New endpoint:** `POST /api/activities/sla-evaluations`. No event yet.
- **New files:**
  - `Modules/Activities/ActivitySlaController.cs` (route `api/activities/sla-evaluations`, depends on `ISlaEvaluationService`)
  - `Modules/Activities/SlaEvaluationService.cs` (`ISlaEvaluationService`; depends on `IActivityRepository`, `ISlaRecordRepository` and `TimeProvider`)
  - `Modules/Activities/SlaContracts.cs` (`SlaStage { Breach, Escalation }`, `SlaEvaluationResponse(EvaluatedAt, NewlyBreached, NewlyEscalated)`)
  - `Modules/Activities/Persistence/SlaRecordRepository.cs` (`ISlaRecordRepository.TryAdvanceStageAsync(activityId, stage, at)` returns `bool`, atomic via `ConcurrentDictionary`)
  - `tests/StoreOps.Api.Tests/Activities/SlaEvaluationEndpointsTests.cs`
- **Changed files:**
  - `Modules/Activities/ActivitiesModule.cs`
  - `tests/StoreOps.Api.Tests/StoreOps.Api.Tests.csproj` (DEP-01)
  - `tests/StoreOps.Api.Tests/Infrastructure/StoreOpsApiFactory.cs`: exposes `Clock` (`FakeTimeProvider`, start `2026-10-01T09:00:00Z`, `AutoAdvanceAmount = 1 ms`)

**Test clock convention (applies to S2–S7).**
- `Clock` starts at 09:00:00Z and only moves forward, via `Clock.SetUtcNow(t)` or `Clock.Advance(d)`; `FakeTimeProvider` rejects earlier times.
- Every read adds 1 ms, so an AC never asserts an exact "now". It asserts that a timestamp lies between clock readings taken just before and just after the call, or that it equals another value from the same call.
- **Overdue within grace:** `dueAt 08:45` (default grace ends 09:15).
- **Overdue past grace:** `dueAt 08:00` (grace ended 08:30). Use it only where escalation is expected.
- **Due later:** `dueAt 10:00Z`. For boundary tests, use an explicit time such as `09:05:00Z`, with evaluations set to 100 ms either side of it.

## 2. Requirements
REQ-01, REQ-02, REQ-06, REQ-13

## 3. Acceptance criteria
```
AC-2.01  Traces: REQ-01, REQ-02  Rules: ARCH-01, TEST-01
GIVEN  Clock = 09:00Z; activity A {priority High, status Pending, dueAt 08:45Z}
WHEN   t0 = Clock.GetUtcNow(); POST /api/activities/sla-evaluations; t1 = Clock.GetUtcNow()
THEN   status 200, newlyBreached = [A.id], newlyEscalated = []
AND    t0 < evaluatedAt < t1
AND    GET /api/activities/{A.id} equals A as created (evaluation changes no activity fields)
EVIDENCE  SlaEvaluationEndpointsTests.Evaluation_reports_overdue_high_priority_activity_as_breached

AC-2.02  Traces: REQ-01  Rules: TEST-01
GIVEN  Clock = 09:00Z; activities with dueAt 08:45Z: H (High), K (Critical), L (Low), M (Medium)
WHEN   POST /api/activities/sla-evaluations
THEN   newlyBreached contains exactly H.id and K.id
EVIDENCE  SlaEvaluationEndpointsTests.Evaluation_breaches_high_and_critical_and_ignores_low_and_medium

AC-2.03  Traces: REQ-01  Rules: TEST-01
GIVEN  Clock = 09:00Z; High activities with dueAt 08:45Z: P (Pending), I (InProgress), C (Completed), X (Cancelled); C and X reach their status through PATCH (200)
WHEN   POST /api/activities/sla-evaluations
THEN   newlyBreached contains exactly P.id and I.id
EVIDENCE  SlaEvaluationEndpointsTests.Evaluation_ignores_completed_and_cancelled_activities

AC-2.04  Traces: REQ-01  Rules: TEST-01
GIVEN  Clock = 09:00Z; High activities: E (dueAt 09:05:00Z), F (dueAt 10:00:00Z), N (no dueAt)
WHEN   POST /api/activities/sla-evaluations at Clock.SetUtcNow(09:04:59.900Z), at Clock.SetUtcNow(09:05:00.100Z), and at Clock.SetUtcNow(10:00:00.100Z)
THEN   first response newlyBreached = []
AND    second response newlyBreached = [E.id]
AND    third response newlyBreached = [F.id] (N never appears in any response)
EVIDENCE  SlaEvaluationEndpointsTests.Evaluation_breaches_only_after_due_time_passes

AC-2.05  Traces: REQ-02, REQ-06  Rules: TEST-01
GIVEN  Clock = 09:00Z; activity A {High, Pending, dueAt 08:45Z} already reported in newlyBreached by an earlier evaluation
WHEN   POST /api/activities/sla-evaluations again (Clock still before 09:15Z)
THEN   status 200, newlyBreached = []
EVIDENCE  SlaEvaluationEndpointsTests.Repeated_evaluation_does_not_report_same_breach_twice

AC-2.06  Traces: REQ-06  Rules: TEST-01
GIVEN  Clock = 09:00Z; one activity A {High, Pending, dueAt 08:45Z}, never evaluated
WHEN   10 POST /api/activities/sla-evaluations calls run concurrently (Task.WhenAll)
THEN   A.id appears in newlyBreached of exactly one response
EVIDENCE  SlaEvaluationEndpointsTests.Concurrent_evaluations_report_breach_exactly_once

AC-2.07  Traces: REQ-13  Rules: all
GIVEN  the S2 changes, including the FakeTimeProvider swap (RISK-01)
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; every pre-existing test still passes unchanged; architecture filter passes (ActivitySlaController → ISlaEvaluationService → ISlaRecordRepository satisfies ARCH-01); COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-01 (a new controller, service and repository).

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
- DEP-01, DEP-03.
- S1, because REQ-01 includes Critical (Q1 = yes).

## 7. Out of scope
Event publishing (S3), escalation (S4), worker (S5), alerts (S6). OOS-04, OOS-08.

## 8. Risks
RISK-01, RISK-02

## 9. Assumptions
ASM-02, ASM-07

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
