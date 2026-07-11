# Inovait — Backend

API planificada para la evaluación técnica de gestión de inscripciones escolares y contratos docentes.

> **Estado actual**: planificación únicamente. El repositorio no contiene scaffold .NET, proyectos, código fuente, pruebas, migraciones ni `database/setup.sql`. Los artefactos de `specs/`, `docs/`, `.specify/` y `.agents/` están en el working tree sin seguimiento; el commit actual `ce160e9...` contiene solo el baseline inicial de README/gitignore.

## Compromiso de una jornada

P0 es el único MVP comprometido:

1. crear o reutilizar `Student` y crear `Enrollment` atómicamente;
2. consultar inscripciones por School, Grade y AcademicYear;
3. crear contratos independientes de un Teacher para varias escuelas y consultarlos;
4. proveer catálogos necesarios, SQL mínimo, pruebas críticas y walkthrough del evaluador.

P1 mantiene planificación completa para los reportes municipales, pero es una extensión condicional después de evidencia P0.

## Stack aprobado para implementación futura

- SDK .NET `10.0.109`, C# 14 y `net10.0`.
- ASP.NET Core/EF Core SQL Server `10.0.9`.
- SQL Server 2022.
- xUnit v3 `3.2.2`, `Microsoft.AspNetCore.Mvc.Testing` `10.0.9` y `Testcontainers.MsSql` `4.13.0`.
- Validación integrada de ASP.NET Core y servicios de dominio explícitos; no se planifican FluentValidation ni FluentAssertions.

Estas son decisiones de planificación, no dependencias instaladas.

## Contrato HTTP canónico

El bundle modular está en [`specs/001-school-enrollment-management/contracts/openapi.yaml`](specs/001-school-enrollment-management/contracts/openapi.yaml) y preserva exactamente 15 `operationId`.

Rutas funcionales principales:

- `POST /api/enrollments` — alta atómica de Student/Enrollment.
- `GET /api/enrollments` — consulta conjunta por `schoolId`, `gradeId` y `academicYearId`.
- `POST /api/teachers/{teacherId}/contracts` — contratos multiescuela atómicos.
- `GET /api/teachers/{teacherId}/contracts` y `GET /api/schools/{schoolId}/teachers` — historia contractual.
- `GET /api/reports/*` y `GET /api/students/{documentType}/{documentNumber}/history` — P1 condicional.

Las referencias School/Grade/AcademicYear existentes siempre forman un contexto consultable; sin ClassGroup se devuelve `200 []`. No existe ni se planifica una tabla adicional de oferta académica.

El checksum combinado local `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a` solo prueba el working tree. Antes de implementar se requiere autorización explícita para versionar la planificación y registrar un commit que contenga el bundle completo.

## Ejecución futura

Los comandos se documentarán y validarán durante apply. Las rutas previstas son:

- integración primaria con Testcontainers;
- `database/setup.sql` mínimo para el evaluador;
- conexión SQL Server externa solo como fallback aislado, no como segunda puerta obligatoria.

Consultar [quickstart.md](specs/001-school-enrollment-management/quickstart.md), [assessment-baseline.md](docs/assessment-baseline.md) y [requirements-traceability.md](docs/requirements-traceability.md).

## Fuera de alcance

Autenticación, autorización, microservicios, CQRS/event sourcing, cloud, CRUD administrativo, paginación y datos personales reales.
