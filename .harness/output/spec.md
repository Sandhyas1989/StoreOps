STATUS: APPROVED
# FEAT-sla-breach-alerting: SLA breach alerting

## 1. Request
`@planner Add SLA breach alerting to StoreOps using PROMPT.md` (PROMPT.md, 1109 bytes, modified 2026-10-03 13:03). Key requirements: an open HIGH or CRITICAL activity that passes its due date publishes an SLA_BREACH event; an alert goes to the Department Lead; escalate to the Store Manager after a configurable grace period; no duplicate alerts per breach stage; keep Controller → Service → Repository, `IEventBus` and `AppError`; business-rule, event-payload, negative-path and escalation tests; evidence that all coverage gates and checks pass. Out of scope: email, SMS, external notifications.

## 2. Summary
- **Activities** (owner) detects breaches and escalations. It stores per-activity SLA stage in a new `ISlaRecordRepository` and publishes a new `ActivitySlaBreached` event with `Stage = Breach | Escalation`.
- **Evaluation runs** through a new `POST /api/activities/sla-evaluations` and a new background worker, both calling `ISlaEvaluationService`.
- **Alerts** (consumer) turns each event into recipient-addressed alerts (`SLA_BREACH` / `SLA_ESCALATION`), resolving recipients via `IStaffLookup`. This adds the dependency edge `Alerts → Staff`, which keeps the graph acyclic.
- **Entities changed:**
  - `ActivityPriority` gets `Critical` (Q1);
  - `Alert` and `AlertResponse` get `RecipientStaffId` and `RecipientRole` (additive);
  - `GET /api/alerts` gets a `recipientStaffId` filter.

## 3. Requirements
| ID | Requirement (objective) | Module | Rules |
|---|---|---|---|
| REQ-01 | An activity is **in breach** when `Priority ∈ {High, Critical}`, `Status ∈ {Pending, InProgress}`, `DueAt` is not null and `now > DueAt` (`now` from `TimeProvider`). All other activities are never in breach. | Activities | ARCH-01 |
| REQ-02 | `POST /api/activities/sla-evaluations` evaluates every activity and returns 200 with `evaluatedAt`, `newlyBreached[]` and `newlyEscalated[]` (activity ids whose stage advanced in this call). | Activities | ARCH-01, ARCH-05 |
| REQ-03 | On reaching the Breach stage, exactly one `ActivitySlaBreached` is published, with `ActivityId, Title, StoreId, Priority, DueAt, AssignedStaffId, Stage=Breach, OccurredAt`. | Activities | ARCH-04 |
| REQ-04 | A breached activity that is still open when `now ≥ DueAt + EscalationGracePeriod` reaches the Escalation stage and publishes exactly one `ActivitySlaBreached` with `Stage=Escalation`. It never escalates before that time, or after being closed. | Activities | ARCH-04 |
| REQ-05 | `Sla:EscalationGracePeriod` (TimeSpan) is configurable, defaults to `00:30:00`, and must be `> 00:00:00`. An invalid value stops startup with an options-validation error. | Activities | ARCH-05 |
| REQ-06 | Each stage is reached at most once per activity, including when evaluations run concurrently (atomic stage advance in the repository). | Activities | ARCH-04 |
| REQ-07 | A hosted worker runs the evaluation every `Sla:EvaluationInterval` (default `00:01:00`) when `Sla:WorkerEnabled` is true (default). | Activities | ARCH-01 |
| REQ-08 | `ActivityPriority.Critical` can be set on POST and PATCH and is returned by the activity endpoints. Existing priorities behave as before. | Activities | ARCH-05 |
| REQ-09 | A Breach event creates one `SLA_BREACH` / `Warning` alert per ShiftLead ("Department Lead") in the activity's store, with `RecipientStaffId` and `RecipientRole=ShiftLead`. The alert is visible in `GET /api/alerts` when the evaluation call returns. | Alerts | ARCH-03, ARCH-04 |
| REQ-10 | An Escalation event creates one `SLA_ESCALATION` / `Critical` alert per StoreManager in the activity's store. | Alerts | ARCH-03, ARCH-04 |
| REQ-11 | At most one alert exists per (activity, stage, recipient), even if the same event is delivered twice. | Alerts | TEST-01 |
| REQ-12 | `GET /api/alerts?recipientStaffId={guid}` returns only that recipient's alerts. A malformed id returns 400 `VALIDATION_FAILED` with `details.recipientStaffId`. | Alerts | ARCH-05 |
| REQ-13 | Every sprint ends with all hard gates PASS (ARCH-01..07, TEST-01, COV-01..04), with the four COV percentages recorded. | All | all |

## 4. Sprint plan
| Sprint | Goal (one vertical slice) | Owning module | REQs | Depends on |
|---|---|---|---|---|
| S1 | Accept `Critical` activity priority | Activities | REQ-08, REQ-13 | none |
| S2 | SLA evaluation endpoint detects breaches (no event yet) | Activities | REQ-01, REQ-02, REQ-06, REQ-13 | S1 |
| S3 | Publish `ActivitySlaBreached` (Breach) | Activities | REQ-03, REQ-06, REQ-13 | S2 |
| S4 | Escalation after the configurable grace period | Activities | REQ-02, REQ-04, REQ-05, REQ-06, REQ-13 | S3 |
| S5 | Background worker runs evaluation automatically | Activities | REQ-07, REQ-13 | S4 |
| S6 | Alerts consumes the event: Department Lead and Store Manager alerts, deduplicated | Alerts | REQ-09, REQ-10, REQ-11, REQ-13 | S4 |
| S7 | Filter alerts by recipient | Alerts | REQ-12, REQ-13 | S6 |

## 5. Traceability
| REQ | ACs |
|---|---|
| REQ-01 | AC-2.01, AC-2.02, AC-2.03, AC-2.04 |
| REQ-02 | AC-2.01, AC-2.05, AC-4.01 |
| REQ-03 | AC-3.01, AC-3.02, AC-3.03 |
| REQ-04 | AC-4.01, AC-4.02, AC-4.03, AC-4.04, AC-4.08 |
| REQ-05 | AC-4.05, AC-4.06 |
| REQ-06 | AC-2.05, AC-2.06, AC-3.02, AC-4.04 |
| REQ-07 | AC-5.01, AC-5.02 |
| REQ-08 | AC-1.01, AC-1.02, AC-1.03 |
| REQ-09 | AC-6.01, AC-6.02 |
| REQ-10 | AC-6.03 |
| REQ-11 | AC-6.04 |
| REQ-12 | AC-7.01, AC-7.02 |
| REQ-13 | AC-1.04, AC-2.07, AC-3.04, AC-4.07, AC-5.03, AC-6.05, AC-7.03 |

## 6. Dependencies
- **DEP-01** Package `Microsoft.Extensions.TimeProvider.Testing` 8.x in the test project, for `FakeTimeProvider`. Without it, due-date and grace-period tests can't control time; S2–S6 are blocked.
- **DEP-02** `IStaffLookup.ListAsync` (existing) provides role and store for recipient resolution in S6. No new lookup method is needed.
- **DEP-03** `SharedInfrastructureExtensions` registers `TimeProvider` with `TryAddSingleton`, so tests can replace it. If that changes, clock control breaks.
- **DEP-04** Each sprint depends on the previous sprints listed in §4.

## 7. Out of scope
- **OOS-01** Email, SMS and any external notification system (PROMPT.md).
- **OOS-02** A department model. "Department Lead" maps to `StaffRole.ShiftLead` (ASM-03).
- **OOS-03** Alert acknowledgement, resolution or closing endpoints.
- **OOS-04** SLA durations per priority. The SLA deadline is `Activity.DueAt` only.
- **OOS-05** Escalation beyond Store Manager, and repeated reminders after Escalation.
- **OOS-06** SLA metrics in `GET /api/reports/summary`.
- **OOS-07** Changes to the existing `HIGH_PRIORITY_ACTIVITY` alert, including for `Critical` priority.
- **OOS-08** Persistence beyond memory. SLA stage is lost on restart (ASM-07).

## 8. Risks
| ID | Trigger | Impact | Mitigation (sprint) | Rule |
|---|---|---|---|---|
| RISK-01 | Replacing `TimeProvider` with a `FakeTimeProvider` in `StoreOpsApiFactory` | Equal `CreatedAt` values break ordering assertions in existing tests; ACs written with times earlier than the Clock, or exact "now" values, can't be executed | Set `AutoAdvanceAmount = 1 ms`; the full existing suite must stay green (S2). All ACs follow the test clock convention in sprint-2-contract §1: start 09:00Z, forward-only, timestamps bounded or equated, `dueAt 08:45Z` for within-grace and `08:00Z` for past-grace | TEST-01 |
| RISK-02 | The worker and the endpoint evaluate at the same time | The same stage is published twice, causing duplicate alerts | Atomic `TryAdvanceStage` in `ISlaRecordRepository` (S2–S4) plus idempotency in Alerts (S6) | ARCH-04 |
| RISK-03 | Additive fields on `AlertResponse` and a new `ActivityPriority` value | Clients of baseline endpoints #1–5 and #9 see new values | Additive only; existing tests must pass unchanged; needs approval (Q1, Q4) | ARCH-05 |
| RISK-04 | The worker test depends on timer scheduling | Flaky test | Drive the `PeriodicTimer` through `FakeTimeProvider.Advance`; wait on the recorder with a bounded timeout, with no wall-clock sleeps (S5) | TEST-01 |
| RISK-05 | Alerts calls Activities or Staff for anything other than reads | Rule violation | Alerts uses only the `ActivitySlaBreached` payload and `IStaffLookup` (S6) | ARCH-02, ARCH-03 |
| RISK-06 | New SLA code paths aren't fully exercised | A COV gate fails | Every AC has a named test; COV values are recorded per sprint | COV-01..04 |
| RISK-07 | The new background worker class is named `*Service` | The ARCH-01 layering rule fails (`*Service` must depend on a repository) | Name it `SlaEvaluationWorker` (S5) | ARCH-01 |

## 9. Assumptions
| ID | Assumption | If wrong |
|---|---|---|
| ASM-01 | "CRITICAL" means adding `ActivityPriority.Critical` | S1 is WITHDRAWN and REQ-01 becomes High-only |
| ASM-02 | "not DONE" means not `Completed` and not `Cancelled` | REQ-01 and REQ-04 status sets change |
| ASM-03 | "Department Lead" means every `ShiftLead` whose `StoreId` equals the activity's `StoreId` (not the assignee) | Recipient resolution in REQ-09 changes |
| ASM-04 | The grace period runs from `DueAt`, not from when the breach was detected | REQ-04 timing changes |
| ASM-05 | Breach alert severity is `Warning`; Escalation severity is `Critical` | REQ-09 and REQ-10 values change |
| ASM-06 | If a store has no ShiftLead (or StoreManager), no alert is created for that stage, and the event is still published | REQ-09 and REQ-10 need a fallback recipient |
| ASM-07 | SLA stage is in memory; after a restart, still-open breached activities breach again | Needs persistence (OOS-08) |
| ASM-08 | The event type is `ActivitySlaBreached` with a `Stage` field. "SLA_BREACH" names the alert type. | Rename only; behaviour is unchanged |

## 10. Open questions
All answered on 2026-10-03:
- **Q1** Add `Critical` to `ActivityPriority`? **Yes** (confirms ASM-01; S1 is in scope).
- **Q2** Who is the "Department Lead"? **The ShiftLead for the activity's store** (confirms ASM-03).
- **Q3** Grace period start and default? **From `DueAt`, 30-minute default** (confirms ASM-04). The worker interval wasn't addressed and keeps its `00:01:00` default (S5).
- **Q4** Additive alert response fields and the recipient filter? **Accepted** (RISK-03 approved).

## 11. Approval
Reply `APPROVED` to accept, or list changes.

**Revision 2 (2026-10-03):** acceptance criteria made executable against a forward-only test clock. Changed: AC-2.01, 2.02, 2.03, 2.04, 2.05, 2.06; AC-3.01, 3.02, 3.03; AC-4.01, 4.02, 4.03, 4.04, 4.05; AC-5.01, 5.02; AC-6.02, 6.03, 6.04; AC-7.01 (setup made explicit). Added the test clock convention (sprint-2-contract §1, RISK-01) and recorded the Q1–Q4 answers. No REQ, AC or sprint IDs, sprint boundaries, or traceability rows changed.
