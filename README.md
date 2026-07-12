# Inovait — Backend

API para la evaluación técnica de gestión de inscripciones escolares y contratos docentes.

> **Estado actual**: `production-model-v2.0.0` está completo — 103/103 tareas, 0 pendientes (S01–S18 cerrados). El modelo de producción materializa 14 tablas/5 triggers mediante una cadena de cuatro migraciones EF (`InitialP0ProductionModel`, `AddP0DatabaseProtections`, `AddP1TeachingModel`, `AddP1DatabaseProtections`), con `database/setup.sql` en paridad verificada. Los 15 `operationId` del baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` están mapeados en runtime (`IT-OPENAPI`), incluidos inscripción atómica, consulta de inscritos, contratos docentes multiescuela, catálogo de materias y los cuatro reportes/historia P1 (`getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`, `getStudentHistory`). `./scripts/run-p0-tests.sh` termina `P0 GATE PASSED: 37/37` y `./scripts/run-p1-tests.sh` termina `P1 GATE PASSED: 13/13`. El empaquetado/paquete final fuera del repositorio no se ejecutó (requiere autorización explícita no otorgada).

## Compromiso de una jornada

P0 es el único MVP comprometido:

1. crear o reutilizar `Student` y crear `Enrollment` atómicamente;
2. consultar inscripciones por School, Grade y AcademicYear;
3. crear contratos independientes de un Teacher para varias escuelas y consultarlos;
4. proveer catálogos necesarios, SQL mínimo, pruebas críticas y walkthrough del evaluador.

P1 (reportes municipales e historia de estudiante) quedó entregado end-to-end como extensión condicional posterior a la evidencia P0, con su propio runner de gate (`./scripts/run-p1-tests.sh`).

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

## Entorno local validado (S12)

`compose.yaml` levanta SQL Server 2022 CU14 (`ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD` externo a Git, puerto parametrizable, volumen nombrado y health check) para desarrollo y evaluación local:

```bash
export MSSQL_SA_PASSWORD='<clave-fuerte-propia>'
docker compose -f compose.yaml up -d --wait
```

La API se apunta al contenedor con la misma clave runtime de siempre, `ConnectionStrings__InovaitDatabase`, y `database/setup.sql` se aplica con `sqlcmd` sobre la base vacía (14 tablas, paridad con la migración). El runner `./scripts/run-p0-tests.sh` valida los 37 IDs del manifest P0 canónico y termina con `P0 GATE PASSED: 37/37`; el runner `./scripts/run-p1-tests.sh` valida los 13 IDs del manifest P1 canónico y termina con `P1 GATE PASSED: 13/13`. Detalle completo en [quickstart.md](specs/001-school-enrollment-management/quickstart.md).

Consultar [quickstart.md](specs/001-school-enrollment-management/quickstart.md), [assessment-baseline.md](docs/assessment-baseline.md) y [requirements-traceability.md](docs/requirements-traceability.md).

## Fuera de alcance

Autenticación, autorización, microservicios, CQRS/event sourcing, cloud, CRUD administrativo, paginación y datos personales reales.
