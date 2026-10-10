# Architecture Decision Journal
 
## Architecture Decision 001 - Technology Stack
 
**Date:** 29 September 2026
 
**Status:** Accepted
 
**Decision:**
Use .NET 8, C# 12, ASP.NET Core Web API, xUnit, and WebApplicationFactory for the StoreOps application.
 
**Alternatives considered:**
- Node.js
- Java
- Python
 
**Rationale:**
.NET was selected because it aligns with my strongest existing development experience and provides good alignment with Microsoft enterprise technologies. This allows me to focus on architecture, governance, testing, and the Claude Code harness instead of spending additional effort learning a new technology stack.

**Current environment constraint:**
The Docker CLI and local Docker runtime are not currently installed or available on the development machine. This was verified using `docker --version`, which returned a CommandNotFoundException.
 
**Decision:**
Proceed with development, build, automated testing, and local API verification using the .NET 8 SDK. Before final submission, use either an organization-approved local Docker installation or an accepted cloud deployment target.
 
**Impact:**
This does not block StoreOps development or harness implementation. It only affects the final deployment and deployment-evidence stage.

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