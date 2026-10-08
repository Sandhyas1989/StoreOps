# StoreOps operating contract

This file is the executable workflow for this repository. The main Claude Code session is the **orchestrator**. It runs the agents in `.harness/agents/`, reads only status markers, and moves the work forward with the deterministic rules below. Agents do the work; the orchestrator never writes code, plans, verdicts or run logs itself.

| Agent | File | Writes |
|---|---|---|
| Planner | `.harness/agents/planner.agent.md` | `.harness/output/spec.md`, `.harness/output/sprint-<N>-contract.md` |
| Generator | `.harness/agents/generator.agent.md` | files in contract §1, `.harness/output/generator-summary.md`, contract line 1 → `STATUS: IN PROGRESS` |
| Evaluator | `.harness/agents/evaluator.agent.md` | `.harness/output/evaluator-feedback.md` |
| Monitor | `.harness/agents/monitor.agent.md` | `.harness/reviews/sprint-<N>-run-log.md` |

Project context and rules: `.harness/skills/app-context/SKILL.md` and `.harness/skills/architecture-principles/SKILL.md`.

## 1. Developer commands (the only inputs that move the workflow)
| Command | Effect |
|---|---|
| `@planner <feature request>` | starts a feature (§3) |
| `APPROVED` | approves the plan awaiting approval (§3) |
| any other reply while a plan awaits approval | a change request, passed to the planner |
| `CONTINUE` | resumes after the commit pause (§5) |
| `RETRY S<N>` | after an escalation, grants up to 3 more iterations (§6) |
| `STOP` | ends the run; no further agent is started |

`@generator`, `@evaluator` and `@monitor` may be typed by hand for diagnosis only. They follow the same rules and don't advance the workflow.

## 2. Status markers (line 1 of each file, exact text)
| File | Allowed values | Set by |
|---|---|---|
| `spec.md` | `STATUS: AWAITING APPROVAL` → `STATUS: APPROVED` → `STATUS: COMPLETE` | planner, planner, orchestrator |
| `sprint-<N>-contract.md` | `STATUS: AWAITING APPROVAL` → `STATUS: APPROVED` → `STATUS: IN PROGRESS` → `STATUS: ACCEPTED` or `STATUS: ESCALATED` | planner, planner, generator, orchestrator |
| `generator-summary.md` | `STATUS: READY FOR EVALUATION` or `STATUS: BLOCKED` | generator |
| `evaluator-feedback.md` | `VERDICT: PASS`, `VERDICT: CONDITIONAL PASS` or `VERDICT: FAIL` | evaluator |
| `escalation.md` | `STATUS: ESCALATED` | orchestrator |

- **Strict matching:** a marker counts only if line 1 matches its value exactly (case-sensitive, no extra text). Anything else is **unparseable**.
- **The orchestrator may only:**
  - set a contract to `ACCEPTED` or `ESCALATED`;
  - set a contract back to `IN PROGRESS` on `RETRY S<N>`;
  - set `spec.md` to `COMPLETE`.

## 3. Planning gate
1. On `@planner <feature request>`, run the planner. It writes `spec.md` and every `sprint-<N>-contract.md`, each with line 1 `STATUS: AWAITING APPROVAL`, then stops.
2. **No generator action until the developer types exactly `APPROVED`.** Any other reply goes back to the planner as a change request, and approval is awaited again.
3. On `APPROVED`, run the planner's approval step (all line 1s become `STATUS: APPROVED`). Then go straight to §4 for the current sprint.

## 4. Sprint loop (automatic after approval)
- **Current sprint:** the lowest `N` whose contract line 1 isn't `STATUS: ACCEPTED`.
- **Iteration:** `k` starts at 1 for each sprint.

**Pre-flight**, before the generator's first run of a sprint (`k = 1` only; on retries the tree legitimately holds the sprint's uncommitted work). Stop and report if any fails:
- `git rev-parse HEAD` succeeds.
- `git status --porcelain` is empty, so `HEAD` is the pre-sprint baseline the evaluator diffs against. `.harness/output/` is git-ignored, so it doesn't count.
- `git check-ignore -q .harness/output/spec.md` succeeds.

**Run order:**
1. **Generator:** `@generator S<N>` when `k = 1`, or `@generator S<N> retry .harness/output/evaluator-feedback.md` when `k > 1`. Read line 1 of `generator-summary.md`:
   - `STATUS: BLOCKED` → escalate (§6, reason `GENERATOR_BLOCKED`).
   - `STATUS: READY FOR EVALUATION` with a heading naming `S<N>` and iteration `k` → step 2.
   - anything else → re-run the generator once for the same `k`. If it's still invalid, escalate (`MALFORMED_SUMMARY`).
2. **Evaluator:** `@evaluator S<N>`. For `N > 1`, include the path of the previous sprint's archived feedback, so carried-forward NB findings are re-checked.
3. **Validate the verdict.** It's valid only if:
   - line 1 is one of the three exact `VERDICT:` values;
   - the heading names `S<N>` and iteration `k`;
   - for `CONDITIONAL PASS`, every row in feedback §5 is `NON-BLOCKING` with check `NB-01`..`NB-05`, and §2–§3 contain no `FAIL`.

   If the verdict is missing, ambiguous or contradictory, **fail safely**: treat the sprint as not passed and re-run the evaluator once for the same `k`, stating that the previous output was invalid. This doesn't use up an iteration. If it's still invalid, escalate (`MALFORMED_VERDICT`).
4. **Snapshot** (every iteration, before anything else). Copy both files unchanged into the git-ignored `.harness/output/history/`:
   - `generator-summary.md` → `.harness/output/history/sprint-<N>-iteration-<k>-generator-summary.md`
   - `evaluator-feedback.md` → `.harness/output/history/sprint-<N>-iteration-<k>-evaluator-feedback.md`

   Snapshots stay out of `.harness/reviews/` until the sprint ends, so they never appear as untracked files in a later iteration's diff (MC-05).
5. **Act on the verdict:**

| Verdict | Action |
|---|---|
| `PASS` | **archive** (copy every `.harness/output/history/sprint-<N>-iteration-*-*.md` unchanged to `.harness/reviews/`) → run the monitor (`@monitor S<N>`) → set the contract to `STATUS: ACCEPTED` → advance (§5) |
| `CONDITIONAL PASS` | the same as PASS. Only NON-BLOCKING findings (NB-01..05) are allowed. They're preserved in the archived feedback, and the next sprint's evaluator re-checks them. |
| `FAIL` with `k < 3` | `k = k + 1` → back to step 1 as a retry, with `evaluator-feedback.md` as the feedback |
| `FAIL` with `k = 3` | escalate (§6, reason `MAX_ITERATIONS`) |

**Maximum three iterations per sprint.** Only `RETRY S<N>` grants more.

## 5. Advance between sprints
1. If every contract is `STATUS: ACCEPTED`: set `spec.md` to `STATUS: COMPLETE`, report the feature run summary (the verdict per sprint and the run-log paths), and stop.
2. Otherwise, check `git status --porcelain`.
   - **Not empty** (the accepted sprint's code and new review archives are uncommitted): stop and ask the developer to commit them. The evaluator diffs against `HEAD`, so the next sprint needs a clean baseline. Agents never commit. Wait for `CONTINUE`, re-check, and continue only when the tree is clean.
   - **Empty:** go to §4 for the next sprint with `k = 1`.

## 6. Escalation
- **When:** after the third failed iteration, or on `GENERATOR_BLOCKED`, `MALFORMED_SUMMARY` or `MALFORMED_VERDICT`.
- **Do these steps in order:**
  1. archive the sprint's snapshots to `.harness/reviews/` (as for PASS);
  2. write `.harness/output/escalation.md` (schema in §8);
  3. set the contract to `STATUS: ESCALATED`;
  4. stop.
- **Don't run the monitor** unless the developer declares the FAIL final (`STOP`), in which case run `@monitor S<N>` once.
- **On `RETRY S<N>`:**
  - commit nothing; the developer may commit or discard the escalated work first. If the tree was reset to `HEAD`, restart with the pre-flight check;
  - require `git status --porcelain .harness/reviews` to be empty (the escalation archives are committed). Otherwise stop and ask the developer to commit them, because untracked archives would fail the evaluator's scope check (MC-05);
  - set the contract back to `STATUS: IN PROGRESS`;
  - continue §4 with `k = k + 1`, allowing up to 3 more iterations;
  - the generator treats the last feedback as its input.

## 7. Context scope and cost control
- **Each agent runs as a fresh subagent** (Agent tool) told to follow its agent file for `S<N>`. It reads only that file's inputs:
  - **Planner:** the three planning skills plus the module source it touches.
  - **Generator:** app-context, architecture-principles, only the stack skills its contract scope needs, the current contract, the files in that contract's scope, and the feedback on a retry.
  - **Evaluator:** the contract, generator-summary, the git diff and changed files, architecture-principles, how-to-review and evaluation-criteria.
  - **Monitor:** app-context, generator-summary, evaluator-feedback and earlier run logs.
- **The orchestrator reads only** line 1 and the heading of each handoff file, plus `git` status output. It never loads source, skills or full reports into its own context.
- **Reset between sprints:** no agent context carries over. All state lives in files. Between iterations, only the feedback file carries over.
- **Cost:** the generator runs single test classes while iterating and the full suite once at the end. The evaluator runs the full suite once. Agents never re-read skills they don't need. The monitor records token cost, labelled `ESTIMATED` unless an exact count is supplied.

## 8. Handoff files and schemas
| File | Location | Lifetime | Required structure |
|---|---|---|---|
| `spec.md` | `.harness/output/` (working state, git-ignored) | per feature | planner.agent.md Output 1 (headings §1–§11) |
| `sprint-<N>-contract.md` | `.harness/output/` | per feature | planner.agent.md Output 2 (headings §1–§10; ACs `AC-<N>.nn` with Traces / Rules / EVIDENCE) |
| `generator-summary.md` | `.harness/output/` | overwritten each iteration | generator.agent.md Output (§1 Files changed … §5 Retry log) |
| `evaluator-feedback.md` | `.harness/output/` | overwritten each iteration | evaluator.agent.md Output (§1 Dimension scores … §8 Previous findings re-check) |
| `escalation.md` | `.harness/output/` | until resolved | the block below |
| `history/sprint-<N>-iteration-<k>-*.md` | `.harness/output/history/` | per sprint (snapshots) | exact copies of the two files above |
| `sprint-<N>-iteration-<k>-*.md` | `.harness/reviews/` (**committed audit history**) | permanent, never edited | copied from `history/` at sprint end (PASS, CONDITIONAL PASS or escalation) |
| `sprint-<N>-run-log.md` | `.harness/reviews/` | permanent, never overwritten | monitor.agent.md (`schema: storeops-run-log/v1`) |

```
STATUS: ESCALATED
# Escalation: S<N>, FEAT-<slug>
Reason: MAX_ITERATIONS | GENERATOR_BLOCKED | MALFORMED_SUMMARY | MALFORMED_VERDICT
Escalated: <UTC ISO-8601>   Iterations used: <k>
## 1. Iteration history     | Iteration | Verdict | Failed HG/MC IDs | Blocking finding IDs | Archive paths |
## 2. Unresolved findings   (copied unchanged from the last evaluator-feedback §5)
## 3. Developer options     RETRY S<N> | @planner <revised request> | STOP
```

- `.harness/output/` is **working state**: it's overwritten, never treated as history, and not committed.
- `.harness/reviews/` is the **committed audit history**: the developer commits it with each accepted sprint (§5).

## 9. Relationship to CI
- The evaluator's checks run **before** CI and feed it. A sprint reaches a commit only with `PASS` or `CONDITIONAL PASS`, and the archived feedback and run log are the review record attached to that commit.
- **They don't replace CI.** CI must independently run `dotnet build StoreOps.sln`, `dotnet test StoreOps.sln --collect:"XPlat Code Coverage"`, the architecture tests and the COV-01..04 gates on the committed code. A CI failure overrides an evaluator PASS: the sprint is treated as `FAIL` and resumed with `RETRY S<N>`.
- There's no CI pipeline in this repository yet. Until there is, the evaluator run is the only automated gate, and the developer's commit is the release decision.

## 10. Invariants
- No agent commits, pushes or rewrites git history.
- Only the generator modifies `src/` and `tests/`, and only for the current sprint's contract §1.
- No agent changes another agent's verdict or summary.
- A later sprint never starts before the previous one is `STATUS: ACCEPTED` and committed.
- Any rule here that can't be evaluated deterministically resolves to **stop and report**, never to proceeding.
