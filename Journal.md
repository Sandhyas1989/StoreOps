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
