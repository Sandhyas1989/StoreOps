STATUS: READY FOR EVALUATION
# Generator summary: S3, iteration 1, FEAT-sla-breach-alerting

## 1. Files changed
| Path | New/Changed | ACs / reason |
|---|---|---|
| src/StoreOps.Api/Modules/Activities/ActivityEvents.cs | Changed | AC-3.01..3.02: adds `ActivitySlaBreached` record (IIntegrationEvent), declared in Activities |
| src/StoreOps.Api/Modules/Activities/SlaEvaluationService.cs | Changed | AC-3.01..3.03: injects `IEventBus`; publishes only when `TryAdvanceStageAsync` returns true, after the stage write, with `OccurredAt = now` (= response.evaluatedAt) |
| tests/StoreOps.Api.Tests/Infrastructure/StoreOpsApiFactory.cs | Changed | registers `RecordingEventHandler<ActivitySlaBreached>` |
| tests/StoreOps.Api.Tests/Activities/SlaEvaluationEndpointsTests.cs | Changed | AC-3.01..3.03 tests added; existing tests untouched |

Contract line 1 set from `STATUS: APPROVED` to `STATUS: IN PROGRESS` (workflow requirement).

## 2. AC self-check
| AC | EVIDENCE test | Result | Notes |
|---|---|---|---|
| AC-3.01 | SlaEvaluationEndpointsTests.Breach_publishes_event_with_full_payload | PASS | all 8 payload fields asserted; OccurredAt equals response.EvaluatedAt |
| AC-3.02 | SlaEvaluationEndpointsTests.Breach_event_is_published_once_across_repeated_and_concurrent_evaluations | PASS | 3 sequential + 10 concurrent; Assert.Single on recorded events |
| AC-3.03 | SlaEvaluationEndpointsTests.Non_qualifying_activities_publish_no_breach_event | PASS | Low, Completed (via PATCH), future-due and undated High; asserts 4 activities exist, newlyBreached empty, no events |
| AC-3.04 | section 3 command output | PASS | see below |

## 3. Commands and results
build: 0 warnings / 0 errors; tests: 78/78 passed (0 failed, 0 skipped); architecture: 18/18 passed
COV-01..04: 93.2% (221/237) / 100.0% (41/41) / 94.4% (101/107) / 96.6% (620/642) (thresholds 80/70/60/70)

TEST-01 check A output (only the two allowlisted tests):
```
SwaggerTests.cs: Swagger_is_not_exposed_outside_development
InMemoryEventBusTests.cs: Publish_without_handlers_completes
```
TEST-01 check B output: (empty)
ARCH-05 grep output: all three greps printed nothing.

## 4. Known gaps
None. Note: `DueAt` is `DateTimeOffset?` on the entity, so the service uses `GetValueOrDefault()` (no `!`); IsInBreach guarantees it is non-null.

## 5. Retry log
(omitted, iteration 1)
