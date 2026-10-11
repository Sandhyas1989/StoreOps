STATUS: READY FOR EVALUATION
# Generator summary: S5, iteration 1, FEAT-sla-breach-alerting

## 1. Files changed
| Path | New/Changed | ACs / reason |
|---|---|---|
| src/StoreOps.Api/Modules/Activities/SlaEvaluationWorker.cs | New | AC-5.01, AC-5.02. `BackgroundService` (not named `*Service`), `PeriodicTimer(interval, TimeProvider)`, new DI scope per tick, calls `ISlaEvaluationService`, logs and continues on unexpected exceptions |
| src/StoreOps.Api/Modules/Activities/SlaOptions.cs | Changed | `EvaluationInterval` (default 00:01:00), `WorkerEnabled` (default true) |
| src/StoreOps.Api/Modules/Activities/ActivitiesModule.cs | Changed | validates `EvaluationInterval > 0`; registers the worker only when `Sla:WorkerEnabled` is true |
| src/StoreOps.Api/appsettings.json | Changed | `Sla:EvaluationInterval` = 00:01:00, `Sla:WorkerEnabled` = true |
| tests/StoreOps.Api.Tests/Infrastructure/StoreOpsApiFactory.cs | Changed | `UseSetting("Sla:WorkerEnabled","false")` by default |
| tests/StoreOps.Api.Tests/Activities/SlaEvaluationWorkerTests.cs | New | AC-5.01, AC-5.02 |

Contract line 1 was set to `STATUS: IN PROGRESS` before editing. No file outside contract §1 changed (`git status --porcelain` shows only the six files above).

## 2. AC self-check
| AC | EVIDENCE test | Result | Notes |
|---|---|---|---|
| AC-5.01 | SlaEvaluationWorkerTests.Worker_publishes_breach_on_each_interval_tick | PASS | Enabled host built with `Factory.WithWebHostBuilder` (UseSetting WorkerEnabled=true, EvaluationInterval=00:01:00). Activity High/Pending dueAt 08:45Z, `Clock.Advance(1 min)`, polls `Factory.Events` every 50 ms for up to 5 s wall time, then asserts exactly one `ActivitySlaBreached` with `ActivityId` = A and `Stage = Breach` (no Escalation event can exist, since `Assert.Single` over all of them) |
| AC-5.02 | SlaEvaluationWorkerTests.Disabled_worker_does_not_evaluate | PASS | Default factory (WorkerEnabled=false). Advances clock 5 x 1 min; asserts hosted services are non-empty and contain no `SlaEvaluationWorker`; `ActivitySlaBreached` events empty; `POST /api/activities/sla-evaluations` returns `newlyBreached = [A.id]`, `newlyEscalated` empty |
| AC-5.03 | §5 command output (3 runs) | PASS | See §3: 3 runs, each 0 warnings / 0 errors, 88/88 tests, 18/18 architecture, coverage above thresholds |

## 3. Commands and results
Three consecutive runs of the §5 commands (build; `rm -rf tests/StoreOps.Api.Tests/TestResults` + full `dotnet test --collect:"XPlat Code Coverage"`; architecture filter). Identical results each run:
```
run 1: build 0 Warning(s) / 0 Error(s); Passed! Failed: 0, Passed: 88, Total: 88; architecture Passed! Passed: 18, Total: 18
run 2: build 0 Warning(s) / 0 Error(s); Passed! Failed: 0, Passed: 88, Total: 88; architecture Passed! Passed: 18, Total: 18
run 3: build 0 Warning(s) / 0 Error(s); Passed! Failed: 0, Passed: 88, Total: 88; architecture Passed! Passed: 18, Total: 18
```
build: 0/0; tests: 88/88; architecture: 18/18

COV-01..04 (computed from each run's cobertura report by the architecture-principles method; identical in all 3 runs):
service 231/247 = 93.5% / controller 41/41 = 100.0% / shared 101/107 = 94.4% / overall 671/698 = 96.1% (thresholds 80/70/60/70). All PASS. Baseline was 92.2/100/94.4/96.3, so no drop of more than 5 points.

TEST-01 check A output (only the two allowlisted tests):
```
SwaggerTests.cs: Swagger_is_not_exposed_outside_development
InMemoryEventBusTests.cs: Publish_without_handlers_completes
```
TEST-01 check B output: (empty)
TEST-01 check C (manual): new hit `SlaEvaluationWorkerTests.cs:47 Assert.DoesNotContain(hostedServices, ...)` is preceded in the same test by `Assert.NotEmpty(hostedServices)`.

ARCH-05 greps. macOS grep has no `-P`, so the `-P` greps were run with perl equivalents (`find ... -exec perl -ne` with the same negative-lookahead regex):
```
throw new (?!(ValidationError|NotFoundError|ConflictError)\b)  -> (empty)
controller-built errors grep -rnE ...Controller.cs            -> (empty)
catch in *Controller.cs / *Service.cs                          -> (empty)
throw (?!new)\w+\(  -> ActivityService.cs (NotFound x2), ProgrammeService.cs (StaffIdError x2): baseline helpers only
```
Worker check (`grep -rnE "\bcatch\b" src/StoreOps.Api/Modules --include=*.cs`), both in SlaEvaluationWorker.cs:
```
SlaEvaluationWorker.cs:23: catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)   // host stopping, no state change
SlaEvaluationWorker.cs:37: catch (Exception ex) when (ex is not OperationCanceledException)                  // logs "SLA evaluation tick failed" and continues
```
No `!` operator, `[ExcludeFromCodeCoverage]`, new package or suppression was added.

## 4. Known gaps
- **Registration-time read of config (design note for the evaluator).** The worker must be absent from `IHostedService` when disabled (AC-5.02), so the decision is made in `AddActivitiesModule`, before the host is built. `Program.cs` is outside §1 and unchanged, so the module cannot receive `IConfiguration` as a parameter. `IsWorkerEnabled` reads the `IConfiguration` registration in the service collection (the host builder registers it as a factory that ignores its argument, invoked with an empty provider). This depends on that framework behaviour. It works with `WebApplicationFactory.UseSetting`, verified by AC-5.01 and AC-5.02; if the registration were not found it falls back to enabled (the default). A cleaner alternative is `AddActivitiesModule(builder.Configuration)`, which needs a one-line `Program.cs` change that the planner would have to add to §1.
- The `catch` blocks in the worker's failure path (log and continue) have no dedicated test; the contract defines no AC for it. Coverage thresholds are met.
- Worker interval/enabled settings take effect at startup only (no runtime reload).
- Out of scope per §7: distributed scheduling, worker health metrics.

## 5. Retry log
Omitted (iteration 1).
