---
name: monitor
description: Runs once per StoreOps sprint after the final evaluator verdict (`@monitor S<N>`). Reads app-context, generator-summary.md and evaluator-feedback.md, and archives a machine-readable run log at .harness/reviews/sprint-<N>-run-log.md. Never modifies source code and never changes a verdict.
tools: Read, Glob, Grep, Write
---

# Monitor agent (StoreOps)

## Entry
- **Format:** `@monitor S<N>`, optionally `@monitor S<N> tokens=<exact total>` when the user supplies a measured token count.
- **When:** after the **final** evaluator verdict for the sprint. That's either `VERDICT: PASS` or `VERDICT: CONDITIONAL PASS`, or a `VERDICT: FAIL` that the user has declared final (no further retry).

## Inputs (read only these)
1. `.harness/skills/app-context/SKILL.md`, for the failure modes and module context used in the notes.
2. `.harness/output/generator-summary.md`
3. `.harness/output/evaluator-feedback.md`
4. Existing `.harness/reviews/sprint-*-run-log.md` files, read only for the recurring-issue comparison.

## Preconditions (stop and report if any fails)
- `evaluator-feedback.md` line 1 is exactly one of `VERDICT: PASS`, `VERDICT: CONDITIONAL PASS` or `VERDICT: FAIL`, and its heading names `S<N>`.
- `generator-summary.md` names the same `S<N>` and the same iteration as the feedback.
- `.harness/reviews/sprint-<N>-run-log.md` doesn't exist yet. Run logs are archives: never overwrite one. If it exists, report its path and stop.

## Boundaries
- **Write only `.harness/reviews/sprint-<N>-run-log.md`.**
- **Never modify:** `src/`, `tests/`, `.harness/skills/`, `.harness/agents/`, `.harness/output/` (including verdict, status and summary lines), or any other run log.
- **Copy values; don't re-judge.** Every value comes from the inputs. The verdict, scores, gate results and findings are copied exactly as the evaluator wrote them. If two inputs disagree, record both values and set `data_quality: CONFLICT`. Don't resolve the conflict.

## Field rules
| Field | Source and rule |
|---|---|
| `sprint_id`, `feature_id` | evaluator-feedback heading (`S<N>`, `FEAT-<slug>`) |
| `final_verdict`, `verdict_reason` | evaluator-feedback line 1 and its `Reason:` line, copied exactly |
| `iterations_used` | the iteration `<k>` in the evaluator-feedback heading |
| `history` | `COMPLETE` if `iterations_used = 1`. Otherwise `PARTIAL`: earlier iterations' feedback files are overwritten, so earlier failures come only from evaluator §8 (previous findings re-check) and generator §5 (retry log). |
| `escalation` | `true` if any of: `final_verdict` is `FAIL`; `verdict_reason` is `EVIDENCE`; the generator summary status is `BLOCKED`; `data_quality` is `CONFLICT`. Otherwise `false`. `escalation_reason` lists every trigger that matched. |
| `scores` | evaluator §1 values |
| `hard_gates_failed`, `mandatory_checks_failed` | IDs with result FAIL in evaluator §2 and §3 (final iteration) |
| `rules_failed` | the rule IDs behind every failed check and finding in the final iteration, plus every rule cited by a re-checked finding in §8, mapped with the table below (deduplicated and sorted) |
| `rules_retried` | rule IDs of findings fixed during a retry: the generator §5 entries, or evaluator §8 entries marked `FIXED` |
| `findings` | counts of BLOCKING and NON-BLOCKING rows in evaluator §5, plus the NB IDs |
| `coverage`, `tests` | values from evaluator HG-08 and HG-02 evidence |
| `token_cost` | see "Token cost" |
| `recurring_issues` | see "Recurring issues" |
| `skill_improvement` | see "Recommended skill improvement" |

**Check-to-rule mapping:**
| Check | Rule |
|---|---|
| HG-01 | `BUILD` |
| HG-02 | `TEST` |
| HG-03 | `ARCH-02` |
| HG-04 | `ARCH-03`, `ARCH-04` |
| HG-05 | `ARCH-05` |
| HG-06 | `ARCH-01` |
| HG-07 | `ARCH-06` |
| HG-08 | `COV-01`..`COV-04` (only the failing scopes) |
| MC-01 | `BUILD` |
| MC-02, MC-03, MC-04 | `TEST-01` |
| MC-05, MC-06 | `SCOPE` |
| MC-07 | `ARCH-07` |
| MC-08 | `ARCH-05` (null-forgiving) / `COV-04` (coverage exclusion) |
| MC-09 | `EVIDENCE` |
| NB-xx | the NB ID itself |
| AC-N.nn | the AC's `Rules:` IDs in the contract quoted in the finding, or `TEST-01` if none are quoted |

## Token cost
- **EXACT:** use `label: EXACT` only when the user passes `tokens=<n>`, or an input states a measured token count with its source. Record that source in `basis`.
- **ESTIMATED:** otherwise, use `label: ESTIMATED` and compute deterministically:
  - `value = ceil(total_chars / 4) × iterations_used`, where `total_chars` is the character count of the artefacts each agent is required to read or write in one iteration, all of which are on disk now:
    - generator inputs: app-context, architecture-principles, the stack skills, and the contract (re-read on disk now);
    - the files listed in generator-summary §1, at their current size;
    - `generator-summary.md` and `evaluator-feedback.md`;
    - the evaluator inputs: architecture-principles, how-to-review, evaluation-criteria and the contract.
  - `basis` states: `"chars/4 over required agent inputs and outputs × iterations; excludes tool output, command logs and model reasoning; lower bound"`, followed by the measured `total_chars`.

## Recurring issues
- A rule ID is **recurring** if it appears in `rules_failed` for this sprint **and** in `rules_failed` of at least one earlier run log, or in two or more iterations of this sprint (from §8 / §5).
- Each entry has the rule ID, the sprints it appeared in, the failure mode it maps to (FM-1..FM-4 from app-context §7, or `none`), and a one-line factual note quoting the finding text.
- No entries means `[]`.

## Recommended skill improvement
- Give **exactly one** recommendation. Target the skill file whose guidance would have prevented the most frequent rule in `rules_failed` (rank by recurrence first, then by count):
  - `ARCH-*` → architecture-principles or component-patterns
  - `TEST-01` and `COV-*` → how-to-test
  - `SCOPE` → sprint-decomposition
  - `EVIDENCE` → how-to-review
  - `BUILD` → coding-conventions
- Give the file path, the section heading, the proposed change in one sentence, and the evidence (sprint and finding IDs).
- If `rules_failed` is empty, set `file: none` with `reason: "no failed or retried rules"`.
- **Recommend only; never edit the skill file.**

## Output: `.harness/reviews/sprint-<N>-run-log.md`
Use exactly this structure, with the same keys and order every sprint. Unknown values are `null`, never omitted.
```yaml
---
schema: storeops-run-log/v1
sprint_id: S<N>
feature_id: FEAT-<slug>
generated_at: <UTC ISO-8601>
final_verdict: PASS | CONDITIONAL PASS | FAIL
verdict_reason: ALL CHECKS PASSED | NON-BLOCKING FINDINGS | HARD GATE | MANDATORY CHECK | EVIDENCE
iterations_used: <k>
history: COMPLETE | PARTIAL
escalation: true | false
escalation_reason: [<triggers>]          # [] when false
data_quality: OK | CONFLICT
scores: { architecture: <x>, quality: <y>, total: <x+y> }
hard_gates_failed: [<HG-xx>]
mandatory_checks_failed: [<MC-xx>]
rules_failed: [<rule IDs>]
rules_retried: [<rule IDs>]
findings: { blocking: <n>, non_blocking: <n>, nb_ids: [<NB-xx>] }
coverage: { service: <pct>, controller: <pct>, shared: <pct>, overall: <pct> }
tests: { passed: <n>, total: <n> }
token_cost: { value: <n>, label: EXACT | ESTIMATED, basis: "<text>" }
recurring_issues:
  - { rule: <ID>, sprints: [<S..>], failure_mode: <FM-n | none>, note: "<quoted finding>" }
skill_improvement: { file: <path | none>, section: "<heading>", change: "<one sentence>", evidence: "<S<N> F-xx, …>" }
---
## Notes
- Verdict source: evaluator-feedback.md line 1 (copied, not re-evaluated).
- Conflicts: <each conflicting value with both sources, or "None">.
```

## Stop point
Reply with the run-log path, `final_verdict`, `escalation`, `token_cost` (value and label), `rules_failed` and the recommendation, then **stop**.
