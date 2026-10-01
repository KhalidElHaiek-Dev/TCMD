# TCMD V1 — Implementation Status

## V1 objective

TCMD V1 is a completed internal web application for one training center. It gives authorized staff a single place to manage students, instructors, courses, training groups, enrollments, sessions, attendance, and staff access while preserving history and enforcing role-sensitive rules on the server.

## Architecture

The solution uses four projects with a clear dependency direction:

```text
TCMD.Api ──> TCMD.Application ──> TCMD.Domain
     └────> TCMD.Infrastructure ──> TCMD.Application + TCMD.Domain
```

- `TCMD.Api` is the executable host and owns HTTP endpoints, authentication, authorization, middleware, configuration, and the same-origin browser client.
- `TCMD.Application` owns use cases, DTOs, validation orchestration, and persistence abstractions. It remains independent of HTTP and EF Core.
- `TCMD.Domain` owns entities, invariants, lifecycle rules, and status transitions. It has no project dependencies.
- `TCMD.Infrastructure` owns EF Core, SQL Server persistence, ASP.NET Core Identity storage, migrations, and implementations of Application abstractions.

## Completed capabilities

### Identity and access

- ASP.NET Core Identity cookie authentication with sign-in, sign-out, lockout, and account deactivation.
- Administrator, Staff, and Instructor roles with server-enforced authorization.
- Administrator-only staff-account, role, password, active-status, and Instructor-link management.
- Optional one-to-one links between Instructor accounts and Instructor records.
- Instructor access restricted to groups currently assigned to the linked Instructor and the related sessions, compact rosters, and attendance.
- Antiforgery validation for state-changing API requests and safe `ProblemDetails` error responses.

### Core records

- Student, Instructor, and Course creation, search, retrieval, update, and history-preserving deactivation.
- Generated unique student numbers and canonical unique course codes.
- Database constraints and server-side validation for important uniqueness and relationship rules.

### Training operations

- Training Group creation, assignment, updates, and lifecycle transitions.
- Enrollment creation, completion, withdrawal, and eligible reactivation without losing history.
- Session scheduling, updates, completion, cancellation, and group-date validation.

### Attendance

- Attendance recording and rowversion-protected correction for eligible enrollments and sessions.
- Present, Absent, Late, and Excused statuses, with optional correction notes and staff attribution.
- Attendance views by session, student, and Training Group.

### Reliability and concurrency

- SQL Server persistence through Entity Framework Core migrations.
- SQL Server `rowversion` optimistic concurrency with explicit conflict responses and client recovery behavior.
- Consistent validation, authorization, lifecycle checks, and safe API errors.
- Dedicated database-name guards for demo seeding and database-backed tests.

### Browser interface

- Same-origin responsive interface built with semantic HTML, project-owned CSS, native JavaScript modules, and the Fetch API.
- Role-aware navigation with authorization still enforced by the server.
- Loading, empty, validation, error, authentication-expiry, and concurrency-conflict states.
- Keyboard, focus, responsive-layout, and accessibility improvements covered by browser tests.

### Testing and development tooling

- Domain tests for entity invariants and lifecycle transitions.
- SQL Server-backed integration tests using `WebApplicationFactory` for HTTP, authorization, antiforgery, persistence, and concurrency behavior.
- Playwright browser tests for complete workflows, role-sensitive behavior, accessibility, responsive presentation, state preservation, and concurrency feedback.
- Explicit development-only demo-data seeding restricted to the `TCMD.Demo` database and an empty data set.

## V1 boundaries

TCMD V1 is intentionally limited to one internal training center. The following remain outside its scope:

- student accounts or a student portal;
- self-service password recovery and outbound recovery email;
- payments, invoices, fees, subscriptions, and other financial management;
- learning-management content, course videos, certificates, chat, and notifications;
- multiple training centers, SaaS or marketplace features, and mobile applications;
- multiple instructors per group, structured room management, recurring schedules, and schedule-conflict detection;
- advanced analytics, custom report building, and file exports unless separately approved;
- full attendance correction-history auditing; and
- microservices, CQRS, message brokers, Redis, Docker, and AI features inside TCMD.

Hosting, production backup ownership, and release approval depend on the environment in which the application is deployed and are not defined by this reference implementation.

## Current status

V1 is implemented. The repository is maintained as a portfolio and reference implementation of a focused ASP.NET Core application, with the approved V1 workflows represented across the API, browser interface, persistence layer, and automated test suites.
