# Repository Instructions for Codex

These instructions apply to the entire TCMD repository.

## Product boundary

- TCMD V1 is an internal web application for one training center.
- Treat the approved files in `docs/` as the product definition.
- Do not add features listed as postponed in `docs/product-scope.md` unless the user explicitly changes the scope.
- Do not invent business rules. Clearly document unresolved requirements and ask for confirmation before implementing behavior that depends on them.
- Keep language and explanations clear enough for a junior .NET developer.

## Before implementation

- Do not create application code, a .NET solution, database files, or UI files until the user explicitly requests the technical implementation phase.
- Before major technical work, confirm any unresolved decision that materially affects data, security, permissions, or architecture.
- Prefer an incremental plan in which each phase produces working, testable behavior.

## Architecture and scope rules

- Use the simplest maintainable design that satisfies confirmed V1 requirements.
- The planned system is one ASP.NET Core Web API with a browser client and SQL Server persistence.
- Do not introduce microservices, CQRS, message brokers, Redis, Docker, or speculative infrastructure in V1.
- Do not create abstraction layers without a current use.
- Keep domain rules, authorization, and validation enforceable on the server.
- Do not depend on browser validation or hidden UI controls for security or data integrity.

## C# and .NET rules for future work

- Target .NET 10 LTS and follow supported ASP.NET Core conventions.
- Enable nullable reference types.
- Prefer clear names and small, focused types and methods.
- Use asynchronous APIs for I/O operations and pass cancellation tokens where useful.
- Use dependency injection for real application dependencies, not as a reason to create unnecessary interfaces.
- Return consistent, safe API errors. Do not expose stack traces or sensitive implementation details to clients.
- Validate external input and enforce important rules again with database constraints when practical.

## Data rules for future work

- Use Entity Framework Core migrations for schema changes.
- Do not edit a shared or deployed database schema manually.
- Do not physically delete records that are required for enrollment or attendance history unless a confirmed retention rule permits it.
- Protect important uniqueness and relationship rules with application behavior and database constraints where appropriate.
- Never store passwords, tokens, connection strings, or other secrets in source control.
- Use test data only in automated tests or explicit development seeding.

## Authentication and authorization

- Deny protected operations unless the user is authenticated and authorized.
- Enforce permissions at API boundaries and, where needed, inside business operations.
- Test both allowed and forbidden cases for every role-sensitive workflow.
- Do not log passwords, authentication tokens, or other sensitive credentials.
- Do not weaken authorization to simplify UI development or testing.

## Testing and verification

- Add automated integration tests with each important API workflow.
- At minimum, test successful behavior, validation failures, missing records, duplicate data, and unauthorized access where applicable.
- Include regression tests with bug fixes when practical.
- Run relevant tests after changes and report the exact verification performed.
- Do not claim that a change works when it has not been verified. State any verification limitation clearly.

## Frontend rules for future work

- Use semantic, accessible HTML and clear labels.
- Keep CSS and JavaScript maintainable and free of unnecessary frameworks unless the user approves a change in direction.
- Use the Fetch API for communication with the backend.
- Treat all displayed user-controlled data safely; do not inject it into HTML as executable markup.
- Show useful loading, empty, validation, and error states.

## Documentation and repository care

- Update relevant documentation when a confirmed product rule or workflow changes.
- Do not silently convert an unresolved product decision into an implemented assumption.
- Preserve existing user changes and avoid unrelated rewrites.
- Inspect the current repository state before editing.
- Keep commits and changes focused on the requested task.
- Do not add dependencies, tools, generated artifacts, or configuration without a clear present need.
- Do not perform destructive Git or filesystem actions without explicit authorization.
