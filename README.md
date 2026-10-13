# StoreOps API

A REST API for managing retail store operations. It is built with .NET 8, C# 12, and ASP.NET Core Web API, and stores all data in memory.

**Repository:** <https://github.com/Sandhyas1989/StoreOps> (branch `test/evaluator-hard-gates`)

## Modules

| Module     | Responsibility                           | Depends on (read-only lookups / events) |
|------------|------------------------------------------|-----------------------------------------|
| Staff      | Store staff (seeded, read-only)          | —                                       |
| Activities | Store work items (CRUD)                  | `IStaffLookup`                          |
| Programmes | Store initiatives and their members      | `IStaffLookup`                          |
| Alerts     | Alerts raised in response to events      | `ActivityCreated` event                 |
| Reports    | Read-only summaries across modules       | `IActivityLookup`, `IProgrammeLookup`, `IStaffLookup` (via `IReportDataRepository`) |
| Shared     | Errors (`AppError`), event bus           | —                                       |

Rules (checked by `tests/StoreOps.Api.Tests/Architecture/ModuleBoundaryTests.cs`; `ModuleRulesSelfTests.cs` proves each rule catches a known violation):

- Each module follows Controller → Service → Repository. Repositories live in `<Module>.Persistence`.
- Modules never reference or import another module's repository or `Persistence` namespace.
- Cross-module constructor dependencies must be read-only `*Lookup` interfaces.
- Cross-module side effects go through `IEventBus`.
- Reports is read-only and never publishes events.
- There are no circular module dependencies.

All errors return `{ "code", "message", "statusCode", "details"? }`.

## Endpoints

| Method | Route                            |
|--------|----------------------------------|
| GET    | /api/activities                  |
| POST   | /api/activities                  |
| GET    | /api/activities/{id}             |
| PATCH  | /api/activities/{id}             |
| DELETE | /api/activities/{id}             |
| GET    | /api/programmes                  |
| POST   | /api/programmes                  |
| POST   | /api/programmes/{id}/members     |
| GET    | /api/alerts                      |

Supporting endpoints: `GET /api/programmes/{id}`, `GET /api/staff`, `GET /api/staff/{id}`, `GET /api/reports/summary`, `GET /health`.

## Run

```bash
dotnet build
dotnet test
dotnet run --project src/StoreOps.Api        # http://localhost:5080
```

```bash
curl -X POST http://localhost:5080/api/activities \
  -H "Content-Type: application/json" \
  -d '{"title":"Freezer check","storeId":"STORE-001","priority":"High"}'
curl http://localhost:5080/api/alerts
```

## Docker

```bash
docker build -t storeops-api .
docker run --rm -p 8080:8080 storeops-api    # http://localhost:8080
```
