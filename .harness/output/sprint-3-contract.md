STATUS: APPROVED
# S3: Publish ActivitySlaBreached (Breach), FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Activities.
- **New event:** `ActivitySlaBreached(ActivityId, Title, StoreId, Priority, DueAt, AssignedStaffId, Stage, OccurredAt) : IIntegrationEvent`, added to `Modules/Activities/ActivityEvents.cs`.
- **Changed:**
  - `Modules/Activities/SlaEvaluationService.cs`: takes `IEventBus`; publishes only when `TryAdvanceStageAsync` returns true.
  - `tests/StoreOps.Api.Tests/Infrastructure/StoreOpsApiFactory.cs`: records `ActivitySlaBreached`.
  - `tests/StoreOps.Api.Tests/Activities/SlaEvaluationEndpointsTests.cs`.

## 2. Requirements
REQ-03, REQ-06, REQ-13

## 3. Acceptance criteria
```
AC-3.01  Traces: REQ-03  Rules: ARCH-04, TEST-01
GIVEN  Clock = 09:00Z; activity A {title "Freezer temp log", storeId "STORE-001", priority High, dueAt 08:45Z, assignedStaffId StaffSeed.AssociateId}
WHEN   POST /api/activities/sla-evaluations
THEN   exactly one ActivitySlaBreached with ActivityId = A.id, Title = A.title, StoreId = "STORE-001", Priority = High, DueAt = 08:45Z, AssignedStaffId = StaffSeed.AssociateId, Stage = Breach, OccurredAt = response.evaluatedAt
EVIDENCE  SlaEvaluationEndpointsTests.Breach_publishes_event_with_full_payload

AC-3.02  Traces: REQ-03, REQ-06  Rules: ARCH-04, TEST-01
GIVEN  Clock = 09:00Z; activity A {High, Pending, dueAt 08:45Z}
WHEN   POST /api/activities/sla-evaluations 3 times sequentially, then 10 times concurrently (Task.WhenAll), all before 09:15Z
THEN   Factory.Events.Of<ActivitySlaBreached>() has exactly one event with ActivityId = A.id and Stage = Breach
EVIDENCE  SlaEvaluationEndpointsTests.Breach_event_is_published_once_across_repeated_and_concurrent_evaluations

AC-3.03  Traces: REQ-03  Rules: TEST-01
GIVEN  Clock = 09:00Z; activities: Low with dueAt 08:45Z, High Completed (via PATCH) with dueAt 08:45Z, High with dueAt 10:00Z, High without dueAt
WHEN   POST /api/activities/sla-evaluations
THEN   status 200, newlyBreached = []
AND    Factory.Events.Of<ActivitySlaBreached>() is empty
EVIDENCE  SlaEvaluationEndpointsTests.Non_qualifying_activities_publish_no_breach_event

AC-3.04  Traces: REQ-13  Rules: all
GIVEN  the S3 changes
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; all tests pass; architecture filter passes (event declared in Activities; no cross-module reference added); COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-04.

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
S2, DEP-01

## 7. Out of scope
Consumers of the event (S6). Escalation stage (S4).

## 8. Risks
RISK-02

## 9. Assumptions
ASM-08

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
