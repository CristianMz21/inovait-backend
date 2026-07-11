# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Inovait Backend is the API for a technical evaluation covering school enrollment management and teacher contracts (school registrations, grade/school/year lookups, multi-school teacher contracts). It is an ASP.NET Core Minimal API monolith (`net10.0`) backed by SQL Server via EF Core, returning `camelCase` JSON and RFC7807 `ProblemDetails` for errors. There is no authentication/authorization, no CQRS/event sourcing, no generic repository, and no MediatR — see `docs/architecture.md` and `specs/001-school-enrollment-management/plan.md`.

The only committed MVP is **P0**: atomic Student/Enrollment creation, enrollment lookup by School/Grade/AcademicYear, teacher-contract creation/lookup across schools, and supporting catalogs. P1 (municipal reports) is planned but conditional on P0 evidence — do not build P1 endpoints unless explicitly asked.

## Architecture & layer rules

Three production projects with a strict, one-directional dependency chain (enforced by `ProjectReference`s in the `.csproj` files, not just convention):

```
Inovait.Api ──► Inovait.Core ◄── Inovait.Infrastructure ──► SQL Server
```

- **`Inovait.Core`** (`src/Inovait.Core/Domain/`, `src/Inovait.Core/Features/`) — entities, domain invariants, use-case handlers and the ports (interfaces) they depend on. **Must NOT reference ASP.NET Core, EF Core, or `Inovait.Infrastructure`/`Inovait.Api`.**
- **`Inovait.Infrastructure`** (`src/Inovait.Infrastructure/Persistence/`, `src/Inovait.Infrastructure/Features/`) — `InovaitDbContext`, Fluent API configurations, interceptors, migrations, seeding, and the EF implementations of Core's ports. **Must NOT reference `Inovait.Api`.**
- **`Inovait.Api`** — Minimal API endpoint mappings, request/response DTOs, ProblemDetails mapping, and *read* services that query `InovaitDbContext` directly for projections.

**Vertical-slice / CQRS-ish pattern per feature** (not textbook CQRS — no bus, no MediatR):
1. A feature lives in `Inovait.Core/Features/<Feature>/` as one file with the command record, an error enum, a result record, port interfaces (`I<Feature>Repository`, `I<Feature>Transaction`), and a `Handler` class. Example: `src/Inovait.Core/Features/Enrollments/CreateEnrollment.cs`.
2. `Inovait.Infrastructure/Features/<Feature>/Ef<Feature>Workflow.cs` implements those ports against `InovaitDbContext`, including the transactional retry loop for serialization conflicts (e.g. `src/Inovait.Infrastructure/Features/Enrollments/EfEnrollmentWorkflow.cs:72` — `Serializable` isolation, 3 attempts, SQL error 1205/2601/2627 treated as a race).
3. `Inovait.Api/Endpoints/<Feature>Endpoints.cs` maps HTTP routes, validates request shape via `RequestValidator`/`RequestValidation`, calls the Core handler, and on success calls a `Inovait.Api/Reads/<Feature>ReadService.cs` for the response DTO (writes and reads are separate paths — see `src/Inovait.Api/Endpoints/EnrollmentEndpoints.cs` and `src/Inovait.Api/Reads/EnrollmentReadService.cs`).
4. Errors map through `Inovait.Api/Errors/<Feature>Problems.cs` → `ProblemFactory.Create(...)` (`src/Inovait.Api/Errors/ProblemFactory.cs`), which produces `ProblemDetails` with a `code` extension. Status-code-to-`code` mapping for the generic ASP.NET Core ProblemDetails path is centralized in `src/Inovait.Api/Program.cs:27`.
5. DI wiring for a feature's ports/handlers lives in `src/Inovait.Infrastructure/DependencyInjection.cs` (`AddInovaitInfrastructure`).

Registration and route mapping are conditional: if `ConnectionStrings:InovaitDatabase` is not configured, `Program.cs` skips `AddInovaitInfrastructure` and skips mapping catalog/enrollment/teacher-contract endpoints entirely — only `/health` and `/` are available (`src/Inovait.Api/Program.cs:17-25,76-81`).

## Key commands

No CI workflow or `Directory.Build.props` exists in the repo; the canonical command sequence is documented in `docs/evaluator-execution.md` and mirrored in `README.md`:

```bash
dotnet restore
dotnet build --no-restore --configuration Debug      # or Release
dotnet test --no-build --no-restore --configuration Debug   # or Release
dotnet format --verify-no-changes --no-restore
dotnet list package --vulnerable --include-transitive
```

Run against the solution file `Inovait.slnx` (all `dotnet` commands above operate on it implicitly from the repo root).

- **Unit tests only**: `dotnet test tests/Inovait.UnitTests` — pure logic, no DB.
- **Integration tests only**: `dotnet test tests/Inovait.IntegrationTests` — needs Docker (Testcontainers spins up `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`), unless the env var `ConnectionStrings__InovaitTest` points at an external SQL Server (`tests/Inovait.IntegrationTests/Infrastructure/SqlServerFixture.cs`).
- **Filter by evidence trait**: tests carry `[Trait("Priority", "P0")]` and `[Trait("Evidence", "<ID>")]` (IDs cataloged in `docs/testing-strategy.md`), e.g. `dotnet test --filter "Priority=P0"` or `--filter "Priority=P0&Evidence=IT-ENR-ATOMIC"`.
- **Single test by name**: standard xUnit filter, e.g. `dotnet test --filter "FullyQualifiedName~CreateEnrollmentHandlerTests"`.
- **Human-diff gate**: `scripts/check-human-lines.py` reads `git diff --numstat` from stdin and fails (`exit 1`) if additions+deletions of human-authored changes exceed 400 lines — part of this repo's PR-sizing discipline, not a build step.
- **Format before committing** (no per-edit hook — `dotnet format` loads the full MSBuild workspace, ~15s per pass, too slow to run on every edit): after finishing a set of `.cs` changes and before creating a commit, format only the changed files, e.g. `dotnet format Inovait.slnx --include $(git diff --name-only --diff-filter=ACM '*.cs' | tr '\n' ' ')`. The `--verify-no-changes` form above is the read-only check used in the evaluator/CI sequence; this fixing form is the commit-time step.

## Tech stack

- SDK `10.0.109` (`global.json`, `rollForward: latestPatch`), C# `14`, `net10.0`.
- ASP.NET Core Minimal APIs + EF Core SQL Server `10.0.9` (`Microsoft.EntityFrameworkCore.SqlServer`/`.Relational`/`.Design` `10.0.9`).
- SQL Server 2022 (collation `Latin1_General_100_CI_AS`).
- xUnit v3 `3.2.2`, `Microsoft.NET.Test.Sdk` `18.0.1`, `xunit.runner.visualstudio` `3.1.5`.
- `Microsoft.AspNetCore.Mvc.Testing` `10.0.9` + `Testcontainers.MsSql` `4.13.0` (integration tests only).
- `coverlet.collector` `6.0.4` in both test projects.
- `TreatWarningsAsErrors` is `true` in every `.csproj` — a build with warnings fails.
- No FluentValidation/FluentAssertions by design (`README.md`) — validation is built-in ASP.NET Core binding + explicit domain checks (`RequestValidator`, Core handlers).

## Testing conventions

- `tests/Inovait.UnitTests` references `Inovait.Core` + `Inovait.Infrastructure` (with `Microsoft.EntityFrameworkCore.InMemory` available, though relational behavior is never asserted there) — pure rules: text normalization, age, identity resolution, contract overlap/cancellation/status.
- `tests/Inovait.IntegrationTests` references `Inovait.Infrastructure` + `Inovait.Api`, uses `WebApplicationFactory` for HTTP-level tests (`tests/Inovait.IntegrationTests/Api/`) and `SqlServerFixture` (real SQL Server via Testcontainers) for physical-model tests (`tests/Inovait.IntegrationTests/Persistence/`) — collation, `rowversion`, checks, triggers, permissions, and index shape are only trusted when verified against real SQL Server (`docs/testing-strategy.md`: "EF InMemory y SQLite no constituyen evidencia relacional").
- Coverage: `coverlet.collector` is wired per test project; run with `dotnet test --collect:"XPlat Code Coverage"` if coverage output is needed (no repo script wraps this).

## Database & migrations

- `InovaitDbContext` lives at `src/Inovait.Infrastructure/Persistence/InovaitDbContext.cs`; entity configs are one file per entity in `src/Inovait.Infrastructure/Persistence/Configurations/`, applied via `ApplyConfigurationsFromAssembly`.
- Migrations live in `src/Inovait.Infrastructure/Persistence/Migrations/`. Current chain: `20260711161500_InitialP0ProductionModel` (EF-scaffolded 11-table P0 model) followed by `20260711161518_AddP0DatabaseProtections` (hand-written — installs triggers and a locked-down `inovait_runtime` SQL role, see `src/Inovait.Infrastructure/Persistence/CatalogDatabaseProtections.cs`).
- Two interceptors run on every `SaveChanges`: `TextNormalizationInterceptor` (NFC + whitespace collapse on required text) and `AuditSaveChangesInterceptor` (UTC `CreatedAtUtc`/`UpdatedAtUtc`, preserves creation timestamp). Concurrency uses EF `rowversion` tokens; a zero-row `UPDATE` is treated as a concurrency conflict.
- **Safety**: this repo treats migrations as high-risk and reviewed carefully — see the `AddP0DatabaseProtections` migration, `CatalogDatabaseProtections.cs`, and `tests/Inovait.IntegrationTests/Persistence/P0DatabaseProtectionTests.cs`, which assert migration `Up`/`Down` idempotency, safe rollback against foreign roles/members, and that protective triggers/DENY grants survive re-apply. Several catalog columns (`School.Code`, `School.Sector`, `AcademicYear.Code`, `Grade.Code`) are immutable by both EF (`PropertySaveBehavior.Throw`) and SQL triggers — do not add code that mutates them. `catalog.DocumentType` is read-only at the SQL role level (`GRANT SELECT` + `DENY INSERT/UPDATE/DELETE` to `inovait_runtime`). No soft delete, no destructive `DELETE` paths exist in application code — treat any new destructive migration or bulk-delete logic as requiring the same evidence rigor as `P0DatabaseProtectionTests`.
- `database/setup.sql` (a from-scratch parity script for the evaluator) does not exist yet — it is planned but not implemented; do not assume it is present.

## Existing workflow (SDD)

This repo is developed under a strict Spec-Driven Development process with its own PR-sizing and evidence-gate discipline (slices, `HUMAN_BASE`/`HUMAN_HEAD` line-diff gates, generated-file manifests). Source of truth:
- `specs/001-school-enrollment-management/` — spec, plan, data model, tasks, quickstart.
- `openspec/changes/school-enrollment-management/` — proposal, design, delta specs, tasks, state.
- `.specify/` — Spec Kit scaffolding/templates.
- `docs/evaluator-execution.md`, `docs/testing-strategy.md`, `docs/architecture.md`, `docs/task-id-supersession.md` — process and evidence detail.
- `.atl/skill-registry.md` — indexed agent skills for this repo.

Don't reproduce or re-derive this process from first principles — read the files above when a task requires it (e.g., adding a slice, closing evidence, touching `specs/.../contracts`).
