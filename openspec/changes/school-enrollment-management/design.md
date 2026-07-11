# Design Mirror: School Enrollment Management

> **Non-authoritative compatibility artifact for OpenSpec change `school-enrollment-management`.** This file is generated only for native OpenSpec dispatcher compatibility. It mirrors `specs/001-school-enrollment-management/` one way; it MUST NOT define, amend, or override design. On any drift, the canonical files listed below win and apply is blocked.

## Authority and Source Map

| IDs | Canonical source |
|---|---|
| D01–D02 | `specs/001-school-enrollment-management/research.md` — “Plataforma y versiones”, “Controllers frente a Minimal APIs”, “Estructura de proyectos”, “Acceso a datos y transacciones” |
| D03–D09 | `specs/001-school-enrollment-management/data-model.md` — “Resultado”, “Convenciones transversales”, “Auditoría y concurrencia”, all table sections, “Supuesto de clustering” |
| D10–D11 | `specs/001-school-enrollment-management/plan.md` — “database/setup.sql y paridad”, “Estrategia de entrega”, “Puertas”; supplemented by `research.md` — “Estrategia de pruebas SQL Server” |

## Mapped Decisions (11)

| ID | Canonical choice and rationale |
|---|---|
| D01 | .NET SDK `10.0.109`, `net10.0`, C# 14, ASP.NET/EF Core SQL Server `10.0.9`, SQL Server 2022: installed LTS alignment; no vulnerable runtime OpenAPI package. |
| D02 | Controllers and feature ports over three production projects (`Api`, `Core`, `Infrastructure`); EF direct, no CQRS/MediatR/generic repository: visible boundaries without empty layers. |
| D03 | Four schemas and 14 tables: `catalog`—School, AcademicYear, AcademicConfiguration, Grade, DocumentType (P0), Subject (P1); `people`—Person, Student, Teacher; `academic`—ClassGroup, Enrollment (P0), TeachingAssignment, ClassSchedule (P1); `staff`—TeacherContract. P0 creates 11 before P1 adds 3. |
| D04 | Integer identity PKs except singleton `AcademicConfiguration.Id`, role PK/FKs, and `ClassSchedule` composite PK. All FKs are `NO ACTION`; business dates are `date`/`DateOnly`; comparable text is `Latin1_General_100_CI_AS`. Application performs NFC/Unicode-whitespace normalization; named `LEN(TRIM(...)) > 0` checks provide the narrower SQL defense. |
| D05 | Audit exactly School, AcademicYear, Grade, ClassGroup, Person, Teacher, TeacherContract, Subject, TeachingAssignment with `CreatedAtUtc`, `UpdatedAtUtc`, chronological check, and `rowversion`; defaults create, `TimeProvider` interceptor updates, conflicts map to 409. Enrollment/ClassSchedule are creation-only; DocumentType/Student/AcademicConfiguration are unaudited. |
| D06 | Stable codes and `School.Sector` use restricted setters, EF after-save throw, and narrow SQL triggers. `AcademicConfiguration(Id=1)` uses PK+CHECK, seed, fail-fast startup, runtime no insert/delete, and anti-delete trigger; DocumentType is runtime read-only. |
| D07 | Person owns identity; Student and Teacher are independent PK/FK roles. Enrollment retains only controlled `AcademicYearId`, with `UQ_Enrollment_StudentPersonId_AcademicYearId` and composite FK to `UQ_ClassGroup_Id_AcademicYear_ForEnrollment`, preserving annual concurrency and 3NF. |
| D08 | TeacherContract cancellation is all-or-none and effective-dated; exact duplicates are unique, overlap is checked under indexed `Serializable`. TeachingAssignment validates contract/group school and bounded periods in one transaction; ClassSchedule requires atomic weekdays 1–7. |
| D09 | Preserve `IX_Person_LastNames_FirstNames_Id` INCLUDE document/birth; `IX_ClassGroup_AcademicYearId_GradeId_SchoolId` INCLUDE `Code` plus `IX_ClassGroup_GradeId`; `IX_Enrollment_ClassGroupId_StudentPersonId` INCLUDE year/created; both canonical TeacherContract teacher/date and school/date indexes with their listed includes; both TeachingAssignment group/date and contract/date indexes with their listed includes plus `IX_TeachingAssignment_SubjectId`. Clustered `Id` PKs supply `Id`; clustering changes reopen metadata tests. |
| D10 | Generated `InitialP0ProductionModel` then manual `AddP0DatabaseProtections`; P1 mirrors this with generated teaching model then Subject protection. `database/setup.sql` targets an empty DB with transaction/TRY-CATCH, schemas, ordered tables, constraints, indexes/includes, triggers, fictitious seeds, singleton, and login-free least-privilege role. Migration chain and setup MUST match metadata, seeds, triggers, and permissions. |
| D11 | Real SQL Server tests use xUnit v3, shared Testcontainers SQL Server primary path, clean DB per scenario, `Priority=P0`, and a runner that proves nonzero/minimum discovery before execution. Delivery is `stacked-to-main`, each human delta ≤400 lines, ordered S01→S02→S03→S04; S05 and S06 follow S04; S07 follows S03–S06; S08–S12 are sequential; P0 gate precedes S13; S13→S14–S17→S18. |

## Constraint and Verification Ledger

Named uniques/checks remain exactly those in `data-model.md` table sections: catalog code/name/sort/date/singleton checks; Person document identity; role PK/FKs; ClassGroup context/composite support key; Enrollment annual unique/composite FK; TeachingAssignment contract-group-subject/date checks; ClassSchedule composite PK/weekday check; TeacherContract exact unique plus date/status/cancellation checks. No duplicated comparison/report columns, generic soft delete, cross-row triggers, or persisted derived status.

Parity tests compare `sys.schemas`, `sys.tables`, `sys.columns`, `sys.default_constraints`, `sys.check_constraints`, `sys.foreign_keys`, `sys.indexes`, `sys.index_columns`, `sys.triggers`, and permissions. Gates: pre-apply branch/clean-scope/size; P0 = 11 tables, migration/setup parity, US1–US3, exact 37-ID manifest, P0 suite, walkthrough; P1 starts only after P0 and proves 14 tables plus four capabilities.

## Current Execution Snapshot

Verified repository HEAD `034ddc7`: S01 and S02 are complete; S03A is partial through catalog entities/mappings and executable partial catalog evidence for the five P0 catalog tables. S03 seed/startup checks and SQL triggers/permissions remain outside that partial evidence; migrations, full 11-table parity, API slices, and P1 remain later slices. This snapshot reports implementation state and does not alter canonical design.

## Open Questions

None. Any future discrepancy is resolved by regenerating this mirror from the three canonical root artifacts.
