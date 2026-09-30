# TCMD

**Training Center Management Dashboard**

TCMD is a full-stack internal training-center management system built with ASP.NET Core, SQL Server, Entity Framework Core, and a same-origin native JavaScript frontend. It gives staff one place to manage Students, Instructors, Courses, Training Groups, Enrollments, Sessions, Attendance, and staff access throughout the training lifecycle.

## Project highlights

- Role-based workflows for Administrator, Staff, and Instructor accounts.
- End-to-end training lifecycle from Student enrollment through Session scheduling and Attendance.
- Instructor accounts restricted to their assigned Groups, Sessions, rosters, and Attendance.
- ASP.NET Core Identity cookie authentication with server-enforced authorization and antiforgery protection.
- SQL Server `rowversion` optimistic concurrency with explicit conflict handling instead of automatic stale-write overwrites.
- Responsive, accessible browser interface built with semantic HTML, project-owned CSS, and native JavaScript modules.
- SQL Server-backed integration tests through `WebApplicationFactory` and end-to-end browser tests with Playwright.

## Screenshots

> Portfolio screenshots will be added in the next refinement pass.

## Core workflows

- **Students and Instructors:** create, search, update, and deactivate records while retaining their history.
- **Courses:** maintain a unique course catalog and deactivate courses without deleting historical relationships.
- **Training Groups:** plan groups, assign Courses and primary Instructors, then activate, complete, or cancel them.
- **Enrollments:** enroll Students, track active/completed/withdrawn membership, and reactivate eligible withdrawals.
- **Sessions:** schedule and update group Sessions, including completion and cancellation.
- **Attendance:** record and correct roster Attendance with staff attribution and eligibility checks.
- **Staff access:** create accounts, assign one approved role, activate or deactivate access, and optionally link Instructor accounts to Instructor records.

## Technology stack

| Area | Technologies |
| --- | --- |
| Backend | .NET 10, ASP.NET Core, ASP.NET Core Identity, Entity Framework Core, SQL Server |
| Frontend | Semantic HTML, CSS, native JavaScript modules, Fetch API; no Node frontend build step |
| Testing | xUnit, `WebApplicationFactory` integration testing, Playwright browser testing |

## Architecture

TCMD separates HTTP and browser delivery, application use cases, domain rules, and infrastructure across four projects:

```text
Browser
   |
TCMD.Api
   |-- TCMD.Application
   `-- TCMD.Infrastructure
          |-- TCMD.Application
          `-- TCMD.Domain

TCMD.Application --> TCMD.Domain
```

- `TCMD.Api` is the executable host and owns endpoints, authentication, middleware, configuration, and the same-origin browser client.
- `TCMD.Application` owns use cases, DTOs, validation orchestration, and persistence abstractions.
- `TCMD.Domain` owns entities, lifecycle rules, and status transitions, with no project dependencies.
- `TCMD.Infrastructure` owns EF Core, SQL Server persistence, Identity storage, migrations, and implementations of abstractions defined by Application.

This dependency direction keeps business rules and use cases independent of HTTP and the concrete database implementation.

## Reliability, security, and concurrency

TCMD uses HTTP-only ASP.NET Core Identity cookies and enforces role and assignment policies at API boundaries. State-changing requests require antiforgery validation, while API errors use safe `ProblemDetails` responses. Business records use SQL Server `rowversion`; stale mutations return a conflict and require the client to reload instead of silently overwriting newer data.

Connection strings and bootstrap credentials are supplied through environment variables or .NET user-secrets rather than source control. Integration tests, browser tests, and demo seeding validate exact, dedicated database names before performing guarded setup operations.

## Testing

The test suite is split by responsibility:

- **Domain Tests** exercise entity invariants and lifecycle transitions without web or database infrastructure.
- **Integration Tests** start the real ASP.NET Core pipeline with `WebApplicationFactory` and verify HTTP behavior, authorization, antiforgery, EF Core, and SQL Server persistence.
- **Browser Tests** start the application on loopback HTTPS and use Playwright to verify complete role-sensitive workflows, accessibility behavior, responsive presentation, and concurrency feedback.

Database-backed suites use dedicated databases named exactly `TCMD.IntegrationTests` and `TCMD.BrowserTests`; their fixtures reject other database names. The browser project is intentionally separate from `TCMD.slnx` so browser tooling is an explicit test dependency.

Run the normal solution tests:

```powershell
dotnet test TCMD.slnx
```

Run browser tests after installing Chromium once:

```powershell
dotnet restore tests/TCMD.BrowserTests/TCMD.BrowserTests.csproj
dotnet build tests/TCMD.BrowserTests/TCMD.BrowserTests.csproj --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/TCMD.BrowserTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/TCMD.BrowserTests/TCMD.BrowserTests.csproj --no-build --no-restore
```

## Run locally

Prerequisites: the .NET 10 SDK and access to SQL Server. On Windows, SQL Server Express LocalDB is the simplest development option. Run these PowerShell commands from the repository root.

1. Restore tools and build the solution:

   ```powershell
   dotnet tool restore
   dotnet restore TCMD.slnx
   dotnet build TCMD.slnx --no-restore
   ```

2. Store a development connection string outside source control:

   ```powershell
   dotnet user-secrets set --project src/TCMD.Api "ConnectionStrings:TCMD" "Server=(localdb)\MSSQLLocalDB;Database=TCMD.Development;Trusted_Connection=True;TrustServerCertificate=True"
   ```

3. Apply the EF Core migrations:

   ```powershell
   dotnet ef database update --project src/TCMD.Infrastructure --startup-project src/TCMD.Api
   ```

4. Configure the first Administrator for one startup only:

   ```powershell
   dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:Enabled" "true"
   dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:UserName" "admin"
   dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:DisplayName" "Initial Administrator"
   dotnet user-secrets set --project src/TCMD.Api "BootstrapAdmin:Password" "choose-a-password-with-letters-and-digits"
   ```

5. Start the application and open the HTTPS URL printed by ASP.NET Core:

   ```powershell
   dotnet run --project src/TCMD.Api
   ```

After the Administrator is created, remove `BootstrapAdmin:Enabled` and `BootstrapAdmin:Password` from user-secrets.

## Demo data

TCMD includes an explicit one-shot seeder for local demonstrations and screenshots. It runs only in `Development`, requires a database named exactly `TCMD.Demo`, refuses a non-empty database, and never runs during normal application startup.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__TCMD = 'Server=(localdb)\MSSQLLocalDB;Database=TCMD.Demo;Trusted_Connection=True;TrustServerCertificate=True'
dotnet run --project src/TCMD.Api -- --seed-demo
```

**Development-only demo credentials**

| Username | Role | Password |
| --- | --- | --- |
| `admin.demo` | Administrator | `TcmdDemo2026` |
| `staff.demo` | Staff | `TcmdDemo2026` |
| `instructor.demo` | Instructor | `TcmdDemo2026` |

These credentials exist only in the explicitly seeded demo database and must not be reused elsewhere. See [Development demo data](docs/development-demo-data.md) for the dataset, safety boundaries, and screenshot candidates.

## Repository structure

```text
src/
  TCMD.Api
  TCMD.Application
  TCMD.Domain
  TCMD.Infrastructure
tests/
  TCMD.DomainTests
  TCMD.IntegrationTests
  TCMD.BrowserTests
docs/
```

## Documentation

- [Product scope](docs/product-scope.md)
- [Users and roles](docs/users-and-roles.md)
- [Core workflows](docs/core-workflows.md)
- [Domain model](docs/domain-model.md)
- [V1 roadmap](docs/v1-roadmap.md)
- [Development demo data](docs/development-demo-data.md)
