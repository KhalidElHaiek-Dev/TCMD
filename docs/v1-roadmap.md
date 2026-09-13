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

## Milestone 2: Student registration and retrieval

Goal: deliver the first operational vertical slice on the API foundation.

Delivered behavior:

- student registration with required basic details;
- unique, generated student numbers;
- student retrieval by identifier; and
- domain and SQL-backed integration tests.

## Milestone 3A: Authentication and authorization foundation

Goal: protect the application before expanding operational features.

Delivered behavior:

- secure credential storage;
- sign-in and sign-out behavior;
- Administrator, Staff, and Instructor authorization rules;
- account deactivation; and
- integration tests for authentication and forbidden actions.

## Milestone 3B: Staff-account administration

Goal: allow administrators to manage internal access safely.

Delivered behavior:

- Administrator-only staff-account creation and retrieval;
- role, active-status, and password administration;
- protection for the last active Administrator; and
- session invalidation after security-sensitive account changes.

Optional links between instructor records and staff accounts remain future work.

## Milestone 4: Standalone Student management

Goal: complete the standalone student workflows needed by later enrollment work.

Delivered behavior:

- student list and search;
- active/inactive filtering;
- basic-detail updates;
- deactivation without deleting history;
- optimistic-concurrency protection; and
- validation, authorization, domain, and integration tests.

## Milestone 5: Standalone Instructor management

Goal: manage instructor records needed by later group assignment.

Delivered behavior:

- instructor creation, retrieval, list, and search;
- active/inactive filtering;
- basic-detail updates;
- deactivation without deleting history;
- optimistic-concurrency protection; and
- validation, authorization, domain, and integration tests.

## Milestone 6: Standalone Course management

Goal: manage reusable course records needed by later training groups.

Delivered behavior:

- course creation, retrieval, list, and search;
- active/inactive filtering;
- course-detail updates;
- canonical, unique course codes across active and inactive records;
- deactivation without deleting history;
- optimistic-concurrency protection; and
- validation, authorization, domain, and integration tests.

## Milestone 7: Standalone Training Group management

Goal: organize planned deliveries of courses before adding enrollment workflows.

Delivered behavior:

- training-group creation, retrieval, list, and update;
- active course and optional primary-instructor assignment while Planned;
- group status and planned-date rules;
- course and instructor group views; and
- validation, authorization, concurrency, and integration tests.

Completion criteria:

- staff can create and maintain a valid group for an active course;
- group dates, status, and instructor requirements follow the approved rules; and
- inactive reference records cannot be selected for new assignments.

## Milestone 8: Enrollment management

Goal: enroll eligible students into training groups while preserving membership history.

Delivered behavior:

- enrollment creation and status management;
- student and group membership views;
- withdrawn-enrollment reactivation; and
- integration tests for inactive records, status rules, and duplicate enrollment.

Completion criteria:

- staff can enroll eligible students;
- duplicate enrollment is impossible; and
- enrollment history survives status changes.

## Milestone 9: Training sessions

Goal: schedule and maintain the meetings delivered by each training group.

Delivered behavior:

- session scheduling, retrieval, update, explicit completion, and cancellation;
- session lists by group;
- group-date validation and protection against excluding non-cancelled sessions;
- optimistic concurrency, authorization, domain, and SQL-backed integration tests; and
- Operational Staff-only access until account-to-Instructor linkage supports safe assigned-session views.

Assigned-Instructor session views remain deferred. Milestone 9 does not add attendance, automatic completion, conflict detection, recurring schedules, or browser UI.

## Milestone 10: Attendance

Goal: record and correct attendance for eligible students in delivered sessions.

Delivered behavior:

- attendance entry and rowversion-protected correction with staff-actor metadata;
- roster attendance by session and saved-attendance views by student and group;
- Active-enrollment, same-group, session-state, and Africa/Casablanca timing validation; and
- domain and SQL-backed integration tests for relationships, history, authorization, and uniqueness.

Completion criteria:

- authorized users can record and correct attendance;
- invalid or duplicate attendance is rejected; and
- cancelled sessions and historical records follow the approved rules.

Missing attendance means Not Recorded rather than Absent. New entry requires a currently Active enrollment whose
enrollment date is no later than the session date. Existing attendance remains visible and correctable after later
enrollment, student, group, or session state changes. Instructor access remains deferred until account-to-Instructor
linkage can enforce assigned-group access.

## Milestone 11: Instructor account linking and assigned access

Goal: connect Instructor-role sign-in accounts to Instructor records and safely expose assigned work.

Delivered behavior:

- Administrator-only creation, replacement, and removal of optional one-to-one account links;
- session invalidation for link changes, link-clearing role changes, and linked Instructor deactivation;
- SQL-scoped Instructor views of assigned groups, sessions, enrollment rosters, and attendance;
- assigned-session Attendance entry and correction using the StaffUser as the audit actor; and
- current-primary-Instructor authorization, historical access transfer, concealment, concurrency, and integration tests.

Instructor accounts may sign in while unlinked or linked to an inactive Instructor, but assigned-resource endpoints
deny access. Instructor access follows the Training Group's current `PrimaryInstructorId`; V1 does not retain
per-session Instructor ownership.

## Milestone 12: Browser interface integration

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

## Milestone 13: V1 hardening and release preparation

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
