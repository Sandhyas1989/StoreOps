STATUS: ACCEPTED
# S7: Filter alerts by recipient, FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Alerts.
- **Changed endpoint:** baseline #9, `GET /api/alerts`, gets an optional `recipientStaffId` query parameter.
- **Changed files:**
  - `Modules/Alerts/AlertsController.cs`
  - `Modules/Alerts/AlertService.cs` (`ListAsync(severity, recipientStaffId, ct)`)
  - `tests/StoreOps.Api.Tests/Alerts/SlaAlertsEndpointsTests.cs`

## 2. Requirements
REQ-12, REQ-13

## 3. Acceptance criteria
```
AC-7.01  Traces: REQ-12  Rules: TEST-01
GIVEN  Clock = 09:00Z; POST /api/activities A {storeId "STORE-001", High, dueAt 08:45Z} (raises the HIGH_PRIORITY_ACTIVITY alert, no recipient); evaluation at 09:00Z (SLA_BREACH alert, recipient StaffSeed.ShiftLeadId); Clock.SetUtcNow(09:15:00.000Z) and evaluation again (SLA_ESCALATION alert, recipient StaffSeed.StoreManagerId)
WHEN   GET /api/alerts?recipientStaffId={StaffSeed.ShiftLeadId}
THEN   status 200 with exactly the one SLA_BREACH alert
AND    GET /api/alerts?recipientStaffId={StaffSeed.StoreManagerId}&severity=Warning returns []
AND    GET /api/alerts?recipientStaffId={a new Guid} returns []
AND    GET /api/alerts (no filter) returns all 3
EVIDENCE  SlaAlertsEndpointsTests.Alerts_filter_by_recipient_and_combine_with_severity

AC-7.02  Traces: REQ-12  Rules: ARCH-05, TEST-01
GIVEN  any state
WHEN   GET /api/alerts?recipientStaffId=not-a-guid
THEN   status 400, code = ErrorCodes.ValidationFailed, details contains the key "recipientStaffId"
EVIDENCE  SlaAlertsEndpointsTests.Malformed_recipient_filter_returns_validation_error

AC-7.03  Traces: REQ-13  Rules: all
GIVEN  the S7 changes
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; all tests pass, including the existing Get_alerts_filters_by_severity unchanged; the Swagger test still lists GET /api/alerts; architecture filter passes; COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-05 (query binding errors).

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
S6

## 7. Out of scope
OOS-03. Pagination of alerts.

## 8. Risks
RISK-03

## 9. Assumptions
None beyond the spec.

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
