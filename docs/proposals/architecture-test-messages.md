# Proposal: actionable assertion messages for the architecture tests

**Status:** proposed, not applied. **Owner of the change:** the developer (or a planner-approved contract). The architecture test files are protected from generators by MC-04, so no agent may apply this on its own.

## Problem

The brief (§10) asks for evaluator feedback that "names specific file paths, line numbers, and the rule violated", precise enough that the generator can fix the problem without human clarification. The architecture tests that back hard gates HG-03, HG-04, HG-06 and HG-07 and mandatory check MC-07 fail with xUnit's default `Assert.Empty` / `Assert.Null` messages.

Observed by injecting a temporary violation (a class in `Modules/Activities` that takes `IAlertRepository` in its constructor) in a throwaway worktree, run twice with identical results (4 of 18 architecture tests failed both times):

| Gap | Observed output |
|---|---|
| Truncated target | `Collection: ["StoreOps.Api.Modules.Activities.TempViolation -> S"···]`. xUnit cuts each item at 50 characters (measured on both observed messages), so the forbidden dependency is not visible. |
| No rule or gate ID | The message holds only the test name. The evaluator links it to ARCH-02 / HG-03 from its skill file. |
| No fix guidance | The message says only that the collection was not empty. |
| No source line | The stack trace points to the test line (`ModuleBoundaryTests.cs:26`), not the violating source line. |

The cycle test is not affected by the truncation: it already prints `Actual: "Activities -> Alerts -> Activities"`. It does lack the rule ID and a fix.

## Proposed change

A small helper, `RuleAssert`, that throws `XunitException` with the full text, and `ModuleBoundaryTests` calling it instead of `Assert.Empty` / `Assert.Null`. `ModuleRules.cs` and `ModuleRulesSelfTests.cs` are not touched, so the self-tests that pin the exact violation strings keep passing.

Patch: [architecture-test-messages.patch](architecture-test-messages.patch) (2 files, +67 / -9). Checked with `git apply --check` against `HEAD` `9a14312`.

## Evidence from the prototype

Same injected violation, with the patch applied (full output, nothing truncated):

```
[ARCH-02 / HG-03] 1 violation(s):
  - StoreOps.Api.Modules.Activities.TempViolation -> StoreOps.Api.Modules.Alerts.Persistence.IAlertRepository
Fix: depend on the target module's *Lookup contract (reads) or publish an event via IEventBus (side effects).

[ARCH-07 / MC-07] module cycle: Activities -> Alerts -> Activities
Fix: remove the last dependency in the cycle; publish an event (ARCH-04) instead of adding a module-to-module edge.
```

Without the violation the architecture suite still passes: `Failed: 0, Passed: 18, Total: 18`.

## Not covered (phase 2)

- **Source line numbers.** `PersistenceImportViolations` returns `path -> target`, and `ModuleRulesSelfTests` asserts those exact strings (around line 101). Adding `:line` changes the format, so it needs a coordinated update of the self-test expectations. The regex match index already gives the position.
- **Type-level rules** (reflection) have no source line at all. The type's full name is the best available locator.

## Risks and checks before adopting

- Edits protected files, so it needs a developer-approved change, ideally as its own commit before the next feature.
- Run `dotnet test StoreOps.sln --filter "FullyQualifiedName~StoreOps.Api.Tests.Architecture"` after applying: expect 18/18.
- Update `how-to-review` so evaluators copy the bracketed rule ID and `Fix:` line into the feedback.
- **Maintenance risk: the `Fix:` guidance could drift from skill guidance.** The fix text is hard-coded in `ModuleBoundaryTests.cs`, while the same advice lives in `architecture-principles` (the ARCH rules). If a rule changes in one place and not the other, the generator receives conflicting instructions, and the test message will look authoritative because it appears in the failure output. Mitigation: keep each `Fix:` to one line that names the rule ID and points to its `architecture-principles` section, and review both together whenever an ARCH rule changes.
