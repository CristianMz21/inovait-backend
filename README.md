# Inovait — Backend

API para la evaluación técnica de gestión de inscripciones escolares y contratos docentes.

> **Estado actual**: S01 materializa la solución .NET, tres proyectos de producción, dos proyectos de pruebas y smoke tests HTTP. S02 y todo el dominio siguen pendientes; todavía no existen entidades, migraciones ni `database/setup.sql`. El baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` conserva sin cambios los diez YAML del bundle OpenAPI.

## Compromiso de una jornada

P0 es el único MVP comprometido:

1. crear o reutilizar `Student` y crear `Enrollment` atómicamente;
2. consultar inscripciones por School, Grade y AcademicYear;
3. crear contratos independientes de un Teacher para varias escuelas y consultarlos;
4. proveer catálogos necesarios, SQL mínimo, pruebas críticas y walkthrough del evaluador.

P1 mantiene planificación completa para los reportes municipales, pero es una extensión condicional después de evidencia P0.

## Stack materializado en S01

- SDK .NET `10.0.109`, C# 14 y `net10.0`.
- ASP.NET Core/EF Core SQL Server `10.0.9`.
- SQL Server 2022.
- xUnit v3 `3.2.2`, `Microsoft.NET.Test.Sdk` `18.0.1`, runner Visual Studio `3.1.5` y `Microsoft.AspNetCore.Mvc.Testing` `10.0.9`.
- Validación integrada de ASP.NET Core y servicios de dominio explícitos; no se planifican FluentValidation ni FluentAssertions.

EF Core SQL Server ya está referenciado en Infrastructure. `Testcontainers.MsSql` se incorpora recién en S02, junto con el fixture relacional. La generación OpenAPI runtime no forma parte de S01: `Microsoft.AspNetCore.OpenApi` `10.0.9` se retiró porque su dependencia transitiva estable disponible presenta una vulnerabilidad alta; el YAML canónico no cambia.

## Contrato HTTP canónico

El bundle modular está en [`specs/001-school-enrollment-management/contracts/openapi.yaml`](specs/001-school-enrollment-management/contracts/openapi.yaml) y preserva exactamente 15 `operationId`.

Rutas funcionales principales:

- `POST /api/enrollments` — alta atómica de Student/Enrollment.
- `GET /api/enrollments` — consulta conjunta por `schoolId`, `gradeId` y `academicYearId`.
- `POST /api/teachers/{teacherId}/contracts` — contratos multiescuela atómicos.
- `GET /api/teachers/{teacherId}/contracts` y `GET /api/schools/{schoolId}/teachers` — historia contractual.
- `GET /api/reports/*` y `GET /api/students/{documentType}/{documentNumber}/history` — P1 condicional.

Las referencias School/Grade/AcademicYear existentes siempre forman un contexto consultable; sin ClassGroup se devuelve `200 []`. No existe ni se planifica una tabla adicional de oferta académica. El modelo de producción ubica `AcademicYear` en `catalog`, centraliza identidad en `people.Person` y materializa 11 tablas P0 (14 con P1).

La prueba contractual primaria es igualdad del árbol respecto de `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` y ausencia de untracked:

```bash
git diff --exit-code 1223630ab99bf1bfaa4f5919fccf5ff539379c8e -- specs/001-school-enrollment-management/contracts
test -z "$(git status --porcelain --untracked-files=all -- specs/001-school-enrollment-management/contracts)"
```

Como evidencia secundaria, desde `specs/001-school-enrollment-management/contracts/`, el orden canónico es:

```bash
sha256sum openapi.yaml \
  paths/catalogs.yaml paths/enrollments.yaml \
  paths/teacher-contracts.yaml paths/reports.yaml \
  components/catalogs.yaml components/enrollments.yaml \
  components/teacher-contracts.yaml components/reports.yaml \
  components/problems.yaml | sha256sum
```

Produce `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a  -`. El task set actual es `production-model-v2.0.0` (`V2-T001`–`V2-T103`); los IDs v1 del baseline están supersedidos. La estrategia de revisión quedó resuelta como `stacked-to-main`, con gate pre-merge de hasta 400 líneas humanas en cada slice y scaffold/lockfiles/migraciones generadas aislados. `EX-PLAN-2026-07-10` exceptúa únicamente el work unit separado del churn documental de planificación ya aprobado; S01, su registro posterior de evidencia y los slices siguientes permanecen sujetos a sus gates.

## Ejecutar S01

```bash
dotnet restore
dotnet build --no-restore --configuration Debug
dotnet test --no-build --no-restore --configuration Debug
```

Las rutas previstas para S02 y posteriores son:

- integración primaria con Testcontainers;
- `database/setup.sql` mínimo para el evaluador;
- conexión SQL Server externa solo como fallback aislado, no como segunda puerta obligatoria.

Consultar [quickstart.md](specs/001-school-enrollment-management/quickstart.md), [assessment-baseline.md](docs/assessment-baseline.md) y [requirements-traceability.md](docs/requirements-traceability.md).

## Fuera de alcance

Autenticación, autorización, microservicios, CQRS/event sourcing, cloud, CRUD administrativo, paginación y datos personales reales.
