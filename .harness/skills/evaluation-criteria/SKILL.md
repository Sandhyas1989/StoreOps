---
name: evaluation-criteria
description: Deterministic StoreOps sprint evaluation. Two weighted dimensions (Architecture Compliance 60%, Quality and Testing 40%), eight hard gates, binary mandatory checks, the evidence each check needs, and the verdict rules (PASS, CONDITIONAL PASS, FAIL, FAIL-safe). Used by the evaluator to produce .harness/output/evaluator-feedback.md.
---

# StoreOps evaluation criteria

Every check is **binary**: `PASS` or `FAIL`. No partial credit and no judgement calls. Each check names the exact evidence to collect. Rule IDs refer to architecture-principles.

## 1. Required evidence (collected by the evaluator itself)

| ID | Evidence | Valid only if |
|---|---|---|
| E-01 | `.harness/output/sprint-<N>-contract.md` | line 1 is `STATUS: APPROVED` or `STATUS: IN PROGRESS`; the heading names `S<N>` |
| E-02 | `.harness/output/generator-summary.md` | names the same `S<N>`; has an iteration number; contains §1–§4 of the generator template |
| E-03 | `git status --porcelain` and `git diff HEAD` (tracked and untracked files) | git runs without error and `HEAD` exists (it's the pre-sprint baseline, because the generator never commits) |
| E-04 | `dotnet build StoreOps.sln` output | contains the `Warning(s)` and `Error(s)` counts and an exit code |
| E-05 | `dotnet test StoreOps.sln --collect:"XPlat Code Coverage" --logger "trx;LogFileName=evaluator.trx"` | exit code captured; TRX parseable; total test count > 0 |
| E-06 | the newest `tests/StoreOps.Api.Tests/TestResults/*/coverage.cobertura.xml` | written by the E-05 run (old results deleted first); contains package `StoreOps.Api` |
| E-07 | `dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"` output | exit code and counts captured |

**FAIL-safe rule.** If any E-item is missing, invalid, unparseable, or contradicts another E-item, the verdict is **FAIL** with the reason `EVIDENCE`. Examples of a contradiction:
- the summary's files-changed list ≠ the git diff;
- the summary reports a test as PASS but the TRX shows it failed;
- the summary's sprint ID ≠ the contract's sprint ID.

A check whose evidence can't be produced counts as `FAIL`, never as "not applicable".

## 2. Hard gates (any FAIL means verdict FAIL)

| ID | Gate | PASS when… | Rule | Points |
|---|---|---|---|---|
| HG-01 | Build fails | E-04 exit code 0 and `0 Error(s)` | n/a | Q 4 |
| HG-02 | Tests fail | E-05 exit code 0, failed = 0, total > 0, no skipped tests | TEST-01 | Q 6 |
| HG-03 | Cross-module repository import | E-07 shows these passed: `No_module_references_another_modules_repository`, `No_source_file_imports_another_modules_persistence_namespace`, and self-tests `Detects_cross_module_repository_reference`, `Detects_import_of_another_modules_persistence_namespace` | ARCH-02 | A 12 |
| HG-04 | Cross-module side effect bypassing `IEventBus` | E-07 shows these passed: `Cross_module_constructor_dependencies_are_read_only_lookups`, `Lookup_contracts_expose_only_read_operations` and their self-tests; **and** command HG-04 shows every contract-named event constructed only inside `eventBus.PublishAsync(`; **and** no handler or service in the diff calls another module's `I*Service` | ARCH-03, ARCH-04 | A 12 |
| HG-05 | Raw expected-error throw in controllers or services | commands HG-05a and HG-05b print nothing; every HG-05c hit is a helper whose return type is an `AppError` subtype | ARCH-05 | A 10 |
| HG-06 | Layer skipping or business logic in controllers | E-07 shows `Every_module_follows_controller_service_repository` and `Controllers_depend_on_services_not_repositories` passed; **and** command HG-06 prints nothing | ARCH-01 | A 10 |
| HG-07 | Reports writing to another module | E-07 shows `Reports_module_is_read_only_toward_other_modules` passed; **and** command HG-07 prints nothing | ARCH-06 | A 6 |
| HG-08 | Coverage below threshold | from E-06, computed per the COV rules: service ≥ 80%, controller ≥ 70%, shared ≥ 60%, overall ≥ 70%; all four sub-checks must pass | COV-01..04 | Q 8 (2 each) |

Run these exactly as written (repository root, Git Bash):
```bash
# HG-04: each hit, or the line printed just before it, must contain eventBus.PublishAsync(
grep -rnE -B1 "new (ActivityCreated|ActivityDeleted|ProgrammeMemberAdded|ActivitySlaBreached)\(" src/StoreOps.Api --include=*.cs
# HG-05a: throws that are not AppError subtypes (must print nothing)
grep -rnP "throw new (?!(ValidationError|NotFoundError|ConflictError)\b)" src/StoreOps.Api --include=*.cs | grep -v /obj/
# HG-05b: catch blocks in controllers or services (must print nothing)
grep -nE "\bcatch\b" src/StoreOps.Api/Modules/*/*Controller.cs src/StoreOps.Api/Modules/*/*Service.cs
# HG-05c: helper throws (each must return an AppError subtype)
grep -rnP "throw (?!new)\w+\(" src/StoreOps.Api --include=*.cs | grep -v /obj/
# HG-06: logic or repository use in controllers (must print nothing)
grep -nE "\b(if|else|switch|for|foreach|while|try|catch|throw)\b|Repository|Error\(|\.Where\(|\.OrderBy|\.Count\(|\.Any\(" src/StoreOps.Api/Modules/*/*Controller.cs
# HG-07: write routes or write calls in Reports (must print nothing)
grep -rnE "\[Http(Post|Put|Patch|Delete)|\.(Add|Update|Delete|Raise|TryAdd)\w*Async\(" src/StoreOps.Api/Modules/Reports
# MC-08: null-forgiving operators and coverage exclusions in src (must print nothing)
grep -rnE "[a-zA-Z)]!(,|;|\.|\))" src/StoreOps.Api/Modules src/StoreOps.Api/Shared --include=*.cs
grep -rn "ExcludeFromCodeCoverage" src/StoreOps.Api --include=*.cs
```
If the contract adds an event, add its name to the HG-04 pattern for that evaluation and record the edited command in the evidence log.

## 3. Mandatory checks (any FAIL means verdict FAIL)

| ID | PASS when… | Points |
|---|---|---|
| MC-01 | E-04 shows `0 Warning(s)` | Q 2 |
| MC-02 | **Every AC** in contract §3 has: (a) a test whose name equals its `EVIDENCE` method; (b) a TRX outcome of `Passed`; (c) an assertion for every THEN/AND clause (how-to-review §4). Gate ACs (`Traces: REQ-13`) pass when HG-01..08 and MC-01 pass. | Q 10 |
| MC-03 | TEST-01 check A prints only the allowlisted tests; check B prints nothing | Q 6 |
| MC-04 | Test integrity in the diff: no removed `Assert`, no new `Skip =`, no deleted test method, and no change to `ModuleRules.cs`, `ModuleBoundaryTests.cs`, `ModuleRulesSelfTests.cs` or `ViolationFixtures.cs` | Q 4 |
| MC-05 | Scope: every changed or new file in E-03 is listed in contract §1, or is named in generator-summary §4 as a dependent change with a reason | A 6 |
| MC-06 | No later-sprint or out-of-scope work: no endpoint, event, entity field, configuration key or package that isn't in this contract's §1/§3/DEPs | A 4 |
| MC-07 | `Module_dependency_graph_has_no_cycles` and `Shared_infrastructure_does_not_depend_on_modules` passed; every `ModuleRulesSelfTests` test passed | 0 |
| MC-08 | both MC-08 commands print nothing | 0 |
| MC-09 | generator-summary matches the evaluator's own results: same files, same per-AC results, same build/test counts, and COV values within 0.1 points | 0 |

Checks worth 0 points still decide the verdict.

## 4. Scoring

| Dimension | Weight | Point sources |
|---|---|---|
| Architecture Compliance | **60** | HG-03 12, HG-04 12, HG-05 10, HG-06 10, HG-07 6, MC-05 6, MC-06 4 |
| Quality and Testing | **40** | HG-01 4, HG-02 6, HG-08 8, MC-01 2, MC-02 10, MC-03 6, MC-04 4 |

- Each check scores its full points or 0. Total = Architecture + Quality, out of 100.
- **The score is reported but never decides the verdict.** A score of 96 with one failed hard gate is still FAIL.

## 5. Non-blocking findings (the only findings allowed with CONDITIONAL PASS)

| ID | Finding |
|---|---|
| NB-01 | A naming or layout deviation from coding-conventions that changes no behaviour and no public contract (for example, a misplaced `using`, or a private member's name) |
| NB-02 | Missing or unclear XML `<summary>` on a non-obvious type |
| NB-03 | Error or alert **message** wording (never `code`, status or field keys) |
| NB-04 | A coverage scope is still above its threshold but dropped more than 5 points against the previous accepted value, and generator-summary §4 doesn't explain it |
| NB-05 | Test readability (missing arrange/act/assert separation, or a duplicated setup that could be a helper) |

Anything not listed here is blocking.

## 6. Verdict rules (apply in order; the first match wins)
1. Any evidence failure (§1) → **FAIL** (reason `EVIDENCE`).
2. Any hard gate FAIL → **FAIL**.
3. Any mandatory check FAIL → **FAIL**.
4. One or more NB findings → **CONDITIONAL PASS**. Each NB must be listed with file and line or symbol.
5. Otherwise → **PASS**.

A CONDITIONAL PASS counts as accepted for starting the next sprint. Its NB findings carry forward into the next sprint's evaluation as findings to re-check.
