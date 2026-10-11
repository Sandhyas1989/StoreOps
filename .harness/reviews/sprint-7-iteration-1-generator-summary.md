STATUS: READY FOR EVALUATION
# Generator summary: S7, iteration 1, FEAT-sla-breach-alerting
## 1. Files changed
| Path | New/Changed | ACs / reason |
|---|---|---|
| src/StoreOps.Api/Modules/Alerts/AlertsController.cs | Changed | AC-7.01, AC-7.02: optional `[FromQuery] Guid? recipientStaffId`, passed to the service; a malformed value is rejected by model binding via ModelStateErrorFactory |
| src/StoreOps.Api/Modules/Alerts/AlertService.cs | Changed | AC-7.01: `ListAsync(severity, recipientStaffId, ct)` on IAlertService and AlertService; filters on `RecipientStaffId` with AND semantics alongside severity |
| tests/StoreOps.Api.Tests/Alerts/SlaAlertsEndpointsTests.cs | Changed | AC-7.01, AC-7.02: two new tests; `GetAlertsAsync` gained an optional query-string parameter; added `using StoreOps.Api.Shared.Errors` |

Contract line 1 set to `STATUS: IN PROGRESS`. No other file changed.

## 2. AC self-check
| AC | EVIDENCE test | Result (PASS/FAIL/NOT RUN) | Notes |
|---|---|---|---|
| AC-7.01 | SlaAlertsEndpointsTests.Alerts_filter_by_recipient_and_combine_with_severity | PASS | Seeds the HIGH_PRIORITY_ACTIVITY (no recipient), SLA_BREACH (ShiftLead) and SLA_ESCALATION (StoreManager) alerts. Asserts 3 unfiltered; exactly the SLA_BREACH for ShiftLead; `[]` for StoreManager+Warning; `[]` for a new Guid. It also asserts StoreManager+Critical returns the SLA_ESCALATION, so the empty Warning result is not vacuous. |
| AC-7.02 | SlaAlertsEndpointsTests.Malformed_recipient_filter_returns_validation_error | PASS | 400, `ErrorCodes.ValidationFailed`, Details contains `recipientStaffId` |
| AC-7.03 | §5 command output | PASS | See §3. Existing `Get_alerts_filters_by_severity` is unchanged and passes; the Swagger tests pass |

## 3. Commands and results
build: 0 warnings / 0 errors (`dotnet build StoreOps.sln --no-incremental`); tests: 94/94 passed (0 failed, 0 skipped); architecture: 18/18 passed
Also run: `Get_alerts_filters_by_severity` and SwaggerTests filter, 13/13 passed.
COV-01..04: 94.1% (254/270) / 100.0% (41/41) / 94.4% (101/107) / 96.4% (718/745) (thresholds 80/70/60/70). Computed from the cobertura report with a script following the SKILL method (unique file/line pairs, nested classes folded into the outer class, package StoreOps.Api).

TEST-01 check A output (only the two allowlisted tests):
```
SwaggerTests.cs: Swagger_is_not_exposed_outside_development
InMemoryEventBusTests.cs: Publish_without_handlers_completes
```
TEST-01 check B output: (empty)

ARCH-05 greps (all empty, output below):
- `grep -rnP "throw new (?!...)"`: macOS grep has no -P, so I ran the equivalent `perl -ne 'print if /throw new (?!(ValidationError|NotFoundError|ConflictError)\b)/'` over every src/StoreOps.Api `*.cs` file outside obj. Output: (empty)
- `grep -rnE "\b(NotFound|BadRequest|Conflict|StatusCode|Problem|UnprocessableEntity)\(" ... --include=*Controller.cs`: (empty)
- `grep -nE "\bcatch\b" Modules/*/*Controller.cs Modules/*/*Service.cs`: (empty)
- Worker check (`grep -rnE "\bcatch\b" src/StoreOps.Api/Modules`): only `Activities/SlaEvaluationWorker.cs:23` (OperationCanceledException when cancelled) and `:37` (`Exception ex when not OperationCanceledException`). Both are in the background worker and unchanged by S7.
- `ExcludeFromCodeCoverage` in src: none.

## 4. Known gaps
None. Assumption: a malformed `recipientStaffId` is rejected by ASP.NET Core model binding of `Guid?` and shaped by the existing ModelStateErrorFactory, as for `severity`, so no service-level validation was added. The alert list is ordered by CreatedAt descending as before. OOS-03 (pagination) was not touched.
