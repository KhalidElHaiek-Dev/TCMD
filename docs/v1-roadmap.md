# Version 1 Roadmap

This roadmap defines a safe implementation order. It does not promise dates or effort estimates. A phase is complete only when its agreed behavior is implemented, tested, and documented.

## Product-definition status

The blocking V1 product decisions are confirmed. The approved rules are recorded in the product scope, roles, workflows, and domain model documents.

The implementation sequence begins with Milestone 1 below. Later milestones remain ordered by business dependency rather than promised dates.

## Milestone 1: Executable and testable API foundation

Goal: create the smallest dependable engineering base before implementing a business module.

Deliverables:

- a .NET 10 solution using the proposed three-project structure;
- an ASP.NET Core Web API project;
- a small domain project;
- an integration-test project;
- nullable reference types enabled;
- environment-based configuration with no committed secrets;
- Entity Framework Core 10 configured for SQL Server;
- an empty initial database migration;
- consistent `ProblemDetails` error responses;
- a health endpoint that checks the API and database;
- generated OpenAPI documentation;
- an integration-test host using `WebApplicationFactory`;
- a dedicated integration-test database configuration;
- one integration test proving API and SQL Server connectivity; and
- documented restore, migration, run, and test commands.

Milestone 1 intentionally contains no student, instructor, course, group, enrollment, session, attendance, or authentication feature endpoints.

Acceptance criteria:

1. A new developer can follow the README and start the API.
2. The solution builds with zero errors.
3. All projects target .NET 10 and enable nullable reference types.
4. Configuration contains no committed credentials or connection-string secrets.
5. EF Core migrations can create or update the dedicated development database.
6. `GET /health` reports API health and SQL Server connectivity.
7. API errors use a consistent `ProblemDetails` representation.
8. OpenAPI document generation succeeds.
9. Integration tests start the real API pipeline through `WebApplicationFactory`.
10. At least one integration test verifies API and SQL Server connectivity.
11. Tests use a dedicated test database and never modify the development database.
12. A clean checkout has documented restore, migration, run, and test commands.
13. No business or postponed feature is implemented prematurely.

Approved technical baseline:

- .NET 10 LTS, ASP.NET Core Web API, and C#;
- Entity Framework Core 10 with the SQL Server provider;
- ASP.NET Core Identity with secure HTTP-only cookie authentication for later authentication work;
- xUnit and `WebApplicationFactory` for integration tests; and
- HTML, CSS, JavaScript, and Fetch served from the API for the later browser interface.

## Milestone 2: Authentication and authorization

Goal: protect the application before adding operational features.

Planned deliverables:

- staff accounts;
- secure credential storage;
- sign-in and sign-out behavior;
- Administrator, Staff, and Instructor authorization rules;
- account deactivation; and
- integration tests for authentication and forbidden actions.

Completion criteria:

- inactive and invalid accounts cannot sign in;
- protected operations require authentication; and
- each role is restricted according to the approved permissions.

## Milestone 3: Core reference records

Goal: manage the people and course information needed by later workflows.

Planned deliverables:

- student management;
- instructor management;
- optional links between instructor records and staff accounts;
- course management;
- search and active/inactive behavior; and
- validation and integration tests.

Completion criteria:

- authorized users can create, view, update, search, and deactivate records;
- unique and required values are enforced; and
- unauthorized changes are rejected.

## Milestone 4: Groups and enrollments

Goal: organize students into deliveries of courses.

Planned deliverables:

- training-group management;
- course and primary-instructor assignment;
- group status and planned dates;
- enrollment management;
- student and group membership views; and
- integration tests for dates, inactive records, and duplicate enrollment.

Completion criteria:

- staff can create a valid group and enroll eligible students;
- duplicate enrollment is impossible; and
- enrollment history survives status changes.

## Milestone 5: Sessions and attendance

Goal: support day-to-day delivery and attendance tracking.

Planned deliverables:

- session scheduling and cancellation;
- session lists by group;
- attendance entry and correction;
- attendance views by session, student, and group;
- instructor restrictions to assigned work; and
- integration tests for attendance relationships and uniqueness.

Completion criteria:

- authorized users can schedule a session and record attendance;
- invalid or duplicate attendance is rejected; and
- cancelled sessions and historical records follow the approved rules.

## Milestone 6: Browser interface integration

Goal: provide a clear internal interface for the approved workflows.

Planned deliverables:

- accessible HTML forms and operational views;
- CSS for usable desktop and responsive layouts;
- JavaScript modules that call the API through Fetch;
- useful loading, validation, empty, and error states; and
- authorization-aware navigation without treating hidden controls as security.

Completion criteria:

- staff can complete every approved workflow through the browser;
- server errors are presented safely and clearly; and
- essential keyboard and accessibility checks pass.

## Milestone 7: V1 hardening and release preparation

Goal: make the completed V1 dependable and supportable.

Planned deliverables:

- full integration-test review of core workflows;
- security and authorization review;
- data-integrity and migration review;
- logging and operational error handling;
- setup, deployment, backup, and recovery documentation; and
- final scope and acceptance review.

Completion criteria:

- all V1 acceptance criteria pass;
- no known critical data-integrity or authorization defects remain;
- deployment and recovery steps are documented and tested in the approved environment; and
- postponed features have not entered the release accidentally.

Hosting, production backup ownership, and the release approval process are deferred until this milestone. They do not block Milestone 1 or domain implementation.

## After V1

Postponed features must be evaluated as separate product work. They should not be added merely because a later architecture could support them. Scope, user value, business rules, security effects, and maintenance cost must be agreed first.
