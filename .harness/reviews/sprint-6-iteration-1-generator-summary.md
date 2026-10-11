STATUS: READY FOR EVALUATION
# Generator summary: S6, iteration 1, FEAT-sla-breach-alerting

## 1. Files changed
| Path | New/Changed | ACs / reason |
|---|---|---|
| src/StoreOps.Api/Modules/Alerts/SlaBreachAlertHandler.cs | New | AC-6.01..6.04: `IEventHandler<ActivitySlaBreached>`; Breach goes to the store's ShiftLead(s) as SLA_BREACH/Warning, Escalation to the store's StoreManager(s) as SLA_ESCALATION/Critical; uses `IAlertService` and `IStaffLookup` only |
| src/StoreOps.Api/Modules/Alerts/Alert.cs | Changed | `RecipientStaffId`, `RecipientRole`, `AlertTypes.SlaBreach`, `AlertTypes.SlaEscalation` |
| src/StoreOps.Api/Modules/Alerts/AlertContracts.cs | Changed | recipient fields on `RaiseAlertCommand` (optional, default null, so existing callers and AlertsEndpointsTests are unchanged) and `AlertResponse` |
| src/StoreOps.Api/Modules/Alerts/AlertService.cs | Changed | `RaiseAsync` now calls `TryAddAsync`, which returns the stored alert (idempotent per (Type, SourceId, RecipientStaffId)) |
| src/StoreOps.Api/Modules/Alerts/Persistence/AlertRepository.cs | Changed | `AddAsync` replaced by atomic `TryAddAsync` (lock-guarded list) |
| src/StoreOps.Api/Modules/Alerts/AlertsModule.cs | Changed | registers `IEventHandler<ActivitySlaBreached>` |
| tests/StoreOps.Api.Tests/Alerts/SlaAlertsEndpointsTests.cs | New | one test per AC-6.01..6.04 |

## 2. AC self-check
| AC | EVIDENCE test | Result | Notes |
|---|---|---|---|
| AC-6.01 | SlaAlertsEndpointsTests.Breach_creates_warning_alert_for_store_shift_lead | PASS | exactly one SLA_BREACH, all fields asserted |
| AC-6.02 | SlaAlertsEndpointsTests.Breach_in_store_without_shift_lead_creates_no_alert | PASS | one Breach event asserted; the HIGH_PRIORITY_ACTIVITY alert from creation is asserted and unaffected |
| AC-6.03 | SlaAlertsEndpointsTests.Escalation_creates_critical_alert_for_store_manager | PASS | 2 SLA alerts total |
| AC-6.04 | SlaAlertsEndpointsTests.Duplicate_breach_events_create_one_alert_per_recipient | PASS | event re-published twice via IEventBus, 3 more evaluations |
| AC-6.05 | section 3 commands | PASS | see below |

## 3. Commands and results
build: 0 warnings / 0 errors; tests: 92/92 passed (Failed 0, Skipped 0); architecture: 18/18 passed (includes Module_dependency_graph_has_no_cycles with the new Alerts -> Staff edge)
COV-01..04: 94.1% (253/269) / 100.0% (41/41) / 94.4% (101/107) / 96.4% (717/744) (thresholds 80/70/60/70). Computed from the newest cobertura report, unique (file, line) pairs, nested classes folded.
TEST-01 check A output:
```
SwaggerTests.cs: Swagger_is_not_exposed_outside_development
InMemoryEventBusTests.cs: Publish_without_handlers_completes
```
(only the two allowlisted tests)
TEST-01 check B output: empty.
ARCH-05 greps (macOS grep has no -P; grep 1 was run with an equivalent perl negative-lookahead one-liner over all non-obj .cs files):
- grep 1 (throw new not AppError): empty
- grep 2 (controller NotFound/BadRequest/...): empty
- grep 3 (catch in controllers/services): empty
- worker check (catch in Modules): only SlaEvaluationWorker.cs:23 (`OperationCanceledException` when cancelled) and :37 (`Exception ex when not OperationCanceledException`), both unchanged from S5
- `throw (?!new)\w+(` hits (perl; the line numbers printed are cumulative across files, not per file): ActivityService NotFound x2, ProgrammeService StaffIdError x2, all existing AppError-returning helpers
- `ExcludeFromCodeCoverage` in src: none. No `!` operators added in Alerts.

## 4. Known gaps
- None known. Assumptions: (a) one alert per matching staff member in the event's store (the seed has exactly one ShiftLead and one StoreManager in STORE-001); (b) dedupe applies only when both SourceId and RecipientStaffId are non-null, so existing recipient-less alerts and the unkeyed seeding in AlertsEndpointsTests keep their behaviour; (c) `AddAsync` on `IAlertRepository` was replaced by `TryAddAsync` (no other callers existed); (d) the new handler is not registered with a recording handler in StoreOpsApiFactory (not needed; ActivitySlaBreached already recorded). Contract line 1 set to STATUS: IN PROGRESS before editing. No file outside contract section 1 was changed.
