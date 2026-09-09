git # TCMD

TCMD stands for **Training Center Management Dashboard**. It is an internal web application for one training center. The approved V1 scope is described in [`docs/`](docs/).

## Current status

Milestone 7 completes standalone Training Group management: authorized operational staff can create, retrieve, list, search, update, activate, complete, and cancel groups connected to existing courses and primary instructors, with validated status transitions and SQL Server optimistic-concurrency protection. The implemented history is:

- Milestone 1: executable API, SQL Server, migrations, health, OpenAPI, and integration-test foundation;
- Milestone 2: Student registration and retrieval vertical slice;
- Milestone 3A: ASP.NET Core Identity cookie authentication, roles, bootstrap administrator, and protected endpoints;
- Milestone 3B: Administrator-only staff-account administration;
- Milestone 4: standalone Student list/search, update, deactivation, and rowversion concurrency handling; and
- Milestone 5: standalone Instructor creation, list/search, retrieval, update, deactivation, and rowversion concurrency handling; and
- Milestone 6: standalone Course creation, list/search, retrieval, update, deactivation, code uniqueness, and rowversion concurrency handling; and
- Milestone 7: standalone Training Group creation, list/search/filtering, retrieval, detail and assignment updates, activation, completion, cancellation, relationship validation, uniqueness, and rowversion concurrency handling.

Milestone 8 is Enrollment management. Enrollment, session, attendance, staff-account linking, Instructor self-service, and browser-interface features remain later milestones.

## Solution structure

- `src/TCMD.Api` is the ASP.NET Core executable. It owns HTTP endpoints, startup, configuration, dependency registration, middleware, health checks, and OpenAPI generation.
- `src/TCMD.Application` owns the student, instructor, course, and training-group use cases, request/response models, and narrow persistence abstractions.
- `src/TCMD.Domain` owns the `Student`, `Instructor`, `Course`, and `TrainingGroup` entities and their business rules. It has no web or persistence dependencies.
- `src/TCMD.Infrastructure` owns EF Core, SQL Server, `TcmdDbContext`, entity configuration, persistence implementations, and migrations.
- `tests/TCMD.IntegrationTests` starts the real API pipeline with `WebApplicationFactory` and tests it through `HttpClient` against a dedicated SQL Server database.
- `tests/TCMD.DomainTests` contains focused tests for student, instructor, course, and training-group domain rules.

The dependency direction is `TCMD.Api -> TCMD.Application + TCMD.Infrastructure`, `TCMD.Infrastructure -> TCMD.Application + TCMD.Domain`, and `TCMD.Application -> TCMD.Domain`. The Domain project has no project dependencies. Infrastructure implements interfaces owned by Application, so use cases remain independent of EF Core and SQL Server.

## Important files

- `TCMD.slnx` groups the three projects so restore, build, and test commands can run from the repository root.
- `src/TCMD.Api/Program.cs` is the application entry point. It registers services and builds the ASP.NET Core middleware and endpoint pipeline.
- `src/TCMD.Api/Students/StudentEndpoints.cs`, `src/TCMD.Api/Instructors/InstructorEndpoints.cs`, `src/TCMD.Api/Courses/CourseEndpoints.cs`, and `src/TCMD.Api/TrainingGroups/TrainingGroupEndpoints.cs` map the standalone management endpoints and translate HTTP input and outcomes.
- `src/TCMD.Application/Students/`, `src/TCMD.Application/Instructors/`, `src/TCMD.Application/Courses/`, and `src/TCMD.Application/TrainingGroups/` contain the use cases and persistence abstractions.
- `src/TCMD.Domain/Students/Student.cs`, `src/TCMD.Domain/Instructors/Instructor.cs`, `src/TCMD.Domain/Courses/Course.cs`, and `src/TCMD.Domain/TrainingGroups/TrainingGroup.cs` contain the approved domain fields and rules.
- `src/TCMD.Infrastructure/Persistence/TcmdDbContext.cs` is the EF Core database context.
- `src/TCMD.Infrastructure/Persistence/Migrations/` contains the ordered EF Core migrations and the current model snapshot.
- `src/TCMD.Api/appsettings.json` contains safe shared settings only. ASP.NET Core loads environment variables and user-secrets over these settings.
- `tests/TCMD.IntegrationTests/TcmdApiFactory.cs` configures `WebApplicationFactory`, guards the dedicated test database name, and applies migrations before tests.
- `tests/TCMD.IntegrationTests/HealthEndpointTests.cs` proves the real API pipeline, SQL Server connection, OpenAPI document, and `ProblemDetails` response work together.
- `.config/dotnet-tools.json` pins the EF Core command-line tool version used by the repository.

## Foundation concepts

ASP.NET Core uses **dependency injection** to create application dependencies. `Program.cs` registers the EF Core context, health checks, OpenAPI, and `ProblemDetails`; the framework supplies them where needed.

The **middleware pipeline** processes each HTTP request in order. Exception and status-code middleware convert errors into the standard `application/problem+json` format. Endpoint mappings then expose `/health` and `/openapi/v1.json`.

EF Core's **`DbContext`** represents a database session and model. The student migration adds the `Students` table, a unique student-number index, and a SQL Server sequence used to generate concurrency-safe `STU-000001`-style numbers. Sequence values are unique but may contain gaps when a registration fails after reserving a number.

`WebApplicationFactory` boots the real application entry point inside the test process. The SQL-backed test classes share one collection fixture, so the guarded test database is migrated once while unrelated test collections remain eligible for parallel execution. This provides much more confidence than testing a method alone because configuration, dependency injection, middleware, routing, EF Core, and SQL Server participate together.

## Prerequisites

- .NET 10 SDK
- SQL Server access
- On Windows, SQL Server Express LocalDB is the simplest development option

The examples below use PowerShell from the repository root.

## Restore and build

```powershell
dotnet tool restore
dotnet restore TCMD.slnx
dotnet build TCMD.slnx --no-restore
```

## Configure the development database

No connection string or credential is committed. Store the development connection string with .NET user-secrets:

```powershell
dotnet user-secrets set --project src/TCMD.Api "ConnectionStrings:TCMD" "Server=(localdb)\MSSQLLocalDB;Database=TCMD.Development;Trusted_Connection=True;TrustServerCertificate=True"
```

For another SQL Server, replace that example locally. In automated or deployed environments, set the `ConnectionStrings__TCMD` environment variable instead. Environment variables use a double underscore where JSON configuration uses a colon.

## Apply database migrations

EF starts `TCMD.Api` for design-time commands, so migrations use the same configuration sources and precedence as the running application. The following command uses the `ConnectionStrings:TCMD` value configured above through API user-secrets or the `ConnectionStrings__TCMD` environment variable:

```powershell
dotnet ef database update --project src/TCMD.Infrastructure --startup-project src/TCMD.Api
```

## Initialize the first administrator

After applying migrations, set these values with user-secrets for one startup only. Do not commit them:

```powershell
dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:Enabled" "true"
dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:UserName" "admin"
dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:DisplayName" "Initial Administrator"
dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:Password" "choose-a-password-with-letters-and-digits"
```

Start the API once. TCMD creates the Administrator, Staff, and Instructor roles and exactly one active Administrator only when no staff accounts exist. Remove `BootstrapAdmin:Enabled` and `BootstrapAdmin:Password` immediately after a successful startup. In deployed environments, provide the same values through protected environment variables instead.

`POST /api/auth/login` accepts `userName` and `password` and creates an HTTP-only cookie session. `POST /api/auth/logout` ends the current authenticated session. Health, OpenAPI, and login are public; all other endpoints require sign-in.

Administrators manage staff access through `/api/staff-accounts`: `GET /` lists accounts, `GET /{id}` retrieves one, and `POST /` creates an active account with exactly one approved role. `PATCH /{id}/active` activates or deactivates an account, `PATCH /{id}/role` changes its single role, and `POST /{id}/password` replaces its password. Staff and Instructor accounts cannot use these endpoints. Deactivation, role changes, and password replacement invalidate the affected user's existing session. TCMD rejects self-deactivation and any operation that would leave no active Administrator.

To select a database explicitly for one non-secret local command, pass its connection string to EF:

```powershell
dotnet ef database update --project src/TCMD.Infrastructure --startup-project src/TCMD.Api --connection "Server=(localdb)\MSSQLLocalDB;Database=TCMD.Development;Trusted_Connection=True;TrustServerCertificate=True"
```

Do not put credentials on the command line because they may be recorded in shell history or process information. For a connection containing credentials, set `ConnectionStrings__TCMD` in the current environment, run the first command, and then remove the environment variable.

To create a future migration after an approved model change:

```powershell
dotnet ef migrations add MigrationName --project src/TCMD.Infrastructure --startup-project src/TCMD.Api --output-dir Persistence/Migrations
```

## Run the API

```powershell
dotnet run --project src/TCMD.Api
```

Use the listening URL printed by ASP.NET Core:

- `GET /health` reports overall API health and the named `database` connectivity check.
- `GET /openapi/v1.json` returns the generated OpenAPI document.
- `POST /api/students` registers a student from `fullName`, required `phoneNumber`, and optional `email`; it returns `201 Created` and a generated student number.
- `GET /api/students` lists all students and accepts optional `search` and `isActive=true|false` query parameters.
- `GET /api/students/{id}` returns the registered student or `404 Not Found`.
- `PUT /api/students/{id}` updates basic details using the current base64 `rowVersion` and returns `409 Conflict` for a stale version.
- `POST /api/students/{id}/deactivate` marks a student inactive using the current base64 `rowVersion`; repeating the operation is safe and does not create another update.

- `POST /api/instructors` creates an active instructor from required `fullName` and optional `phoneNumber` and `email`.
- `GET /api/instructors` lists all instructors and accepts optional `search` across name, phone, and email plus `isActive=true|false`.
- `GET /api/instructors/{id}` returns one instructor or `404 Not Found`.
- `PUT /api/instructors/{id}` updates contact details using the current base64 `rowVersion`; inactive instructor details may also be corrected.
- `POST /api/instructors/{id}/deactivate` marks an instructor inactive using `rowVersion`; repeating it does not write again.

Student and Instructor representations include a base64 `rowVersion`. Send that value unchanged when updating or initially deactivating a record. Administrator and Staff accounts can use these endpoints. Instructor accounts cannot use standalone Student or Instructor endpoints in V1.

- `POST /api/courses` creates an active course from required `code` and `name` plus optional `description`; codes are trimmed and stored in invariant uppercase.
- `GET /api/courses` lists all courses and accepts optional `search` across code, name, and description plus `isActive=true|false`.
- `GET /api/courses/{id}` returns one course or `404 Not Found`.
- `PUT /api/courses/{id}` updates course details using the current base64 `rowVersion`; duplicate codes and stale versions return `409 Conflict`.
- `POST /api/courses/{id}/deactivate` marks a course inactive using `rowVersion`; repeating it does not write again.

Course codes are unique across active and inactive courses. Administrator and Staff accounts can use standalone Course endpoints; Instructor accounts cannot.

- `POST /api/training-groups` creates a Planned group for an active course, optionally with one active primary instructor.
- `GET /api/training-groups` lists historical and current groups and accepts `search`, `status`, `courseId`, `primaryInstructorId`, and `hasPrimaryInstructor` filters.
- `GET /api/training-groups/{id}` returns one group or `404 Not Found`.
- `PUT /api/training-groups/{id}` updates the approved details and assignments using the current base64 `rowVersion`.
- `POST /api/training-groups/{id}/activate` activates a Planned group whose course and assigned instructor are active.
- `POST /api/training-groups/{id}/complete` completes an Active group.
- `POST /api/training-groups/{id}/cancel` cancels a Planned or Active group without deleting it.

Training Group statuses are Planned, Active, Completed, and Cancelled. Completed and Cancelled groups are terminal and preserved for historical views. Course and instructor group views use the Training Group list filters. Administrator and Staff accounts can use these endpoints; Instructor accounts cannot use this standalone management slice.

Unknown routes and unhandled API errors use `ProblemDetails` JSON and include a request `traceId` for troubleshooting.

## Run tests

```powershell
dotnet test TCMD.slnx --no-restore
```

The command runs focused domain tests and database-backed integration tests. The integration fixture resolves its SQL Server connection string in this order:

1. The `TCMD_TEST_CONNECTION_STRING` environment variable.
2. `ConnectionStrings:TCMD` in `tests/TCMD.IntegrationTests/appsettings.IntegrationTests.json`.
3. A non-secret Windows LocalDB fallback using database `TCMD.IntegrationTests`.

The committed test configuration contains an empty value and no credentials. For a temporary PowerShell session, configure another SQL Server without changing tracked files:

```powershell
$env:TCMD_TEST_CONNECTION_STRING = "Server=your-test-server;Database=TCMD.IntegrationTests;User ID=your-user;Password=your-password;TrustServerCertificate=True"
dotnet test TCMD.slnx --no-restore
Remove-Item Env:TCMD_TEST_CONNECTION_STRING
```

Do not paste real credentials into `appsettings.IntegrationTests.json`. If that file is used for a non-secret local configuration, its value has lower precedence than the environment variable.

Regardless of the source, the fixture parses the resolved connection string and refuses to run unless its database name is exactly `TCMD.IntegrationTests`. It never uses `TCMD.Development`. The test database may be created automatically and retained between runs; EF migrations keep its schema current.

If LocalDB is installed but not running, start it before testing:

```powershell
sqllocaldb start MSSQLLocalDB
```

For non-Windows CI, provide an available SQL Server through the protected `TCMD_TEST_CONNECTION_STRING` environment variable. Never point tests at a development, shared, staging, or production database.

## Product documentation

- [Product scope](docs/product-scope.md)
- [Users and roles](docs/users-and-roles.md)
- [Core workflows](docs/core-workflows.md)
- [Domain model](docs/domain-model.md)
- [V1 roadmap](docs/v1-roadmap.md)
- [Repository instructions](AGENTS.md)
