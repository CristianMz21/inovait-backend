# Inovait — Back End (.NET Core)

Back-end API for the **Inovait Enrollment Management System** (technical assessment).

This repository currently ships only the project scaffold and documentation. **No source code has been written yet** by design — code will be added in subsequent commits that satisfy the spec below.

## Tech Stack (planned)

- **Runtime:** .NET 8 (LTS)
- **Framework:** ASP.NET Core Web API
- **Persistence:** SQL Server (preferred per brief) via Entity Framework Core
- **Validation:** FluentValidation
- **Logging:** `Microsoft.Extensions.Logging` (structured)
- **Testing:** xUnit + FluentAssertions

## Scope (from the assessment brief)

The system manages student enrollments and teacher contracts for a single city and must answer questions like:

- Children enrolled between ages 3–7 (and age buckets in general).
- Teachers contracted by public vs. private sector.
- School with the highest number of students.
- Historical grade/group participation per student and assigned teacher.

### Business rules (from the brief)

1. A teacher may work at more than one school simultaneously.
2. A student belongs to exactly one school.
3. All schools belong to the same city.
4. Schools are public or private sector.
5. Schools track enrollment per grade.
6. Students are assigned to a specific group within a grade; year-over-year, group changes are tracked.

### Functional endpoints covered by the back-end

- `POST /api/students` — create a student in a school, grade, and year.
- `GET /api/students` — query students by **grade**, **school**, and **year**.
- `GET /api/teachers` — list teachers and their school assignments.
- `GET /api/reports/*` — KPIs (enrollment by age bucket, public/private split, top school, student history).

Schools, grades, and teachers will be **seeded via constants or DB lookup tables**. No admin CRUD is required for them.

## Database (planned)

SQL Server is the preferred engine per the brief.

Deliverables include a **DB script with only the tables and seed data needed to run the functional endpoints** — nothing more.

## Non-Goals (out of scope)

- Authentication / authorization.
- Multi-city support.
- Real-time features (SignalR, etc.).

## Repository Conventions

- **Branching:** Trunk-based; short-lived feature branches named `feat/<slug>`, `fix/<slug>`.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`).
- **No AI attribution** in commit messages.
- **Strict typing:** `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- **No suppressions:** no `#pragma warning disable`, no `// type: ignore`, no skipped tests.

## Local Setup (once source code lands)

```bash
dotnet restore
dotnet build
dotnet run --project src/Inovait.Api
```

Database migrations will be added via `dotnet ef migrations add <Name>` in a later commit.

## Related

- Front-end repo: see `../inovait-frontend` (sibling directory).

---

**Status:** Scaffold only — awaiting implementation per the technical assessment.
