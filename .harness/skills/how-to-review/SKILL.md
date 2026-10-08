---
name: how-to-review
description: Step-by-step StoreOps sprint review procedure for the evaluator. Covers collecting evidence, running the deterministic commands, reading the git diff per file type, mapping every acceptance criterion to its test assertions, and writing actionable findings with file and line/symbol references. Use together with evaluation-criteria.
---

# How to review a StoreOps sprint

Review the code, not the generator's description of it. Every finding must be reproducible from a command or a file and line.

## 1. Collect evidence (evaluation-criteria §1)
Run from the repository root. Keep command output outside the repo; the only repo file the evaluator writes is `.harness/output/evaluator-feedback.md`.
```bash
head -1 .harness/output/sprint-<N>-contract.md                     # E-01
head -3 .harness/output/generator-summary.md                       # E-02
git status --porcelain && git diff --stat HEAD && git diff HEAD    # E-03 (untracked files: git ls-files --others --exclude-standard)
dotnet build StoreOps.sln                                          # E-04
rm -rf tests/StoreOps.Api.Tests/TestResults
dotnet test StoreOps.sln --collect:"XPlat Code Coverage" --logger "trx;LogFileName=evaluator.trx"   # E-05, E-06
dotnet test StoreOps.sln --no-build --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture" # E-07
```
- If `git` fails (for example "detected dubious ownership", or no `HEAD`), E-03 is missing and the verdict is FAIL-safe. Report the exact error and the fix the user must run (`! git config --global --add safe.directory "<repo>"`, then commit a baseline). Don't work around it.
- If the build fails with `MSB3027` (a locked `bin/`), report E-04 as invalid and ask the user to stop the running `StoreOps.Api`. Don't kill processes.

## 2. Run the gate commands
Run every grep in evaluation-criteria HG-04..HG-07 and MC-08 exactly as written, and TEST-01 checks A and B from architecture-principles. Compute COV-01..04 from the cobertura file:
- take unique `(filename, line)` pairs per scope, folding compiler-generated `/` classes into their outer class;
- a pair counts as covered if any entry has `hits > 0`;
- report `covered/total = x.x%`.

Record each command and its decisive output line in the feedback evidence log.

## 3. Read the diff, file by file
For each file in `git diff --stat HEAD` plus the untracked files:

| File type | Check |
|---|---|
| `*Controller.cs` | one `I*Service` dependency; each action is bind → one service call → `*Response.From` → `Ok`/`CreatedAtAction`/`NoContent`; no logic tokens (HG-06 grep); `{id}` routes without a `:guid` constraint |
| `*Service.cs` | write order: load → state check → validate → `with` → persist → publish; only `AppError` throws; error codes are `const` fields; cross-module dependencies are `*Lookup` only; time comes from `TimeProvider` |
| `Persistence/*Repository.cs` | in its module's `Persistence` namespace; no business rules; "once only" operations atomic (`TryAdd`, `TryRemove`, compare-and-swap) |
| `*Events.cs` | declared in the publishing module; the payload has every field the contract's ACs list |
| `*Handler.cs` | in the consuming module; calls only its own service and `*Lookup`s; idempotent for a duplicate event |
| `*Module.cs` | lifetimes (repositories singleton; services, lookups and handlers scoped); no other module's `Persistence` types |
| `*Contracts.cs` | additive-only changes to existing `*Response` records; requests use nullable members |
| tests | one test per AC with the exact `EVIDENCE` name; no removed `Assert`, `Skip`, or edits to architecture rule files (MC-04) |
| anything else | must be in contract §1, or justified in generator-summary §4 (MC-05) |

Compare the diff against contract §1, §3 and §7 for out-of-scope work (MC-06).

## 4. Map each AC to its assertions (MC-02)
For every AC in contract §3:
1. Find the `EVIDENCE` test by exact name (`grep -rn "Task <Name>(" tests`). If it's missing, the AC fails.
2. Read its outcome from the TRX (`UnitTestResult testName=… outcome=`). If it isn't `Passed`, the AC fails.
3. For each `THEN` and `AND` clause, find the assertion that proves it:
   - status → `ReadErrorAsync` or `Assert.Equal(HttpStatusCode…)`
   - `code` → `Assert.Equal(<Service>.<Code>Code, error.Code)`
   - stored state → a follow-up `GET` with `Assert.Equal`
   - events → `Factory.Events.Of<T>()` with `Assert.Single`/`Assert.Empty` and field asserts
   - exact counts → `Assert.Equal(n, …)`

   If any clause has no matching assertion, the AC fails. The finding cites the missing clause.
4. Check the test's `GIVEN` data matches the AC: same priorities, statuses, times, and seeded ids.

## 5. Write findings
Each finding is one row:

| Field | Content |
|---|---|
| ID | `F-01`, `F-02`, … (numbered per feedback file) |
| Severity | `BLOCKING` (any HG, MC or evidence failure) or `NON-BLOCKING` (only NB-01..05) |
| Check | HG-xx, MC-xx, NB-xx or AC-N.nn |
| Location | `path:line`, or `path` + symbol (`ActivityService.UpdateAsync`). Line numbers come from `grep -n` on the current file. |
| Problem | what the evidence shows, quoted (command output or code) |
| Required change | one concrete action the generator can take, scoped to this sprint |

- **Banned in findings:** opinions without evidence ("could be cleaner", "consider refactoring"), and requests outside the contract.
- **One root cause, one finding.** List every affected location in that finding.
- **On a retry iteration,** first re-check each previous BLOCKING finding by its ID, then run the full review again.
