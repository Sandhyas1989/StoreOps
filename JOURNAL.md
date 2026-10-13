# Architecture Decision Journal

This journal records the significant decisions behind the StoreOps harness and the corrections made during the demonstration run (FEAT-sla-breach-alerting). Each entry uses the same fields: **Date, Context, Decision, Alternatives, Rationale, Assumption, Evidence path, Lesson.**

**How to read it.** Dates come from Git history (commit times are +0530) or run artefacts. Where the repository does not record a decision date, the entry says **TODO** rather than guessing. Anything marked *inferred* is my reading of the evidence, and anything marked **TODO (developer input)** needs your answer. The original, unedited text of ADR-004 and the hard-gate rationale is kept in Appendices A and B.

| ID | Decision or correction |
|---|---|
| J-01 | .NET technology stack |
| J-02 | `.harness/` separated from CI/CD configuration |
| J-03 | Event-bus hard gate (HG-04, with HG-03) |
| J-04 | 60/40 evaluation weighting (ADR-004) |
| J-05 | Three-iteration limit per sprint |
| J-06 | Context reset strategy |
| J-07 | Docker not available; deployment to Azure App Service |
| J-08 | Correction: Sprint 2 interruption (spend limit, SDK pin, diff base) |
| J-09 | Correction: `.harness/output/` committed with the ignore rule disabled |
| J-10 | Correction: final spec and contracts archived; journal file name fixed |

---

## J-01: .NET technology stack

- **Date:** 29 September 2026, as originally recorded. The first repository commit is 2026-10-02 (`7a9da5d`, "baseline: generated StoreOps scaffold") and already contained this entry, so the date predates the repository. **TODO (developer input): confirm the date.**
- **Context:** The brief (§3.2) requires choosing one stack from four options. Option D is C# 12 / .NET 8 with ASP.NET Core Web API, xUnit + `WebApplicationFactory`, and StyleCop with nullable annotations.
- **Decision:** Use .NET 8, C# 12, ASP.NET Core Web API, xUnit and `WebApplicationFactory` for the StoreOps application.
- **Alternatives:** Node.js, Java, Python (the other options in the brief §3.2).
- **Rationale:** ".NET was selected because it aligns with my strongest existing development experience and provides good alignment with Microsoft enterprise technologies. This allows me to focus on architecture, governance, testing, and the Claude Code harness instead of spending additional effort learning a new technology stack."
- **Assumption:** *Inferred from the rationale:* existing .NET experience outweighs any benefit of another stack, so effort goes into the harness. **TODO (developer input): confirm.**
- **Evidence path:** `Requirement_Doc/AI_Native_Architect_Build_Track_SA_M_SM_AD.pdf` §3.2; `global.json` (SDK pin 8.0.425); `Directory.Build.props` (`net8.0`, nullable enabled); commit `7a9da5d`.
- **Lesson:** An exact SDK pin is a precondition the harness must check. It blocked Sprint 2 (see J-08).

## J-02: `.harness/` separated from CI/CD configuration

- **Date:** Decision date **TODO**. First implemented 2026-10-08 (`a1856df`, "harness: add orchestrator agents and StoreOps skills", 23:02).
- **Context:** The brief (§2.2) says `.harness/` is used instead of `.github/` "to keep harness files separate from CI/CD configuration", as a deliberate structural choice. Brief §7 puts working files in `.harness/output/` (gitignored) and sprint archives in `.harness/reviews/` (committed).
- **Decision:** All harness files live under `.harness/` (`agents/`, `skills/`, `output/`, `reviews/`). `.harness/output/` is git-ignored working state; `.harness/reviews/` is the committed audit history. There is no `.github/` folder.
- **Alternatives:** Store harness files under `.github/` alongside CI/CD assets.
- **Rationale:** I wanted a clear separation between AI harness governance files and CI/CD configuration files. The brief (§2.2) states the same aim, and CLAUDE.md §9 adds that the evaluator's checks run before CI and feed it, and do not replace it.
- **Assumption:** CI will be added separately later. CLAUDE.md §9: "There's no CI pipeline in this repository yet."
- **Evidence path:** `CLAUDE.md` §8, §9; `.gitignore` (`.harness/output/*`); brief §2.2 and §7; commit `a1856df`.
- **Lesson:** The boundary between ignored and committed files proved fragile: the output folder was committed by mistake (J-09) and the final plan files had to be archived by copy (J-10).

## J-03: Event-bus hard gate (HG-04, with HG-03)

- **Date:** Decision date **TODO**. The architecture tests exist from the baseline (2026-10-02, `7a9da5d`); the gates were written into the evaluator skills on 2026-10-08 (`a1856df`).
- **Context:** Two of the four failure modes in the brief §2: **FM-1** direct imports from another module's repository, and **FM-4** state changes written straight to sibling modules instead of going through the event bus. Brief §3.5 lists "Event bus only" and suggests it be LLM-assessed.
- **Decision:** HG-03 (no cross-module repository access) and HG-04 (cross-module side effects only through `IEventBus`) are hard gates: any failure is a FAIL verdict. Cross-module reads use read-only `*Lookup` contracts, and each contract-named event is constructed only inside `eventBus.PublishAsync(`.
- **Alternatives:** An LLM-assessed soft check, as the brief suggests (§3.5). Rejected in favour of automated architecture tests plus a grep.
- **Rationale:** See "HG-03" and "HG-04" in Appendix B: violating module boundaries "undermines the modular architecture", and event-driven communication "must remain consistent across all features".
- **Assumption:** Reflection over constructor, field, property and method signatures, plus a source scan for `using` directives, is enough to detect cross-module dependencies (see the comments in `ModuleRules.cs`).
- **Evidence path:** `.harness/skills/evaluation-criteria/SKILL.md` §2; `.harness/skills/architecture-principles/SKILL.md` (ARCH-02 to ARCH-04); `tests/StoreOps.Api.Tests/Architecture/ModuleBoundaryTests.cs`; HG-03 and HG-04 PASS in all seven `.harness/reviews/sprint-N-iteration-1-evaluator-feedback.md`; `docs/proposals/architecture-test-messages.md`.
- **Lesson:** An injected `Activities → Alerts` repository reference failed the same four tests twice, so the gate is deterministic. Its failure messages are not actionable (truncated, no rule ID), which is the limitation in `REFLECTION.md`.

## J-04: 60/40 evaluation weighting (ADR-004)

- **Date:** Decision date **TODO**. Implemented 2026-10-08 (`a1856df`, `evaluation-criteria` §4).
- **Context:** A deterministic evaluation framework is needed so that AI-generated code stays maintainable, testable and aligned with the intended architecture (ADR-004, Appendix A).
- **Decision:** Architecture Compliance 60%, Quality and Testing 40%. The score is reported but never decides the verdict.
- **Alternatives:** 50/50 (rejected: treats a boundary violation and a functional defect as equally severe). 40 Architecture / 60 Testing (rejected: architecture is the primary governance mechanism).
- **Rationale:** "A fully tested implementation that breaks architectural constraints is still considered unacceptable" (ADR-004).
- **Assumption:** The hard gates and mandatory checks, not the weights, carry the accept/reject decision. `evaluation-criteria` §4 states that the score "never decides the verdict".
- **Evidence path:** `.harness/skills/evaluation-criteria/SKILL.md` §4; evaluator feedback §1 in each sprint; run logs (`scores: { architecture: 60, quality: 40, total: 100 }`).
- **Lesson:** All seven sprints scored 100/100, so the weighting never discriminated between sprints. Its value rests on the gates (DESIGN_BRIEF.md, D3).

## J-05: Three-iteration limit per sprint

- **Date:** Decision date **TODO**. Written into `CLAUDE.md` 2026-10-08 (`a1856df`).
- **Context:** The brief (§2.1, §5.1) says the orchestrator escalates "after a maximum of three iterations" and that the escalation output is written to `.harness/output/escalation.md`.
- **Decision:** "Maximum three iterations per sprint. Only `RETRY S<N>` grants more" (CLAUDE.md §4). A third FAIL, or a blocked or malformed generator or evaluator output, escalates under §6 and stops the run.
- **Alternatives:** **TODO (developer input): none are recorded in the repository.**
- **Rationale:** Three iterations provided a balance between autonomous correction, cost control, and escalation visibility. Beyond three iterations, repeated retries were unlikely to produce materially different outcomes without human intervention. (The brief, §2.1 and §5.1, also prescribes a maximum of three iterations.)
- **Assumption:** *Inferred:* a sprint that fails three times needs human judgement, not another automatic retry. **TODO: confirm.**
- **Evidence path:** `CLAUDE.md` §4, §6, §8 (escalation schema); the seven `sprint-N-run-log.md` files (`iterations_used: 1`, `escalation: false`).
- **Lesson:** The limit was never reached: every sprint passed on iteration 1, so the retry and escalation paths are untested in a real run (REFLECTION.md). In Sprint 2 a `BLOCKED` summary should have escalated and was handled by judgement instead (J-08).

## J-06: Context reset strategy

- **Date:** Decision date **TODO**. Written into `CLAUDE.md` 2026-10-08 (`a1856df`).
- **Context:** The brief (§5.1) requires a strategy for resetting or bounding context between agent invocations "to prevent context window degradation across long runs".
- **Decision:** CLAUDE.md §7: each agent runs as a fresh subagent that reads only the inputs its agent file lists. The orchestrator reads only line 1 and headings of the handoff files, plus `git` status. Nothing carries over between sprints, and between iterations only the feedback file carries over. The generator runs single test classes while iterating, and the full suite once.
- **Alternatives:** **TODO (developer input): none are recorded in the repository.**
- **Rationale:** The brief's reason (above). CLAUDE.md §7: "All state lives in files".
- **Assumption:** The handoff files hold all state needed to resume. CLAUDE.md §7 asserts this.
- **Evidence path:** `CLAUDE.md` §7; the Inputs sections of `.harness/agents/*.agent.md`; `token_cost` basis in `.harness/reviews/sprint-N-run-log.md`; `.harness/output/history/`.
- **Lesson:** Keeping state in files made the interrupted Sprint 2 session recoverable: work resumed from the contract, summaries and git history (J-08). The cost is only an ESTIMATED lower bound, because the run logs count only the required agent inputs and outputs.

## J-07: Docker not available; deployment to Azure App Service

- **Date:** Docker constraint: **TODO** (recorded in the original Decision 001, undated). Deployment: 2026-10-12 (the Cloud Shell deployment started 2026-10-12T16:49:33Z).
- **Context:** The brief (§3.4) accepts local Docker or a cloud platform (AWS, Azure, GCP). The development machine has no Docker CLI or runtime: `docker --version` returned a CommandNotFoundException. The repository has a `Dockerfile`.
- **Decision:** Proceed with build, test and local verification using the .NET 8 SDK, then deploy to **Azure App Service** (Windows, Standard S1, India South Central, subscription "Learner Account") by zip deploy from the portal's Cloud Shell. The `Dockerfile` was not built or run.
- **Alternatives:** Local Docker deployment (not possible: no Docker runtime on the development machine). Other cloud targets such as AWS and GCP. Running only with `dotnet run` locally was not an option, because the brief (§3.4) accepts only Docker or a cloud platform.
- **Rationale:** Azure App Service provided a straightforward deployment path for the .NET 8 application and aligned with my Microsoft-focused technology background. An Azure learner subscription was available, and the brief accepts cloud deployment. **TODO (developer input): why the Windows Standard S1 plan was chosen.** Original Decision 001: "This does not block StoreOps development or harness implementation. It only affects the final deployment and deployment-evidence stage."
- **Assumption:** A single instance, because SLA state is in memory (spec ASM-07, OOS-08). The learner URL may stop responding once the subscription is retired.
- **Evidence path:** `DEPLOYMENT.md` §1 to §6 and Figures 1 to 6; `Screenshots/`; `Dockerfile`; `.harness/output/spec.md` (ASM-07).
- **Lesson:** Keep commands portable: a first Cloud Shell attempt failed because the macOS-only `date -v` option does not exist on Linux (`DEPLOYMENT.md` §4). The `Dockerfile` build image is not pinned to SDK 8.0.425, the same risk as in J-01.

## J-08: Correction: Sprint 2 interruption (spend limit, SDK pin, diff base)

- **Date:** 2026-10-11 (code committed as `1a178c9` at 18:08).
- **Context:** The first Sprint 2 generator run stopped on an API spend-limit error (HTTP 429) before writing a summary, leaving partial work in the tree. A later generator attempt wrote `STATUS: BLOCKED` because the SDK pinned in `global.json` (8.0.425) was not installed; only 8.0.422 was. The developer had already committed the partial code as `1a178c9`.
- **Decision:** CLAUDE.md §4 and §6 would escalate a `BLOCKED` summary (`GENERATOR_BLOCKED`). Instead the orchestrator stopped and reported, the developer installed SDK 8.0.425, and the generator was re-run to verify the committed code and write the summary. Because `1a178c9` already contained the Sprint 2 code, `git diff HEAD` was empty, so the evaluator used `d27f765` (the Sprint 1 commit) as the diff base. No `escalation.md` was written.
- **Alternatives:** Formal escalation (`GENERATOR_BLOCKED`, then `RETRY S2`). Resetting to `d27f765` and regenerating Sprint 2 from scratch: offered during the session and not chosen (not recorded in a repository file).
- **Rationale:** It was an environment setup problem, not a feature-design or architecture problem, and the committed code was kept.
- **Assumption:** The interrupted run's code was complete. The verification run (all Sprint 2 ACs, build, 75/75 tests) and the evaluator's independent re-run confirmed it.
- **Evidence path:** `.harness/reviews/sprint-2-iteration-1-generator-summary.md`; `.harness/reviews/sprint-2-iteration-1-evaluator-feedback.md` (diff-base note); `.harness/reviews/sprint-2-run-log.md`; commits `1a178c9`, `d27f765`; `global.json`. The `BLOCKED` summary itself was overwritten and not archived.
- **Lesson:** *Proposed:* add a `dotnet --version` versus `global.json` check to pre-flight; archive a `BLOCKED` summary before overwriting it; and define how the loop treats work committed outside it, so the `d27f765` diff base is not a one-off deviation (REFLECTION.md).

## J-09: Correction: `.harness/output/` committed with the ignore rule disabled

- **Date:** 2026-10-11 (committed at 20:18 in `7a43bbe`; fixed at 20:45 in `094d445` and 20:47 in `a22918f`).
- **Context:** Before Sprint 2 could start, `git check-ignore -q .harness/output/spec.md` failed: the rule `.harness/output/*` was commented out in `.gitignore` and the working files were committed.
- **Decision:** Untrack `.harness/output/` (keeping `.gitkeep`), re-enable the ignore rule, and commit before any sprint ran.
- **Alternatives:** Keep the output files tracked on purpose, which would contradict CLAUDE.md §4 ("`.harness/output/` is git-ignored, so it doesn't count") and make the working tree dirty after every run.
- **Rationale:** The evaluator diffs against `HEAD`; tracked working files would appear as changes and fail the scope check (MC-05).
- **Assumption:** The developer commits between sprints and keeps `.harness/output/` ignored.
- **Evidence path:** commits `7a43bbe`, `094d445`, `a22918f`; `.gitignore`; `CLAUDE.md` §4 (pre-flight).
- **Lesson:** The pre-flight check stopped the run on a real fault before any agent was spent (REFLECTION.md).

## J-10: Correction: final spec and contracts archived; journal file name fixed

- **Date:** 2026-10-12 (`e4aa2c7` at 18:25 and `eada136`).
- **Context:** After commit `094d445`, `spec.md` and the seven sprint contracts existed only on disk (ignored) and in history (`7a43bbe`, with non-final statuses). The brief (§5.5, §6.3) requires them committed. The journal was also tracked as `Journal.md`, not the required `JOURNAL.md`.
- **Decision:** Copy the final files unchanged into `.harness/reviews/FEAT-sla-breach-alerting/` and commit them (`e4aa2c7`); rename the journal to `JOURNAL.md` (`eada136`).
- **Alternatives:** `git add -f` on the ignored originals: rejected because tracked files would turn dirty when the next feature run overwrites them, which fails the clean-tree pre-flight (CLAUDE.md §4).
- **Rationale:** Satisfies the brief without weakening the ignore rule.
- **Assumption:** A final-state copy meets the brief's "committed to the repository" requirement; the original approved versions remain in history at `7a43bbe`.
- **Evidence path:** commits `e4aa2c7`, `eada136`; `.harness/reviews/FEAT-sla-breach-alerting/`; brief §5.5 and §6.3.
- **Lesson:** *Proposed:* make archiving the plan files part of the end-of-feature step (CLAUDE.md §5.1, when `spec.md` is set to `COMPLETE`).

---


# Appendix A: ADR-004 as originally recorded (unedited)

# Architecture Decision Record - Evaluation Framework

## Decision ID: ADR-004
### Title
Evaluation Weighting and Hard Gate Strategy

### Context
The StoreOps AI Harness uses AI agents to plan, generate, and evaluate software changes. To ensure that AI-generated code remains maintainable, testable, and aligned with the intended architecture, a deterministic evaluation framework is required.

### Decision
The evaluation framework uses:

- Architecture Compliance: 60%
- Quality and Testing: 40%

### Rationale

Architecture Compliance is weighted higher because architectural defects can create long-term maintainability, scalability, and governance issues across the solution. A feature may appear to work correctly while violating architectural boundaries, creating technical debt that becomes increasingly difficult to resolve.

The StoreOps solution is designed around:
- Controller -> Service -> Repository layering
- Event-driven communication through IEventBus
- Module isolation
- Typed AppError handling
- Defined dependency boundaries

These principles are foundational to the solution and must be preserved regardless of feature requirements.

Quality and Testing is weighted at 40% because functional correctness and automated validation are essential, but they cannot compensate for architectural violations. A fully tested implementation that breaks architectural constraints is still considered unacceptable.

### Alternatives Considered

#### Option 1: 50/50 weighting
Rejected because it treats architectural violations and functional defects as equally severe.

#### Option 2: 40 Architecture / 60 Testing
Rejected because architecture is the primary governance mechanism of the harness and must remain the dominant evaluation factor.

#### Option 3: 60 Architecture / 40 Testing
Selected because it prioritizes long-term system integrity while still placing significant emphasis on quality and testing.

---

---

# Appendix B: Hard gate rationale as originally recorded (unedited)

# Hard Gate Rationale

## HG-01: Build Must Succeed

Failure Mode Prevented:
AI generates code that cannot compile.

Why It Cannot Be a Soft Check:
A solution that does not build cannot be deployed, tested, or evaluated further.

---

## HG-02: Tests Must Pass

Failure Mode Prevented:
AI introduces functional regressions or broken behavior.

Why It Cannot Be a Soft Check:
Failing tests indicate the implementation does not satisfy expected system behavior.

---

## HG-03: No Cross-Module Repository Access

Failure Mode Prevented:
Tight coupling between modules.

Why It Cannot Be a Soft Check:
Violating module boundaries undermines the modular architecture and increases long-term maintenance costs.

Example Prevented:
Activities -> AlertRepository

Allowed:
Activities -> EventBus -> Alerts

---

## HG-04: Cross-Module Side Effects Must Use IEventBus

Failure Mode Prevented:
Direct module-to-module dependencies.

Why It Cannot Be a Soft Check:
Event-driven communication is a core architectural principle of StoreOps and must remain consistent across all features.

---

## HG-05: No Raw Expected Error Handling

Failure Mode Prevented:
Inconsistent error behavior and unpredictable API responses.

Why It Cannot Be a Soft Check:
The system relies on typed AppError objects for deterministic handling of business exceptions.

Rejected Pattern:
throw new Exception(...)

Required Pattern:
ValidationError
NotFoundError
ConflictError

---

## HG-06: Layering Rules Must Be Preserved

Failure Mode Prevented:
Business logic leaking into Controllers or data access bypassing Services.

Why It Cannot Be a Soft Check:
Controller -> Service -> Repository separation is a fundamental architectural requirement.

---

## HG-07: Reports Module Must Remain Read-Only

Failure Mode Prevented:
Reporting functionality modifying operational data.

Why It Cannot Be a Soft Check:
Reports are designed as a read-only module and must not affect transactional workflows.

---

## HG-08: Coverage Thresholds Must Be Met

Failure Mode Prevented:
Insufficient verification of AI-generated code.

Why It Cannot Be a Soft Check:
The evaluation framework depends on objective evidence that the implemented behavior has been tested.

Required Thresholds:
- Service Coverage >= 80%
- Controller Coverage >= 70%
- Shared Utilities Coverage >= 60%
- Overall Coverage >= 70%

---

## Expected Outcome

The combination of hard gates and weighted scoring ensures that AI-generated changes cannot bypass critical architectural or quality requirements. A feature is accepted only when it satisfies both functional expectations and architectural governance standards.
