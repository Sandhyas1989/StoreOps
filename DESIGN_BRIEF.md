# Harness Design Brief: StoreOps (.NET 8)

The harness governs AI-generated changes to the StoreOps API so that the four failure modes seen in the client's earlier experiment cannot reach `main`: **FM-1** direct imports from another module's repository, **FM-2** raw throws instead of the typed `AppError` hierarchy, **FM-3** tests that assert status codes but not business rules, **FM-4** state changes that bypass the event bus. The demonstration feature was SLA breach alerting (`PROMPT.md`), run as seven sprints, all accepted on iteration 1. Evidence is in `.harness/reviews/`.

## A. Intent decomposition

**Agents.** Four agents with non-overlapping outputs, wired by `CLAUDE.md`, which acts only as orchestrator:

| Agent | Writes | Never does |
|---|---|---|
| Planner | `spec.md`, `sprint-<N>-contract.md` | write code |
| Generator | files named in contract §1, `generator-summary.md` | accept its own work, touch skills or other contracts |
| Evaluator | `evaluator-feedback.md` | modify source |
| Monitor | `sprint-<N>-run-log.md` | change a verdict |

**From PROMPT.md to Planner outputs.** The Planner turned each bullet of `PROMPT.md` into numbered requirements in `spec.md`: the breach definition and the published event (REQ-01, REQ-03), the alert for the Department Lead (REQ-09, resolved to the store's ShiftLead), the configurable grace period and escalation to the Store Manager (REQ-04, REQ-05, REQ-10), and duplicate prevention (REQ-06, REQ-11). The "tests" and "evidence of thresholds" bullets became REQ-13 and the TEST-01 and COV gates repeated in every sprint contract, so the feature intent directly decides what the Generator must build and what the Evaluator checks.

**Sprint boundaries.** The feature (PROMPT.md) became 13 numbered requirements and 7 vertical slices, each one observable through an endpoint or a test: S1 `Critical` priority; S2 evaluation endpoint detects breaches (no event yet); S3 publish `ActivitySlaBreached`; S4 escalation after a configurable grace period; S5 background worker; S6 Alerts consumes the event and creates deduplicated alerts for the ShiftLead and StoreManager; S7 filter alerts by recipient. Boundaries follow module ownership and the dependency order recorded in the spec's sprint plan (S2 depends on S1, S3 on S2, S4 on S3, S5 on S4, S6 on S4, S7 on S6). My reasoning for the ordering, not a rule in the spec: detection before publishing lets the breach rule be tested without the event bus; finishing the Activities side before Alerts consumes it means the new `Alerts → Staff` edge arrives in a single sprint where the acyclic-graph check can judge it; and the worker (S5) comes after the logic it schedules.

**Acceptance criteria.** Every AC has GIVEN / WHEN / THEN, the requirement and rules it traces to, and an `EVIDENCE` line naming the exact test method. That is what makes it testable and not subjective: the evaluator checks that a test with that name exists, that it passed in the TRX, and that every THEN/AND clause has an assertion. Time is controlled with a forward-only `FakeTimeProvider` convention fixed in the Sprint 2 contract, so no AC depends on wall-clock time.

**Example contract entry (Sprint 2, AC-2.05, verbatim):**
```
AC-2.05  Traces: REQ-02, REQ-06  Rules: TEST-01
GIVEN  Clock = 09:00Z; activity A {High, Pending, dueAt 08:45Z} already reported in newlyBreached by an earlier evaluation
WHEN   POST /api/activities/sla-evaluations again (Clock still before 09:15Z)
THEN   status 200, newlyBreached = []
EVIDENCE  SlaEvaluationEndpointsTests.Repeated_evaluation_does_not_report_same_breach_twice
```

## B. Governance framework

**Skill files** (nine, in `.harness/skills/`, read selectively per agent to limit tokens):

| Skill | Read by | StoreOps-specific content |
|---|---|---|
| `app-context` | all | modules, enums, endpoint inventory, FM-1..FM-4 |
| `architecture-principles` | all | ARCH-01..07, TEST-01, COV-01..04, each with allowed and prohibited StoreOps examples |
| `sprint-decomposition` | planner | vertical-slice rules, GIVEN/WHEN/THEN with objective evidence, required IDs, dependencies, risks |
| `coding-conventions`, `api-integration`, `component-patterns`, `how-to-test` | generator, only those the contract scope touches | C# 12 naming, DI and nullable rules; routes, status codes and the `AppError`-to-response mapping; controller/service/repository/event/handler/options/worker patterns copied from `src/`; `WebApplicationFactory` and `EventRecorder` test patterns |
| `how-to-review`, `evaluation-criteria` | evaluator | evidence collection, gates, scoring, verdict rules |

The shared pair is read by every agent because a rule means the same thing to the author and the checker. The rest are loaded only when needed.

**Audit trail.** `.harness/output/` is git-ignored working state, overwritten each iteration. At the end of each sprint the orchestrator copies the generator summary and evaluator feedback unchanged into `.harness/reviews/` (committed, never edited) and the monitor writes `sprint-<N>-run-log.md` in a fixed schema (`storeops-run-log/v1`): verdict, iterations, failed rules, retried rules, coverage, tests, estimated token cost. A recurring problem would show as the same rule ID under `rules_failed` across several logs, and the monitor's `skill_improvement` field names the skill file and section to change. In this run every log shows `rules_failed: []`, so nothing recurred. **Access:** the archive lives in the repository, so anyone with read access to it can audit every acceptance decision. Only the developer commits it: agents never commit, push or rewrite history (CLAUDE.md §10), and the next sprint cannot start until the developer has committed the previous sprint's code and archive (§5). The developer's commit is therefore the release decision, and the archive is the record attached to it.

**One rule that encodes a StoreOps decision (ARCH-04, gate HG-04).** *A side effect that crosses a module boundary is published through `IEventBus`; each contract-named event is constructed only inside `eventBus.PublishAsync(`, and cross-module constructor dependencies may only be read-only `*Lookup` contracts.* Without it, Activities would call `AlertService` directly when it detects a breach. That adds an `Activities → Alerts` edge on top of `Alerts → Staff`, and couples the detector, and its SLA stage bookkeeping, to alert creation. With the rule, Alerts reacts to `ActivitySlaBreached` through `SlaBreachAlertHandler` and the detector never learns who consumes it.

**Relationship to CI.** There is no pipeline in the repository and no `.github/` folder (harness files live in `.harness/`). The evaluator's checks run before CI and feed it: a sprint reaches a commit only with PASS or CONDITIONAL PASS. They do not replace CI, which must independently run build, test with coverage, the architecture tests and the COV gates; a CI failure overrides an evaluator PASS and the sprint resumes with `RETRY S<N>`.

## C. Non-determinism strategy

**Dimensions and weights.** Architecture Compliance 60, Quality and Testing 40 (ADR-004 in JOURNAL.md). Architecture dominates because a fully tested change that breaks a module boundary is still unacceptable, while a feature with a missing test is repairable. The score is reported but never decides the verdict.

**Hard gates**, each tied to a failure mode and each backed by an automated check (an architecture test, a grep or the compiler), which is why none is a soft check:

| Gate | Prevents | Why not a soft check |
|---|---|---|
| HG-01 build, HG-02 tests | code that does not compile or regresses | nothing else can be evaluated |
| HG-03 no cross-module repository reference | FM-1 | boundary erosion is cumulative and costly to unwind |
| HG-04 events through `IEventBus` | FM-4 | one direct call re-couples the modules |
| HG-05 only `AppError` subtypes thrown | FM-2 | clients depend on stable error codes |
| HG-06 controller → service → repository | logic in controllers | layering is the foundation of the other rules |
| HG-07 Reports is read-only | writes from the reporting module | reports must not alter transactional data |
| HG-08 coverage 80/70/60/70 | unverified generated code | the evidence has to be numeric |

FM-3 is covered by mandatory checks MC-02 (every THEN/AND clause has an assertion, found by exact test name) and MC-03 (TEST-01 checks A and B).

**From variable output to a deterministic verdict.** Every check is binary and names its evidence, and the rules are applied in order, first match wins: evidence failure → FAIL; any hard gate FAIL → FAIL; any mandatory check FAIL → FAIL; non-blocking finding NB-01..05 → CONDITIONAL PASS; otherwise PASS. Missing or contradictory evidence counts as FAIL, never as not applicable.

*Worked example: an actual architecture violation.* All seven sprints in the demonstration passed, so I reproduced a violation after the run, in a throwaway git worktree that was then deleted (nothing was committed). The Generator's output varies, but suppose it adds a class in `Modules/Activities` whose constructor takes Alerts' `IAlertRepository`, giving the dependency `Activities → Alerts → IAlertRepository`. I ran the architecture tests twice. Both runs gave the same result, 4 of 18 failing:
- `No_source_file_imports_another_modules_persistence_namespace` and `No_module_references_another_modules_repository` (HG-03);
- `Cross_module_constructor_dependencies_are_read_only_lookups` (HG-04);
- `Module_dependency_graph_has_no_cycles`, reporting `Activities -> Alerts -> Activities` (MC-07).

Verdict rules, in order: no evidence failure; hard gate HG-03 fails, so the verdict is **FAIL**. The score, the generator's wording and the file layout do not change that, and the rules read fields, properties and signatures through reflection and scan source for `using` directives and qualified names, so a different class name or a different way of reaching the same repository is judged by the same rules. The failure messages themselves have gaps (truncated text, no rule ID), which `docs/proposals/architecture-test-messages.md` addresses.

**Escalation.** Three failed iterations, `GENERATOR_BLOCKED`, `MALFORMED_SUMMARY` or `MALFORMED_VERDICT` triggers the §6 sequence: archive the snapshots, write `.harness/output/escalation.md` (reason, iteration history, unresolved findings, developer options), mark the contract `ESCALATED`, stop. The developer receives it in the Claude Code session and answers `RETRY S<N>` (three more iterations), a revised `@planner` request, or `STOP`. It was not produced in this run; the closest event was the Sprint 2 generator being blocked by a missing SDK, which the orchestrator reported and left to the developer to fix.

## D. Architectural decisions

**D1. Architecture rules are executable tests, not review prose.**
*Decision:* the module rules are xUnit architecture tests in the solution (`Architecture` namespace), each with self-tests that run the rule against deliberately bad fixtures. *Alternatives:* an external dependency analyser run in a pipeline; LLM-only review. *Rationale:* the rules run in the same `dotnet test` the generator uses, so a violation fails locally, and the self-tests show a rule can actually fail. *Depends on:* the stack having reflection and syntax access to enforce the rules, and the rule files being protected (MC-04 forbids generators editing them).

**D2. Working state is git-ignored, audit state is committed.**
*Decision:* `.harness/output/` is ignored; reviews are copied to `.harness/reviews/` at the end of each sprint, via a snapshot folder so archives never appear as untracked files in a later diff. *Alternatives:* commit everything; keep reviews outside the repository. *Rationale:* the evaluator diffs against `HEAD`, so working files must not look like changes, while the verdict history must outlive the run. *Depends on:* the developer committing between sprints. This assumption failed once in the demonstration (`.harness/output/` was committed and the ignore rule disabled), and the pre-flight check caught it.

**D3. The Evaluator weights Architecture Compliance 60 and Quality and Testing 40 (ADR-004).**
*Decision:* two dimensions, with architecture dominant, and a score that is reported but never decides the verdict (hard gates and mandatory checks do). *Alternatives:* 50/50, rejected because it treats a boundary violation and a functional defect as equally severe; 40/60, rejected because architecture is the main governance mechanism against FM-1, FM-2 and FM-4. *Rationale:* a fully tested change that breaks a module boundary is still unacceptable, while a missing test is repairable. *Depends on:* the hard gates, not the weights, carrying the accept/reject decision, so the weights only rank a sprint's quality and can never outvote a failed gate. Evidence from this run: all seven sprints scored 100/100, so the weighting never discriminated between sprints, and its value rests on the assumption above and on the gate behaviour shown in the worked example in Section C.
