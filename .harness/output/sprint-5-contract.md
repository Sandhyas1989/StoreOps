STATUS: APPROVED
# S5: Background worker runs SLA evaluation automatically, FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Activities. No new endpoint or event.
- **New:**
  - `Modules/Activities/SlaEvaluationWorker.cs`: a `BackgroundService` using a `PeriodicTimer` driven by `TimeProvider`. It creates a DI scope on each tick and calls `ISlaEvaluationService`. It must not be named `*Service` (RISK-07).
  - `tests/StoreOps.Api.Tests/Activities/SlaEvaluationWorkerTests.cs`
- **Changed:**
  - `Modules/Activities/SlaOptions.cs` (`EvaluationInterval`, default `00:01:00`, must be `> 0`; `WorkerEnabled`, default `true`)
  - `Modules/Activities/ActivitiesModule.cs` (register the worker)
  - `src/StoreOps.Api/appsettings.json`
  - `tests/StoreOps.Api.Tests/Infrastructure/StoreOpsApiFactory.cs`: sets `Sla:WorkerEnabled=false` by default so endpoint tests stay deterministic

## 2. Requirements
REQ-07, REQ-13

## 3. Acceptance criteria
```
AC-5.01  Traces: REQ-07  Rules: ARCH-01, TEST-01
GIVEN  factory with Sla:WorkerEnabled = true, Sla:EvaluationInterval = 00:01:00; Clock = 09:00Z; activity A {High, Pending, dueAt 08:45Z}; no evaluation endpoint called
WHEN   Clock.Advance(1 minute)
THEN   polling Factory.Events every 50 ms for at most 5 s of wall time, Factory.Events.Of<ActivitySlaBreached>() for A becomes exactly [Stage = Breach]; no Escalation, because grace runs until 09:15Z
EVIDENCE  SlaEvaluationWorkerTests.Worker_publishes_breach_on_each_interval_tick

AC-5.02  Traces: REQ-07  Rules: TEST-01
GIVEN  factory with Sla:WorkerEnabled = false; Clock = 09:00Z; activity A {High, Pending, dueAt 08:45Z}
WHEN   Clock.Advance(1 minute) 5 times
THEN   Factory.Services.GetServices<IHostedService>() contains no SlaEvaluationWorker (so no background evaluation can run; no wait is needed)
AND    Factory.Events.Of<ActivitySlaBreached>() is empty
AND    a subsequent POST /api/activities/sla-evaluations returns newlyBreached = [A.id] (the stage wasn't consumed)
EVIDENCE  SlaEvaluationWorkerTests.Disabled_worker_does_not_evaluate

AC-5.03  Traces: REQ-13  Rules: all
GIVEN  the S5 changes
WHEN   the §5 commands run 3 times in a row
THEN   all 3 runs pass with 0 warnings and 0 errors (flakiness check for RISK-04); architecture filter passes; COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output (3 runs)
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-01 (naming of the worker).

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
S4, DEP-01

## 7. Out of scope
Distributed or multi-instance scheduling. Worker health metrics.

## 8. Risks
RISK-02, RISK-04, RISK-07

## 9. Assumptions
A 1-minute default interval is acceptable (part of Q3).

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
