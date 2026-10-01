# Repository Instructions for Codex

These instructions apply to the entire TCMD repository.

## Product and scope

- TCMD V1 is an implemented internal web application for one training center.
- Treat the approved documents in `docs/` as the product definition.
- Do not add functionality listed as outside V1 in `docs/product-scope.md` unless the user explicitly changes the scope.
- Do not invent business rules. Document unresolved requirements and obtain confirmation before implementing behavior that depends on them.
- Prefer the simplest maintainable design that satisfies the confirmed requirement.

## Architecture

- Preserve the four-project architecture: `TCMD.Api`, `TCMD.Application`, `TCMD.Domain`, and `TCMD.Infrastructure`.
- `TCMD.Domain` must remain independent of Infrastructure, HTTP, and persistence concerns.
- `TCMD.Application` owns use cases and abstractions and must remain independent of HTTP and EF Core.
- `TCMD.Infrastructure` implements Application abstractions and owns EF Core, SQL Server, Identity storage, and migrations.
- `TCMD.Api` is the composition and delivery layer for endpoints, middleware, configuration, authentication, and the browser client.
- Do not introduce speculative infrastructure or abstraction layers without a current need.

## Security and data integrity

- Preserve server-side authentication, authorization, Instructor assignment checks, antiforgery validation, and safe `ProblemDetails` responses.
- Preserve SQL Server `rowversion` optimistic concurrency and explicit conflict handling.
- Enforce important validation and lifecycle rules on the server and with database constraints where practical.
- Preserve history required by enrollments and attendance; do not replace deactivation with physical deletion without an approved retention rule.
- Preserve exact database-name safety guards for demo seeding and database-backed tests.
- Never commit real passwords, tokens, connection strings, personal data, or production credentials.

## Implementation conventions

- Target .NET 10 with nullable reference types enabled and follow existing ASP.NET Core and Entity Framework Core conventions.
- Prefer clear, focused code, asynchronous I/O, useful cancellation tokens, semantic accessible HTML, and the existing native JavaScript module structure.
- Use Entity Framework Core migrations for schema changes.
- Keep changes focused and preserve unrelated user work.
- Update relevant documentation when a confirmed rule or workflow changes.

## Verification and repository care

- Add or update automated tests for behavioral changes, including allowed and forbidden cases for role-sensitive workflows.
- Run the relevant Domain, Integration, or Browser tests after changes and report exactly what was verified.
- Do not claim unverified behavior works; state environmental limitations clearly.
- Do not weaken database guards or authorization to simplify testing.
- Do not perform destructive Git or filesystem actions without explicit authorization.
- Do not commit or push unless the user explicitly requests it.
