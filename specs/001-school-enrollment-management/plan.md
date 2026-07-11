# Plan de implementación: Gestión de inscripción escolar y contratación docente

**Rama**: `main` | **Fecha**: 2026-07-10 | **Especificación**: [spec.md](./spec.md)

**Entrada**: especificación en `specs/001-school-enrollment-management/spec.md`

**Nota**: `/speckit-plan` completa esta planificación. No autoriza implementación.

## Resumen

El compromiso de alcance de una jornada se limita a P0: inscripción atómica, consulta por School/Grade/AcademicYear y contratación docente multiescuela, junto con catálogos necesarios, SQL Server mínimo, pruebas críticas y walkthrough del evaluador. Su factibilidad es **de riesgo alto** y está condicionada a la ruta crítica operativa de ocho horas definida en [quickstart.md](./quickstart.md); no es una promesa incondicional ni evidencia de ejecución. P1 permanece completamente diseñado como extensión condicional posterior a evidencia P0. La solución futura usa tres proyectos de producción y dos de pruebas, sin CQRS, MediatR ni `Generic Repository`; hoy el repositorio contiene únicamente planificación no versionada y el README inicial.

## Contexto técnico

**Lenguaje/versión**: C# 14, SDK .NET `10.0.109`, `net10.0`.

**Dependencias principales**: ASP.NET Core `10.0.9`, `Microsoft.AspNetCore.OpenApi` `10.0.9`, EF Core SQL Server `10.0.9`; DI, `ProblemDetails` y logging integrados. Pruebas futuras: xUnit v3 `3.2.2`, `Microsoft.AspNetCore.Mvc.Testing` `10.0.9` y `Testcontainers.MsSql` `4.13.0`.

**Persistencia**: SQL Server 2022 mediante EF Core; esquema 3NF, fechas de negocio `date`/`DateOnly`, restricciones e índices explícitos y borrados restrictivos.

**Pruebas**: aún no existe runner. P0 planifica unitarias puras y pruebas HTTP/SQL Server con `WebApplicationFactory` y Testcontainers como ruta reproducible primaria. Una instancia externa aislada queda documentada solo como fallback, no como segunda puerta obligatoria. EF InMemory no demostrará SQL ni transacciones.

**Plataforma objetivo**: Linux o Windows con .NET 10 SDK y SQL Server 2022 accesible; Docker compatible es requisito solo para la ruta Testcontainers.

**Tipo de proyecto**: servicio web REST dentro de un monolito modular.

**Objetivos de rendimiento**: recorridos P0 manuales en menos de 3 min. La latencia local se registra de forma informativa con una ejecución calentada por consulta P0, sin umbral ni puerta de release.

**Restricciones**: MVP de una jornada, P0 antes de P1, sin autenticación, secretos ni datos reales; CORS limitado a orígenes locales configurados; async y `CancellationToken` de HTTP a EF; nullable habilitado; DTOs separados de entidades; transacciones explícitas para operaciones atómicas.

**Escala/alcance**: una ciudad y volumen acotado sin paginación. P0 materializa 8 tablas y seeds mínimos; P1 puede extender a las 11 entidades ya diseñadas. Los reportes nunca almacenan agregados y el orden es determinista.

## Control constitucional

*PUERTA: DEBE aprobarse antes de investigación y repetirse después del diseño.*

**Ejecución previa a investigación (2026-07-10)**: PASS. Esta tabla registra la puerta inicial sobre el enfoque propuesto; las referencias de diseño fueron verificadas nuevamente al cerrar Phase 1.

| Control | Evidencia requerida | Estado |
| --- | --- | --- |
| P0 antes de P1 | US1-US3 forman el primer hito; US4-US7 quedan bloqueadas hasta validar P0 | PASS |
| Simplicidad | Tres proyectos de producción; Controllers; sin patrones o capacidades prohibidas | PASS |
| Integridad histórica | `Enrollment` y `TeacherContract` son tablas históricas; todas las FK usan `NO ACTION` | PASS |
| Fechas | Todas las fechas de negocio son `date`/`DateOnly`; no se requieren timestamps técnicos | PASS |
| Validación y errores | [data-model.md](./data-model.md) distribuye reglas y OpenAPI define `ProblemDetails` | PASS |
| Trazabilidad | [requirements-traceability.md](../../docs/requirements-traceability.md) cubre REQ, SCN, BQ, operación, tabla, pantalla y prueba | PASS |
| Contrato entre repositorios | [openapi.yaml](./contracts/openapi.yaml) es canónico y el impacto UI está identificado | PASS |
| Pruebas por riesgo | [testing-strategy.md](../../docs/testing-strategy.md) cubre límites, atomicidad, SQL y reportes | PASS |
| Accesibilidad | OpenAPI provee errores de campo y estados previsibles; la UI accesible queda en el frontend | PASS |
| Entrega evaluable | [quickstart.md](./quickstart.md) y la tarea temprana de `docs/evaluator-execution.md` planifican setup, SQL P0, seeds y walkthrough | PASS |
| Seguridad de datos | Todos los ejemplos y seeds planificados son ficticios; variables sin valores secretos | PASS |

Un `FAIL` bloquea el avance. Solo una excepción constitucional documentada puede continuar.

## Estructura del proyecto

### Documentación de esta feature

```text
specs/001-school-enrollment-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
└── contracts/
    ├── openapi.yaml
    ├── paths/
    └── components/
```

Los documentos transversales viven en `docs/architecture.md`, `docs/testing-strategy.md`, `docs/entity-relationship-model.md`, `docs/requirements-traceability.md` y `docs/assessment-baseline.md`. `docs/evaluator-execution.md` y `database/setup.sql` son entregables P0 futuros: sus tareas se ejecutan antes de la puerta P0; ninguno existe todavía.

### Código fuente

```text
Inovait.slnx
├── src/Inovait.Api/                 # Controllers, HTTP DTOs, ProblemDetails, DI
├── src/Inovait.Core/                # Domain y Features por caso de uso
├── src/Inovait.Infrastructure/      # EF Core, SQL Server, consultas y seeds
├── tests/Inovait.UnitTests/         # Reglas puras
└── tests/Inovait.IntegrationTests/  # WebApplicationFactory + SQL Server
```

**Decisión de estructura**: tres proyectos de producción. Separar cuatro (`Api/Application/Domain/Infrastructure`) añade referencias y capas con poco contenido para una jornada; uno o dos mezclan HTTP, dominio y EF o generan referencias circulares. `Inovait.Core` reúne Domain y Application en carpetas explícitas, mientras Infrastructure mantiene EF fuera del núcleo. [architecture.md](../../docs/architecture.md) conserva el análisis completo.

## Trazabilidad de diseño

La matriz canónica y exacta está en [requirements-traceability.md](../../docs/requirements-traceability.md); este plan no la duplica. Sus anclas son:

| Prioridad | Historias | Modelo principal | Operaciones | Evidencia |
| --- | --- | --- | --- | --- |
| P0 | US1 | `Student`, `ClassGroup`, `Enrollment` | `createEnrollment` | UT-IDENTITY; IT-ENR-CREATE/IDENTITY/ANNUAL/CONTEXT |
| P0 | US2 | `Enrollment` + catálogos | `listEnrollments` | UT-AGE; IT-ENR-FILTER |
| P0 | US3 | `Teacher`, `TeacherContract`, `School` | `createTeacherContracts`, `listTeacherContracts`, `listTeachersBySchool` | UT-CONTRACT-*; IT-CON-* |
| P1 | US4-US7 | Historia normalizada completa | `getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`, `getStudentHistory` | IT-RPT-*; IT-HISTORY |

Para listas y reportes, cualquier trío de referencias existentes School/Grade/AcademicYear es consultable. No se agrega tabla de oferta académica: la ausencia de `ClassGroup` produce `200 []` o conteos cero. `422` se reserva para reglas semánticas reales, como un `ClassGroup` suministrado al alta que no pertenece al contexto elegido.

## Impacto entre repositorios

| Decisión compartida | Backend canónico | Acción en `inovait-frontend` | Compatibilidad/versión |
| --- | --- | --- | --- |
| Identificadores y JSON | Schemas camelCase de OpenAPI | Generar/adaptar modelos sin renombrar conceptos | OpenAPI `1.0.0` |
| Errores | `ProblemDetails` con `code` y `errors` | Asociar errores a campos y anunciarlos accesiblemente | HTTP 400/404/409/422 según operación |
| Catálogos | operaciones `list*` | Poblar selectores y respetar orden recibido | Sin CRUD administrativo |
| P0 | operaciones de inscripción y contratos | Pantallas FE-S01 a FE-S04 | Sin autenticación; `security: []` |
| P1 | cuatro capacidades de reporte | FE-S05 y FE-S06 | BQ-001/002 comparten respuesta |

La planificación frontend está sincronizada para contenido de planificación: define 49 tareas —P0 T001–T035, P1 T036–T047 y cierre T048–T049—, 13 consumidores runtime y conserva `listSubjects` y `listTeachersBySchool` como contract-only; `SCN-035` permanece backend-only. No queda sincronización documental pendiente. El contrato aún no es reproducible y requiere el baseline versionado explícitamente autorizado descrito a continuación.

### Reproducibilidad del contrato

El bundle OpenAPI local es evidencia de working tree, no evidencia de `HEAD`: el commit `ce160e92d001c43a4ab5f7849da1957ae412b909` no contiene `specs/` ni el contrato. El checksum combinado local actual de los diez YAML, en el orden documentado en quickstart, es `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`; cambia si se edita el working tree y todavía no identifica un baseline aprobado. Antes de cualquier scaffold o implementación se requiere autorización explícita para crear un commit de planificación; luego se registrarán el hash que contenga los diez YAML y su checksum. La verificación frontend deberá fallar si el contrato está sin seguimiento, sucio o no coincide con ese baseline. No se crea ningún commit durante esta remediación.

### Puertas pre-apply

1. **Contrato**: obtener autorización explícita para versionar el baseline completo y registrar commit+checksum aprobados.
2. **Presupuesto de revisión**: la estrategia cacheada es `ask-on-risk`; antes del scaffold debe elegirse una cadena de PRs o aprobar una `size:exception` limitada a scaffold/migración/lockfiles generados. Mientras siga pendiente no se afirma “cero excepciones”.

## Seguimiento de complejidad

La redundancia controlada `Enrollment.AcademicYearId` es una decisión de integridad admitida por REQ-052, no una excepción: el FK compuesto impide divergencia y permite unicidad concurrente anual. La estrategia de entrega y cualquier excepción para archivos generados permanecen pendientes y bloquean apply; no son excepciones aprobadas.

## Control constitucional posterior al diseño

**Resultado**: PASS técnico condicionado por dos decisiones pre-apply: baseline contractual versionado y estrategia de revisión. El diseño mantiene P0 antes de P1, 3NF mínima, borrados restrictivos, OpenAPI canónico y Testcontainers; ninguna decisión pendiente autoriza scaffold ni implementación.

## Hitos y compromiso

### Condiciones del pronóstico diario

La ruta crítica P0 solo se considera operativa si la planificación y las dos puertas pre-apply están aprobadas antes de iniciar el reloj, SDK .NET, Node y Docker están listos, y una sola persona operadora trabaja con Codex/CLI sin interrupciones. P1 queda excluido. Los siete timeboxes, sus tareas y los cortes obligatorios de mitad de jornada y de la última hora están en [quickstart.md](./quickstart.md), como única fuente del horario.

Si el avance no cabe, solo puede retirarse hardening P0 no esencial —por ejemplo, observación de latencia, refactor cosmético o casos redundantes que no cubran un riesgo nuevo—. No se recorta ningún entregable original requerido, `database/setup.sql` ni una prueba crítica de integridad; si aun así no cabe, se declara incumplido el objetivo diario en lugar de prometer una entrega incompleta.

| Hito | Esencial | Diferible sin romper P0 |
| --- | --- | --- |
| Pre-apply | commit autorizado del baseline y decisión de revisión | nada: ambas puertas bloquean scaffold |
| Base P0 | scaffold, 8 tablas, migración P0, Testcontainers, `ProblemDetails`, cinco catálogos y seeds mínimos | Subject/TeachingAssignment/ClassSchedule y seeds de reportes |
| P0 comprometido | US1-US3 end-to-end, `database/setup.sql` mínimo, pruebas con trait P0, runner con conteo mínimo, README y walkthrough | toda conducta P1 |
| P1 condicional | cuatro consultas para BQ-001–BQ-005, tres tablas restantes, seeds y pruebas P1 | paginación, CRUD, autenticación y observabilidad avanzada |
