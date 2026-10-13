# REFLECTION: demonstration run of FEAT-sla-breach-alerting

**Run:** 7 sprints, each accepted on iteration 1 with `VERDICT: PASS` 100/100; 94/94 tests; coverage 94.1 / 100.0 / 94.4 / 96.4% (thresholds 80/70/60/70). No sprint was escalated.

## What the harness did well

- **Pre-flight caught a real fault before any agent ran.** `.harness/output/` had been committed (`7a43bbe`) with its ignore rule disabled, so `git check-ignore -q .harness/output/spec.md` failed and the run stopped. Otherwise the output files would have appeared as modified files in the evaluator's diff (MC-05). Fixed in `094d445` and `a22918f` (JOURNAL.md).
- **A missing precondition produced `BLOCKED`, not fake evidence.** SDK 8.0.425, pinned in `global.json`, was not installed, and the Sprint 2 generator stopped with `STATUS: BLOCKED` (JOURNAL.md).
- **Evidence was re-run, not trusted.** Each evaluator rebuilt, retested and recomputed coverage, and MC-09 compared the result with the generator summary (every `evaluator-feedback` file).
- **The same violation failed the same way.** I injected an `Activities → Alerts` repository reference in a throwaway worktree and ran the architecture tests twice: the same four tests failed both times (`docs/proposals/architecture-test-messages.md`).

## Where it fell short

**Observed limitation: architecture-test failures are not actionable.** `tests/StoreOps.Api.Tests/Architecture/ModuleBoundaryTests.cs` backs gates HG-03 and HG-04 (and MC-07) with plain `Assert.Empty` / `Assert.Null`. In the experiment above (run after the demonstration, so no sprint is involved) xUnit truncated each item at 50 characters (`TempViolation -> S"···`), and the message had no rule ID, no fix and no source line. The brief (§10) asks for file, line and rule violated.

Also observed: `evaluation-criteria` §2 uses `grep -P` for HG-05a and HG-05c, which macOS lacks, so the S2–S7 evaluator feedback records substituted regexes. Sprint 2's `BLOCKED` summary should have escalated under CLAUDE.md §6 and did not (JOURNAL.md). All seven verdicts were 100/100, so the FAIL, retry and `escalation.md` paths never ran.

## One concrete improvement

This refines **D1, "Architecture rules are executable tests, not review prose"** (DESIGN_BRIEF.md): if these tests are the evidence behind HG-03 and HG-04, their failure output must be enough to act on. [`docs/proposals/architecture-test-messages.md`](docs/proposals/architecture-test-messages.md) proposes a `RuleAssert` helper that throws with the rule ID, every violation in full and a one-line fix. In a prototype the same violation produced `[ARCH-02 / HG-03] … TempViolation -> …IAlertRepository` plus a `Fix:` line, and the clean suite passed 18/18. The patch (`docs/proposals/architecture-test-messages.patch`, +67 / -9) is not applied, because those test files are protected from generators (MC-04).

## Before using this harness with a client team

- **Add CI.** CLAUDE.md §9: "There's no CI pipeline in this repository yet." Run build, tests, architecture tests and the COV gates on every commit.
- **Check the environment in pre-flight:** compare `dotnet --version` with `global.json` (the Sprint 2 block).
- **Make gate commands portable:** replace the `grep -P` commands with a checked-in script.
- **Protect the rule files:** require code-owner review for `ModuleRules.cs` and the architecture tests, since only the evaluator enforces MC-04.
- **Measure token cost:** every run log is an ESTIMATED chars/4 lower bound, and Sprint 2's excludes the interrupted runs.
