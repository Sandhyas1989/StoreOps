STATUS: APPROVED
# S6: Alerts for Department Lead and Store Manager, deduplicated, FEAT-sla-breach-alerting

## 1. Scope
- **Owning module:** Alerts (consumer of `ActivitySlaBreached`). This adds the dependency edge `Alerts → Staff` via `IStaffLookup`.
- **New:**
  - `Modules/Alerts/SlaBreachAlertHandler.cs` (`IEventHandler<ActivitySlaBreached>`; depends on `IAlertService` and `IStaffLookup`)
  - `tests/StoreOps.Api.Tests/Alerts/SlaAlertsEndpointsTests.cs`
- **Changed:**
  - `Modules/Alerts/Alert.cs`: `RecipientStaffId: Guid?`, `RecipientRole: StaffRole?`, `AlertTypes.SlaBreach = "SLA_BREACH"`, `AlertTypes.SlaEscalation = "SLA_ESCALATION"`
  - `Modules/Alerts/AlertContracts.cs`: recipient fields on `RaiseAlertCommand` and `AlertResponse`
  - `Modules/Alerts/AlertService.cs`: idempotent raise keyed by (Type, SourceId, RecipientStaffId)
  - `Modules/Alerts/Persistence/AlertRepository.cs`: atomic `TryAddAsync`
  - `Modules/Alerts/AlertsModule.cs`

## 2. Requirements
REQ-09, REQ-10, REQ-11, REQ-13

## 3. Acceptance criteria
```
AC-6.01  Traces: REQ-09  Rules: ARCH-03, ARCH-04, TEST-01
GIVEN  Clock = 09:00Z; activity A {title "Freezer temp log", storeId "STORE-001", High, dueAt 08:45Z}
WHEN   POST /api/activities/sla-evaluations
THEN   when the response returns, GET /api/alerts holds exactly one alert with type = AlertTypes.SlaBreach and sourceId = A.id
AND    that alert has severity Warning, sourceModule "activities", recipientStaffId = StaffSeed.ShiftLeadId, recipientRole ShiftLead, and a message containing "Freezer temp log"
EVIDENCE  SlaAlertsEndpointsTests.Breach_creates_warning_alert_for_store_shift_lead

AC-6.02  Traces: REQ-09  Rules: TEST-01
GIVEN  Clock = 09:00Z; activity Z {storeId "STORE-999" (no staff), High, Pending, dueAt 08:45Z}
WHEN   POST /api/activities/sla-evaluations
THEN   exactly one ActivitySlaBreached (Stage Breach) for Z is published
AND    GET /api/alerts has no alert with type SLA_BREACH and sourceId = Z.id
EVIDENCE  SlaAlertsEndpointsTests.Breach_in_store_without_shift_lead_creates_no_alert

AC-6.03  Traces: REQ-10  Rules: ARCH-04, TEST-01
GIVEN  A as in AC-6.01 (dueAt 08:45Z), breached at 09:00Z with its SLA_BREACH alert
WHEN   Clock.SetUtcNow(09:15:00.000Z) (dueAt + 30m); POST /api/activities/sla-evaluations
THEN   GET /api/alerts holds exactly one alert with type = AlertTypes.SlaEscalation and sourceId = A.id: severity Critical, recipientStaffId = StaffSeed.StoreManagerId, recipientRole StoreManager
AND    alerts with sourceId = A.id and type ∈ {SLA_BREACH, SLA_ESCALATION} number exactly 2
EVIDENCE  SlaAlertsEndpointsTests.Escalation_creates_critical_alert_for_store_manager

AC-6.04  Traces: REQ-11  Rules: TEST-01
GIVEN  Clock = 09:00Z; A {storeId "STORE-001", High, Pending, dueAt 08:45Z} breached by one evaluation; E = the recorded ActivitySlaBreached (Stage Breach) for A
WHEN   E is published twice more via IEventBus in a test scope, and evaluations then run 3 more times (all before 09:15Z)
THEN   GET /api/alerts holds exactly one SLA_BREACH alert for (A.id, StaffSeed.ShiftLeadId)
EVIDENCE  SlaAlertsEndpointsTests.Duplicate_breach_events_create_one_alert_per_recipient

AC-6.05  Traces: REQ-13  Rules: all
GIVEN  the S6 changes
WHEN   the §5 commands run
THEN   0 warnings and 0 errors; all tests pass, including the existing AlertsEndpointsTests unchanged (the HIGH_PRIORITY_ACTIVITY behaviour is preserved); the architecture filter passes, including Module_dependency_graph_has_no_cycles with the new Alerts → Staff edge; COV-01..04 recorded and at or above thresholds
EVIDENCE  §5 command output
```

## 4. Gates
ARCH-01..07, TEST-01, COV-01..04 (all hard). This sprint exercises ARCH-03 (`IStaffLookup` only), ARCH-04 (handler owned by Alerts) and ARCH-07 (new edge).

## 5. Verification commands
```
dotnet build StoreOps.sln
rm -rf tests/StoreOps.Api.Tests/TestResults && dotnet test StoreOps.sln --collect:"XPlat Code Coverage"
dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"
```
Then run TEST-01 checks A and B from architecture-principles.

## 6. Dependencies
S4 (both stages are published), DEP-02

## 7. Out of scope
OOS-01, OOS-02, OOS-03, OOS-07. Recipient filtering (S7).

## 8. Risks
RISK-02, RISK-03, RISK-05

## 9. Assumptions
ASM-03, ASM-05, ASM-06

## 10. Definition of done
- The EVIDENCE for all ACs passes.
- All gates PASS.
- No file outside §1 changed.
