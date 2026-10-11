STATUS: READY FOR EVALUATION
# Generator summary: S4, iteration 1, FEAT-sla-breach-alerting

## 1. Files changed
| Path | New/Changed | ACs / reason |
|---|---|---|
| src/StoreOps.Api/Modules/Activities/SlaOptions.cs | New | AC-4.05, AC-4.06: `EscalationGracePeriod`, default 00:30:00 |
| src/StoreOps.Api/Modules/Activities/SlaEvaluationService.cs | Changed | AC-4.01..4.05, 4.08: injects `IOptions<SlaOptions>`; after the Breach step, escalates when `now >= dueAt + grace` through `TryAdvanceStageAsync(..., Escalation, ...)`, publishes `ActivitySlaBreached` with `Stage = Escalation`, fills `NewlyEscalated`. Event publishing moved into a private `PublishAsync` helper shared by both stages. |
| src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs | Changed | AC-4.05, 4.06: `AddOptions<SlaOptions>().BindConfiguration("Sla").Validate(> 0).ValidateOnStart()` |
| src/StoreOps.Api/appsettings.json | Changed | AC-4.05: `"Sla": { "EscalationGracePeriod": "00:30:00" }` |
| tests/StoreOps.Api.Tests/Activities/SlaEvaluationEndpointsTests.cs | Changed | AC-4.01..4.05, 4.08: 6 new tests; `EvaluateAsync` gained a `(HttpClient)` overload, with the no-arg form delegating to it. Existing tests are unchanged. |
| tests/StoreOps.Api.Tests/Activities/SlaOptionsTests.cs | New | AC-4.06 |

No file outside contract §1 changed. The contract's line 1 was set to `STATUS: IN PROGRESS` before editing.

## 2. AC self-check
| AC | EVIDENCE test | Result | Notes |
|---|---|---|---|
| AC-4.01 | SlaEvaluationEndpointsTests.Escalates_open_breach_when_grace_period_elapses | PASS | Escalation event equals the Breach event with only `Stage` and `OccurredAt` changed. |
| AC-4.02 | SlaEvaluationEndpointsTests.Does_not_escalate_before_grace_period_elapses | PASS | Evaluated at 09:39:59.900Z. |
| AC-4.03 | SlaEvaluationEndpointsTests.Does_not_escalate_activity_completed_after_breach | PASS | Closed activities are excluded by the existing `IsInBreach` filter. |
| AC-4.04 | SlaEvaluationEndpointsTests.Escalation_event_is_published_once | PASS | 3 sequential runs (Advance 1h each) plus 10 concurrent; 13 responses all have empty `newlyEscalated`. |
| AC-4.05 | SlaEvaluationEndpointsTests.Grace_period_is_read_from_configuration | PASS | `UseSetting("Sla:EscalationGracePeriod", "00:05:00")`; boundary is inclusive at 09:15:00.000Z. |
| AC-4.06 | SlaOptionsTests.Non_positive_grace_period_fails_startup (Theory, 2 cases) | PASS | "00:00:00" and "-00:01:00"; `OptionsValidationException` message contains "EscalationGracePeriod". |
| AC-4.07 | §5 command output | PASS | See §3. |
| AC-4.08 | SlaEvaluationEndpointsTests.First_evaluation_after_grace_publishes_breach_then_escalation | PASS | Events are exactly [Breach, Escalation], in that order. |

## 3. Commands and results
build: 0 warnings / 0 errors; tests: 86/86 passed; architecture: 18/18 passed
COV-01..04: 93.5% (231/247) / 100.0% (41/41) / 94.4% (101/107) / 96.7% (637/659) (thresholds 80/70/60/70). Computed from `tests/StoreOps.Api.Tests/TestResults/61d28751-b012-4581-910d-6d672521fc23/coverage.cobertura.xml`, unique (file, line) pairs, nested compiler classes folded into the outer class.

TEST-01 check A output (only the two allowlisted tests):
```
SwaggerTests.cs: Swagger_is_not_exposed_outside_development
InMemoryEventBusTests.cs: Publish_without_handlers_completes
```
TEST-01 check B output: (empty)

ARCH-05 greps (macOS grep has no `-P`, so the two `-P` greps were run as equivalent Python regexes over `src/StoreOps.Api/**/*.cs`, excluding obj/bin):
- `throw new (?!(ValidationError|NotFoundError|ConflictError)\b)`: no output.
- controller `NotFound|BadRequest|Conflict|StatusCode|Problem|UnprocessableEntity(`: no output.
- `catch` in `*Controller.cs` and `*Service.cs`: no output. `catch` anywhere under `Modules`: no output.
- `throw (?!new)\w+\(`: only the baseline hits (`ActivityService.cs` lines 58 and 129 `NotFound`; `ProgrammeService.cs` lines 87 and 92 `StaffIdError`).
- `ExcludeFromCodeCoverage` in `src/*.cs`: no output.

Other TEST-01 notes: the new `Assert.All` and `Assert.DoesNotContain` uses each follow an `Assert.Single` or `Assert.Equal` count in the same test that proves the collection holds the case.

## 4. Known gaps
None.

Notes:
- The `Escalation` check uses `now >= dueAt + gracePeriod`, so it is inclusive at the boundary (AC-4.05 requires an escalation at exactly 09:15:00.000Z).
- Escalation is computed from `dueAt`, not from the breach time (AC-4.01, AC-4.08).
- `StoreOpsApiFactory` was not edited; `ActivitySlaBreached` already had a recording handler registered.
- `Sla:EscalationGracePeriod` validation runs through `ValidateOnStart`, so an invalid value throws `OptionsValidationException` at host start, not an `AppError` (contract §8).
