---
name: api-integration
description: StoreOps HTTP API conventions (routes, verbs, status codes, JSON shapes, query filters, PATCH semantics) and the exact mapping from AppError and model-binding failures to error responses. Load for sprints that add or change endpoints, request/response DTOs or error behaviour.
---

# StoreOps API integration

## Routes and verbs
- **Base route** `api/<module-plural-lowercase>`: `api/activities`, `api/programmes`, `api/staff`, `api/alerts`, `api/reports`. Sub-resources and actions use kebab-case nouns: `api/programmes/{id}/members`, `api/reports/summary`, and the planned `api/activities/sla-evaluations`.
- **Status codes by verb:**

  | Verb | Use | Success | Body |
  |---|---|---|---|
  | `GET` collection | list, optional filters | 200 | `*Response[]`, in a fixed order (activities oldest-first, alerts newest-first) |
  | `GET {id}` | one item | 200 | `*Response` |
  | `POST` collection | create | 201 + `Location` via `CreatedAtAction(nameof(GetById), …)` | created `*Response` |
  | `POST` sub-resource or action | add a member, run an evaluation | 200 | the updated parent `*Response`, or a result DTO |
  | `PATCH {id}` | partial update | 200 | updated `*Response` |
  | `DELETE {id}` | remove | 204 | none |

- **Every created resource has a `GET {id}`** so its `Location` header resolves (`ProgrammesController.GetById` exists for that reason).
- **Query filters** are nullable action parameters: `[FromQuery] ActivityStatus? status, [FromQuery] string? storeId`. A missing filter means "no filter". Several filters combine with AND.
- **Reports routes are `GET` only** (ARCH-06). `/health` and `/swagger` (Development only) are infrastructure, not module routes.

## JSON contract
- camelCase property names. Enums are strings (`"priority": "High"`), set by `JsonStringEnumConverter` in `Program.cs`.
- `DateTimeOffset` values are ISO 8601 with offset. `DateOnly` is `"yyyy-MM-dd"`.
- **Responses are `*Response` records built with `From(entity)`.** Never return entities directly.
- **Changes to existing responses must be additive.** Add a field or enum value; never rename or remove one. Each addition is recorded as a RISK in the spec (RISK-03 covers `recipientStaffId`, `recipientRole` and `Critical`).
- **Request DTOs** are records with nullable members (see coding-conventions). Validation lives in the service.
- **PATCH semantics:** an omitted or `null` field is left unchanged (`UpdateActivityRequest`). There's no way to clear a field. Changing that is a contract change and needs a spec.
- Collections in responses are never `null`; they serialise as `[]`.

## Error mapping
The body is always `ErrorResponse { code, message, statusCode, details? }`, and `statusCode` equals the HTTP status.

| Source | Status | `code` | `details` |
|---|---|---|---|
| `ValidationError` (thrown by a service, usually via `ValidationErrorBuilder.ThrowIfAny`) | 400 | `VALIDATION_FAILED` | field → messages, camelCase keys (`title`, `storeId`, `assignedStaffId`, `staffId`, `endDate`) |
| Model binding: missing body, `null` body, malformed JSON | 400 | `VALIDATION_FAILED` | `{ "body": ["The request body is missing or is not valid JSON."] }` (`ModelStateErrorFactory.BodyField` / `BodyInvalidMessage`) |
| Model binding: bad value at a JSON path (e.g. unknown enum `"priority": "Urgent"`) | 400 | `VALIDATION_FAILED` | `{ "priority": ["The value is invalid."] }` (`ValueInvalidMessage`); no CLR type names |
| Model binding: bad route or query value (`/api/activities/not-a-guid`, `?severity=Bogus`) | 400 | `VALIDATION_FAILED` | keyed by parameter name (`id`, `severity`) |
| `NotFoundError` | 404 | `<ENTITY>_NOT_FOUND`: `ACTIVITY_NOT_FOUND`, `PROGRAMME_NOT_FOUND`, `STAFF_NOT_FOUND` | none |
| `ConflictError` | 409 | state-specific: `ACTIVITY_CLOSED`, `PROGRAMME_MEMBER_EXISTS` | none |
| Any other exception | 500 | `INTERNAL_ERROR` | none; message is `"An unexpected error occurred."` |

- **404 only for the addressed resource.** An unknown id in the route returns 404. An unknown id *inside the body* (an assignee or member) is a field error, `400 VALIDATION_FAILED`.
- **409 for a valid request that conflicts with current state.** For example, editing a closed activity or adding a duplicate member.
- **Messages** name the entity and id (`"Activity '{id}' was not found."`). Clients must branch on `code`, never on `message`.
- **New codes** are `SCREAMING_SNAKE` constants on the owning service. Document each one in the sprint contract's ACs.

## Swagger
- `/swagger` and `/swagger/v1/swagger.json` are served only in Development.
- `SwaggerTests` asserts that the nine baseline endpoints appear. Changed endpoints must still appear.
- New endpoints appear automatically through `AddEndpointsApiExplorer`. Don't hand-edit any documentation.
