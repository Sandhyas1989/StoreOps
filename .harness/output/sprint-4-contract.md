STATUS: APPROVED
# S4: Escalation after the configurable grace period, FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Activities.
- **Event change:** the existing `ActivitySlaBreached` event is now also published with `Stage = Escalation`.
- **New:**
  - `Modules/Activities/SlaOptions.cs` (`EscalationGracePeriod`, default `00:30:00`, validated `> 00:00:00`)
  - `tests/StoreOps.Api.Tests/Activities/SlaOptionsTests.cs`
- **Changed:**
  - `Modules/Activities/SlaEvaluationService.cs`
  - `Modules/Activities/ActivitiesModule.cs` (`AddOptions<SlaOptions>().BindConfiguration("Sla").Validate(...).ValidateOnStart()`)
  - `src/StoreOps.Api/appsettings.json` (`"Sla": { "EscalationGracePeriod": "00:30:00" }`)
  - `tests/StoreOps.Api.Tests/Activities/SlaEvaluationEndpointsTests.cs`

## 2. Requirements
REQ-02, REQ-04, REQ-05, REQ-06, REQ-13

## 3. Acceptance criteria
```
AC-4.01  Traces: REQ-02, REQ-04  Rules: ARCH-04, TEST-01
GIVEN  Clock = 09:00Z; activity A {High, Pending, dueAt 09:10:00Z}; at Clock.SetUtcNow(09:11:00Z) an evaluation returns newlyBreached = [A.id]
WHEN   Clock.SetUtcNow(09:40:00.000Z) (dueAt + 30m); POST /api/activities/sla-evaluations
THEN   status 200, newlyEscalated = [A.id], newlyBreached = []
AND    exactly one ActivitySlaBreached with ActivityId = A.id, Stage = Escalation, OccurredAt = response.evaluatedAt, and other fields equal to the Breach event's
EVIDENCE  SlaEvaluationEndpointsTests.Escalates_open_breach_when_grace_period_elapses

AC-4.02  Traces: REQ-04  Rules: TEST-01
GIVEN  A {High, Pending, dueAt 09:10:00Z} at stage Breach (evaluated at 09:11:00Z)
WHEN   Clock.SetUtcNow(09:39:59.900Z); POST /api/activities/sla-evaluations
THEN   newlyEscalated = []
AND    no ActivitySlaBreached with Stage = Escalation
EVIDENCE  SlaEvaluationEndpointsTests.Does_not_escalate_before_grace_period_elapses

AC-4.03  Traces: REQ-04  Rules: TEST-01
GIVEN  A {High, Pending, dueAt 09:10:00Z} at stage Breach (evaluated at 09:11:00Z); at 09:12:00Z PATCH /api/activities/{A.id} {status:"Completed"} returns 200
WHEN   Clock.SetUtcNow(09:45:00Z); POST /api/activities/sla-evaluations
THEN   newlyEscalated = []
AND    no ActivitySlaBreached with Stage = Escalation
EVIDENCE  SlaEvaluationEndpointsTests.Does_not_escalate_activity_completed_after_breach

AC-4.04  Traces: REQ-04, REQ-06  Rules: TEST-01
GIVEN  A escalated as in AC-4.01 (at 09:40:00Z)
WHEN   POST /api/activities/sla-evaluations 3 times sequentially with Clock.Advance(1 hour) before each, then 10 times concurrently (Task.WhenAll)
THEN   exactly one Escalation event for A in total, and every response has newlyEscalated = []
EVIDENCE  SlaEvaluationEndpointsTests.Escalation_event_is_published_once

AC-4.05  Traces: REQ-05  Rules: TEST-01
GIVEN  factory configured with Sla:EscalationGracePeriod = 00:05:00; A {High, Pending, dueAt 09:10:00Z} at stage Breach (evaluated at 09:11:00Z)
WHEN   evaluations at Clock.SetUtcNow(09:14:59.900Z) and at Clock.SetUtcNow(09:15:00.000Z)
THEN   first newlyEscalated = []; second newlyEscalated = [A.id]
EVIDENCE  SlaEvaluationEndpointsTests.Grace_period_is_read_from_configuration

AC-4.06  Traces: REQ-05  Rules: ARCH-05, TEST-01
GIVEN  Sla:EscalationGracePeriod = "00:00:00" (and, in a second case, "-00:01:00")
WHEN   the test host starts (first CreateClient)
THEN   OptionsValidationException is thrown, and its message contains "EscalationGracePeriod"
EVIDENCE  SlaOptionsTests.Non_positive_grace_period_fails_startup (Theory, 2 cases)

AC-4.07  Traces: REQ-13  Rules: all
GIVEN  the S4 changes
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; all tests pass; architecture filter passes; COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output

AC-4.08  Traces: REQ-04  Rules: ARCH-04, TEST-01
GIVEN  activity B {High, Pending, dueAt 08:00Z} never evaluated; Clock = 09:00Z (past the grace period)
WHEN   POST /api/activities/sla-evaluations
THEN   newlyBreached = [B.id] and newlyEscalated = [B.id]
AND    the events for B are exactly [Stage = Breach, Stage = Escalation], in that order
EVIDENCE  SlaEvaluationEndpointsTests.First_evaluation_after_grace_publishes_breach_then_escalation
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
S3, DEP-01

## 7. Out of scope
OOS-05 (no reminders after Escalation). Alerts for either stage (S6).

## 8. Risks
- RISK-02.
- An invalid configuration fails at startup. That's an options-validation failure, not an `AppError`, because it isn't a request failure.

## 9. Assumptions
ASM-04

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
