Feature: SLA breach alerting.

Business Context:
Store operations teams need timely visibility when activities breach SLA commitments so that corrective actions can be taken quickly.

Requirements:
- When a HIGH or CRITICAL activity passes its due date and is not DONE, publish an SLA_BREACH event.
- Create an alert for the assigned Department Lead.
- If unresolved after a configurable grace period, escalate to the Store Manager.
- Prevent duplicate alerts for the same breach stage.
- Preserve Controller -> Service -> Repository architecture.
- Use IEventBus for cross-module side effects.
- Use typed AppError behavior.
- Add business-rule tests.
- Add event-payload tests.
- Add negative-path tests.
- Add escalation tests.
- Require evidence that all coverage thresholds and automated checks pass.

Expected Outcomes:
- SLA breaches are detected reliably.
- Department Leads are notified immediately.
- Escalations occur only after the grace period.
- Duplicate alerts are prevented.

Out Of Scope:
- Email integration.
- SMS integration.
- External notification systems.
