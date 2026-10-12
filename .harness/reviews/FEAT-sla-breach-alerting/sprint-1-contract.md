STATUS: ACCEPTED
# S1: Accept Critical activity priority, FEAT-sla-breach-alerting
Q1 answered yes (2026-10-03): Critical priority is in scope.

## 1. Scope
- **Owning module:** Activities.
- **Changed:** `src/StoreOps.Api/Modules/Activities/Activity.cs` (`ActivityPriority.Critical`), `tests/StoreOps.Api.Tests/Activities/ActivitiesEndpointsTests.cs`.
- **Endpoints affected:** baseline #2 and #4 (an additional enum value). No new endpoint or event.

## 2. Requirements
REQ-08, REQ-13

## 3. Acceptance criteria
```
AC-1.01  Traces: REQ-08  Rules: TEST-01
GIVEN  no activities
WHEN   POST /api/activities {title:"Cold-chain check", storeId:"STORE-001", priority:"Critical"}
THEN   status 201, body.priority = "Critical"
AND    GET /api/activities/{body.id} returns priority = "Critical"
AND    exactly one ActivityCreated with Priority = Critical
EVIDENCE  ActivitiesEndpointsTests.Post_activity_with_critical_priority_stores_and_publishes_critical

AC-1.02  Traces: REQ-08  Rules: TEST-01
GIVEN  a Pending activity with priority Low
WHEN   PATCH /api/activities/{id} {priority:"Critical"}
THEN   status 200, body.priority = "Critical", title and status unchanged
AND    GET /api/activities/{id} returns priority = "Critical"
EVIDENCE  ActivitiesEndpointsTests.Patch_activity_to_critical_priority_updates_priority_only

AC-1.03  Traces: REQ-08  Rules: ARCH-05, TEST-01
GIVEN  no activities
WHEN   POST /api/activities {title:"x", storeId:"STORE-001", priority:"Urgent"}
THEN   status 400, code = ErrorCodes.ValidationFailed, details.priority = [ModelStateErrorFactory.ValueInvalidMessage]
AND    GET /api/activities returns []
AND    no ActivityCreated published
EVIDENCE  ActivitiesEndpointsTests.Post_activity_with_unknown_enum_value_reports_field_without_type_names (extended with the state and event asserts)

AC-1.04  Traces: REQ-13  Rules: all
GIVEN  the S1 changes
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; all tests pass; architecture filter passes; COV-01..04 at or above thresholds, with the values recorded
EVIDENCE  §5 command output
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-05 (enum binding errors).

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
None.

## 7. Out of scope
OOS-07 (`HIGH_PRIORITY_ACTIVITY` alert stays High-only). No changes to report counts.

## 8. Risks
RISK-03 (new enum value visible to baseline clients).

## 9. Assumptions
ASM-01

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
