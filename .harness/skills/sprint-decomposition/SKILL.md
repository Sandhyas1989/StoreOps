---
name: sprint-decomposition
description: How to split a StoreOps feature into small vertical sprints, each independently verifiable, with GIVEN/WHEN/THEN acceptance criteria backed by objective evidence, plus the required IDs, dependencies, out-of-scope items, risks and assumptions. Used by the planner to write .harness/output/spec.md and sprint-N-contract.md.
---

# StoreOps sprint decomposition

Read with `app-context` (modules, entities, endpoints) and `architecture-principles` (rule IDs). Every sprint must leave the repository passing every hard gate.

## 1. Machine-readable IDs

| Item | Format | Example | Lives in |
|---|---|---|---|
| Feature | `FEAT-<kebab-slug>` | `FEAT-remove-programme-member` | spec |
| Requirement | `REQ-<nn>` | `REQ-02` | spec |
| Sprint | `S<N>` (1-based, execution order) | `S2` | spec, contract file `sprint-<N>-contract.md` |
| Acceptance criterion | `AC-<N>.<nn>` (`N` = sprint) | `AC-2.03` | contract |
| Dependency | `DEP-<nn>` | `DEP-01` | spec, contract |
| Out of scope | `OOS-<nn>` | `OOS-03` | spec |
| Risk | `RISK-<nn>` | `RISK-01` | spec, contract |
| Assumption | `ASM-<nn>` | `ASM-02` | spec, contract |

- IDs are never reused or renumbered after approval. Removed items stay in place, marked `WITHDRAWN`.
- Every `AC` lists the `REQ` it proves (`Traces: REQ-02`).
- Every `REQ` is covered by at least one `AC`.

## 2. What a sprint is

A sprint is one **vertical slice** that a client can observe through the API or a published event. It is not a layer: "add the repository method" alone is not a sprint.

A sprint must:
- Change **one owning module** across Controller → Service → `Persistence` repository, plus that module's tests (ARCH-01).
- Add **at most one** new or changed endpoint, **or** one new integration event with its publisher.
- Put an event's **consumer** (its `IEventHandler<T>` in another module) in its **own sprint** after the publisher's sprint (ARCH-04). The consumer sprint is verified through the consumer module's endpoint.
- Be verifiable on its own. After the sprint, `dotnet build StoreOps.sln` reports 0 warnings and 0 errors, `dotnet test StoreOps.sln` passes, and every hard gate passes (ARCH-01..07, TEST-01, COV-01..04). Nothing may depend on a later sprint to turn tests green.
- Have about 3–8 acceptance criteria. If it needs more, split it.

**Order** sprints along the module dependency graph:
1. Staff
2. Activities and Programmes
3. Alerts (consumers of events)
4. Reports, last, because it only reads (ARCH-06)

Never plan a sprint that needs a new edge creating a cycle (ARCH-07). Plan an event instead.

**Split triggers:**
- The sprint touches two modules' services. Split into a publisher sprint and a consumer sprint.
- The sprint mixes a new entity field with a new endpoint. Split into the field and its validation first, then the endpoint.
- The sprint needs a new `AppError` subtype. Put the subtype in a Shared-only sprint with its own unit test, or in the first sprint that uses it.

## 3. Acceptance criteria: GIVEN / WHEN / THEN

Each AC has exactly this shape. The example is hypothetical: the member-removal endpoint, the `ProgrammeMemberRemoved` event and the test don't exist yet.

```
AC-2.03  Traces: REQ-02  Rules: ARCH-04, TEST-01
GIVEN  programme P exists with member StaffSeed.AssociateId
WHEN   DELETE /api/programmes/{P.id}/members/{StaffSeed.AssociateId}
THEN   status 204
AND    GET /api/programmes/{P.id} returns memberStaffIds = []
AND    exactly one ProgrammeMemberRemoved event with ProgrammeId = P.id and StaffId = StaffSeed.AssociateId is published
EVIDENCE  ProgrammesEndpointsTests.Delete_member_removes_staff_and_publishes_event (dotnet test StoreOps.sln)
```

- **GIVEN** uses concrete data: seeded ids (`StaffSeed.*`), entities created in the test, enum values.
- **WHEN** is one HTTP call (method, route, body), or one published event for consumer sprints.
- **THEN/AND** are observable values only:
  - an HTTP status;
  - response fields;
  - an error `code` named by its constant (`ProgrammeService.NotFoundCode`), plus `details` keys for `VALIDATION_FAILED`;
  - the stored state read back through a `GET`;
  - the published events (`Factory.Events.Of<T>()`);
  - exact counts.
- **EVIDENCE** names the test class and method that will prove the AC, following the existing pattern `<Action>_<condition>_<outcome>` in `tests/StoreOps.Api.Tests/<Module>/<Module>EndpointsTests.cs`. The other allowed form is a named rule check (an architecture test, a grep from `architecture-principles`, or a COV value).
- Every write AC must also have a failure AC (validation, not-found or conflict) whose THEN includes "state unchanged" and "no event published" (TEST-01).

**Prohibited in any criterion or requirement:**
- Subjective or unmeasurable words: *works, correctly, properly, appropriate, as expected, robust, clean, intuitive, user-friendly, fast, efficient, scalable, handles gracefully, should support, etc.*
- Status-only THEN clauses, except the TEST-01 allowlist cases.
- Range assertions (`at least`, `>=`) on counts.
- "Manually verify", or screenshots as the only evidence.

A requirement is objective only if a failing test or a failing rule check can disprove it.

## 4. Required sections

`spec.md` and every `sprint-<N>-contract.md` must contain these items. Write `None` where nothing applies, but never leave a section out.

- **Dependencies (`DEP`):** prior sprints, existing types or endpoints relied on (for example `IStaffLookup.FindAsync`), packages. Each one says what blocks if it's missing.
- **Out of scope (`OOS`):** at least the nearby work deliberately excluded. Defaults when the feature doesn't explicitly ask for them:
  - SLA-breach evaluation (`Activity.DueAt` is unused);
  - persistence beyond memory;
  - authentication;
  - changes to the nine baseline endpoints' contracts.
- **Risks (`RISK`):** each has a trigger, impact, and the mitigation built into a sprint. StoreOps risks to check for:
  - cross-module coupling (ARCH-02/03/04);
  - lost updates in read-modify-write on in-memory repositories;
  - changes that break an existing endpoint's response shape;
  - coverage falling below COV thresholds.
- **Assumptions (`ASM`):** anything not stated in the feature request or the code. Each one names what would change if it's wrong. Unresolved assumptions are also listed as open questions in the spec.

## 5. Self-check before handing over

- [ ] Every sprint is one vertical slice in one owning module, and consumers are in separate sprints.
- [ ] Every sprint is green on its own. No AC depends on a later sprint.
- [ ] Every AC is GIVEN/WHEN/THEN with an EVIDENCE line, and none uses a prohibited word.
- [ ] Every REQ is traced by at least one AC, and every write AC has a failure AC.
- [ ] DEP, OOS, RISK and ASM are present in the spec and in every contract.
- [ ] No planned change contradicts an ARCH, TEST or COV rule. If one would, it's listed as a RISK with the rule ID and needs explicit approval.
