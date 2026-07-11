# Plan de implementación: gestión escolar con modelo de producción

**Rama**: `feat/production-data-model` | **Fecha**: 2026-07-10 | **Especificación**: [spec.md](./spec.md)

**Estado**: planificación actualizada y S01 materializado; S02–S18 permanecen pendientes. Esta actualización no autoriza commit, merge ni push.

**Task set ejecutable**: `production-model-v2.0.0` (`V2-T001`–`V2-T103`). Los IDs históricos `T001`–`T076` del baseline v1 están supersedidos y no son válidos para ejecución actual; ver [task-id-supersession.md](../../docs/task-id-supersession.md).

## Resumen

El modelo futuro tendrá 14 tablas en cuatro schemas SQL Server. P0 continúa primero, pero ahora materializa 11 tablas para compartir identidad mediante `Person`, usar `DocumentType` y seleccionar el año actual con un singleton seguro. P1 sigue condicionado a la puerta P0 y agrega tres tablas. Se mantienen tres proyectos de producción, API sin cambios y consultas derivadas sin agregados.

## Contexto técnico

- C# 14, SDK .NET `10.0.109`, `net10.0`; ASP.NET Core/EF Core SQL Server `10.0.9`.
- SQL Server 2022; `date`/`DateOnly`, `datetime2(3)` UTC, `rowversion`, `Latin1_General_100_CI_AS`.
- xUnit v3 `3.2.2`, `Microsoft.NET.Test.Sdk` `18.0.1`, runner Visual Studio `3.1.5` y `WebApplicationFactory` `10.0.9` ya materializados; `Testcontainers.MsSql` `4.13.0` comienza en S02.
- Monolito modular con `Inovait.Api`, `Inovait.Core`, `Inovait.Infrastructure`; dos proyectos de pruebas.
- Sin CQRS, MediatR, `Generic Repository`, autenticación, soft delete genérico ni columnas duplicadas para comparación.

## Control constitucional

| Control | Evidencia | Estado |
| --- | --- | --- |
| P0 antes de P1 | 11 tablas P0 y US1–US3 antes de las tres tablas/reportes P1 | PASS |
| Integridad histórica | roles PK+FK, Enrollment/Contract históricos, FK `NO ACTION` | PASS |
| Fechas/auditoría | negocio en `date`; sellos exactos UTC solo donde se aprobaron | PASS |
| Validación por frontera | SQL, EF y aplicación distribuidos sin garantías ficticias | PASS |
| Trazabilidad | REQ-053–REQ-063 enlazados a diseño, pruebas y tareas | PASS |
| Contrato canónico | 15 operationIds sin cambio observable | PASS |
| Pruebas por riesgo | SQL Server real para collation, triggers, concurrencia e índices | PASS planificado |
| Entrega evaluable | migración/setup con paridad y slices ≤400 líneas humanas | PASS planificado |
| Seguridad | runtime con permisos mínimos; datos ficticios y sin secretos | PASS |

Un `FAIL` bloquea apply. El control se repetirá tras P0 y antes de P1.

## Modelo y schemas

| Schema | P0 | P1 |
| --- | --- | --- |
| `catalog` | School, AcademicYear, AcademicConfiguration, Grade, DocumentType | Subject |
| `people` | Person, Student, Teacher | — |
| `academic` | ClassGroup, Enrollment | TeachingAssignment, ClassSchedule |
| `staff` | TeacherContract | — |

`catalog.AcademicYear` es autoritativo. `Student.PersonId` y `Teacher.PersonId` son PK+FK independientes. `Enrollment.AcademicYearId` se conserva como única dependencia controlada para `UNIQUE(StudentPersonId, AcademicYearId)` y queda cerrado por el FK compuesto soportado por `UQ_ClassGroup_Id_AcademicYear_ForEnrollment`.

## Diseño EF Core exacto

```text
src/Inovait.Core/Domain/{Catalogs,People,Academics,Staff,Common}/
src/Inovait.Infrastructure/Persistence/
├── InovaitDbContext.cs
├── Configurations/<Entity>Configuration.cs
├── Interceptors/TextNormalizationInterceptor.cs
├── Interceptors/AuditSaveChangesInterceptor.cs
├── Seed/ProductionCatalogSeed.cs
└── Migrations/
```

Cada `IEntityTypeConfiguration<T>` configura `ToTable(table,schema)`, PK/FK/UNIQUE con nombres exactos, `DeleteBehavior.NoAction`, tipos/longitudes, collation, defaults, checks, conversiones de enums string, índices/includes y `IsRowVersion`. `SchoolSector` y `TeacherContractStatus` se convierten a `varchar`; `DateOnly` a `date`; timestamps permanecen UTC.

`TextNormalizationInterceptor` aplica NFC, trim y colapso del whitespace Unicode —incluidos tabs y saltos de línea— a todo texto requerido, y la validación rechaza el resultado vacío antes de persistir. `AuditSaveChangesInterceptor` fija `UpdatedAtUtc` en updates mediante `TimeProvider`; los defaults cubren inserts. La lista auditable exacta es `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`, `Subject` y `TeachingAssignment`; `Enrollment`/`ClassSchedule` solo registran creación y `DocumentType`/`Student`/`AcademicConfiguration` no reciben auditoría genérica ni rowversion. Códigos estables y `School.Sector` usan setter restringido y `PropertySaveBehavior.Throw`.

## SQL frente a aplicación

| Invariante | SQL Server | EF/aplicación |
| --- | --- | --- |
| texto | collation, UNIQUE y `LEN(TRIM([Column])) > 0`, que rechaza vacío o solo U+0020 en SQL directo, no todo whitespace Unicode | NFC/trim y colapso/rechazo de whitespace Unicode, incluidos tabs/newlines, antes de persistir |
| código/sector inmutable | cuatro triggers estrechos | save behavior throw |
| singleton/referencias | PK+CHECK máximo uno; seed, permisos y trigger anti-delete; rol `[inovait_runtime]` con SELECT y DENY explícito de INSERT/UPDATE/DELETE sobre `DocumentType` | singleton sin insert/delete y fail-fast si falta; `DocumentType` solo lectura |
| auditoría | defaults/check/rowversion | interceptor UpdatedAt y manejo de conflicto |
| cancelación | CHECK all-or-none y fecha efectiva | transición Confirmed→Cancelled |
| asignación | FK y rango propio | misma escuela y período contenido, en transacción |
| solapamiento contractual | UNIQUE exacto | lectura indexada `Serializable` |

No se usan triggers amplios para normalización, auditoría, solapamientos ni compatibilidad entre tablas.

## Índices OLTP

| Índice | Beneficio esperado |
| --- | --- |
| `UQ_Enrollment_StudentPersonId_AcademicYearId` | seek/defensa concurrente de inscripción anual |
| `IX_Enrollment_ClassGroupId_StudentPersonId` INCLUDE `(AcademicYearId, CreatedAtUtc)` | join desde grupos filtrados; `Id` ya está disponible por la PK clustered |
| `IX_TeacherContract_TeacherPersonId_StartDate_EndDate` INCLUDE `(SchoolId, Status, CancelledAtUtc, CancellationReason, CancellationEffectiveDate)` | lista e intersección por docente/fecha; `Id` implícito |
| `IX_TeacherContract_SchoolId_StartDate_EndDate` INCLUDE `(TeacherPersonId, Status, CancellationEffectiveDate)` | lista por escuela y conteo de sector; `Id` implícito |
| `IX_TeachingAssignment_ClassGroupId_StartDate_EndDate` INCLUDE `(TeacherContractId, SubjectId)` | historia por grupo y período; `Id` implícito |
| `IX_TeachingAssignment_TeacherContractId_StartDate_EndDate` INCLUDE `(ClassGroupId, SubjectId)` | validación y navegación por contrato; `Id` implícito |

`IX_ClassGroup_AcademicYearId_GradeId_SchoolId` incluye únicamente `Code`; su `Id` también llega por la PK clustered. PK/UNIQUE ya líderes cubren sus FK; solo se agregan índices mínimos para FK restantes. No se planifica índice filtrado contractual hasta que mediciones justifiquen un tercer árbol solapado.

Esta cobertura asume que cada PK `Id` conserva el clustering predeterminado de SQL Server/EF. Si una PK pasa a nonclustered o cambia la clustered key, `data-model.md`, las proyecciones y `IT-INDEXES-P0/P1` deben re-evaluar todos los INCLUDE antes del merge que cambie el clustering.

## `database/setup.sql` y paridad

El script futuro trabajará sobre una base vacía: `XACT_ABORT`, `TRY/CATCH`, transacción, schemas, tablas en orden, collations, defaults, constraints, índices/includes, triggers, seeds ficticios, singleton y rol database `[inovait_runtime]` sin login/credenciales. Debe ejecutar `GRANT SELECT ON OBJECT::catalog.DocumentType TO [inovait_runtime]` y `DENY INSERT, UPDATE, DELETE ON OBJECT::catalog.DocumentType TO [inovait_runtime]`; la migración manual y setup expresan la misma política. No crea database ni login. EF separa el scaffold generado `InitialP0ProductionModel` del migration manual `AddP0DatabaseProtections`, que solo contiene triggers/permisos; P1 repite el patrón. La cadena completa debe producir los mismos objetos que setup.

Las pruebas compararán `sys.schemas`, `sys.tables`, `sys.columns`, `sys.default_constraints`, `sys.check_constraints`, `sys.foreign_keys`, `sys.indexes`, `sys.index_columns`, `sys.triggers` y permisos. P0 espera 11 tablas; P1, 14.

## Estructura de documentación

```text
specs/001-school-enrollment-management/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── tasks.md
└── contracts/                 # sin cambios en este refactor
docs/{architecture,entity-relationship-model,testing-strategy,requirements-traceability}.md
```

## Estrategia de entrega

**Decisión**: `stacked-to-main`. Cada slice abre, en una fase posterior, un PR contra `main`, solo después de integrar su dependencia. No se crea ningún PR ahora. El presupuesto es ≤400 líneas humanas por slice; scaffold, lockfiles y migraciones generadas se aíslan antes de `HUMAN_BASE`. Cada S01–S18 fija tres SHAs inmutables: `SLICE_BASE`, `HUMAN_BASE` y `HUMAN_HEAD`; el gate suma additions+deletions exclusivamente de `HUMAN_BASE...HUMAN_HEAD` mediante `scripts/check-human-lines.py`. Un work unit documental posterior no altera esa evidencia. Un resultado >400 bloquea el slice y el siguiente salvo `size:exception` explícito. `EX-PLAN-2026-07-10` cubre solo el work unit separado de planificación documental ya aprobado; no exceptúa S01 ni la documentación de evidencia posterior.

| Slice | Resultado autónomo | Dependencia | Verificación futura |
| --- | --- | --- | --- |
| S01 | solución de tres proyectos y test harness HTTP; salida de scaffold aislada | planificación | materializado; gate por SHAs pendiente hasta crear commits |
| S02 | normalizador, auditoría/concurrencia y convenciones relacionales | S01 | unitarias + modelo EF |
| S03 | cinco tablas de catálogo P0, singleton, checks y save behavior | S02 | tres evidencias parciales ejecutables solo contra las cinco tablas; sin acreditar triggers/permisos |
| S04 | `Person` y roles duales | S03 | NFC/collation/roles/concurrencia |
| S05 | `ClassGroup`/`Enrollment` y unicidad anual | S04 | FK compuesto/3NF/índices |
| S06 | `TeacherContract` y cancelación/solapamiento | S04 | checks/Serializable/índices |
| S07 | migración P0 generada aislada + migration de protecciones SQL separado | S03–S06 | aplicar cadena a SQL Server limpio y ejecutar evidencia completa de 11 tablas, triggers, singleton y permisos después de V2-T045 |
| S08–S11 | catálogos/API, US1, US2 y US3 | S07, en orden | tests HTTP por capacidad |
| S12 | `database/setup.sql`, paridad y walkthrough P0 | S08–S11 | manifest exacto de 37 IDs, ejecución completa y puerta P0 |
| S13 | modelo/migración P1 y `listSubjects` end-to-end, generado aislado | puerta P0 | 14 tablas + prueba HTTP con orden `name, code, id` |
| S14–S17 | una capacidad P1 por slice | S13 | BQ aislada |
| S18 | hardening y entrega | aplicables | suite y walkthrough |

**Primer slice autónomo**: S01. Inicio: repositorio solo con planificación. Estado actual: la estructura de tres proyectos y dos proyectos de pruebas compila, el runner base ejecuta y no existe dominio ni migración. El manifest generado está en `docs/generated-manifests/s01.txt`: enumera las 16 rutas producidas por los comandos de scaffold, incluidas plantillas que después se modifican o eliminan. El commit generado conserva esas plantillas sin personalización; todo delta posterior —ediciones, altas y bajas— se cuenta en `HUMAN_BASE...HUMAN_HEAD`. El gate por SHAs se ejecuta después de crear los commits autorizados; rollback elimina únicamente la solución recién creada.

**Fallbacks predefinidos**: S03 se divide en tablas/configuración de catálogos y seed/startup check con evidencia parcial; no acredita triggers ni permisos. S07 se divide en migration generada, protecciones manuales y verificación completa posterior a V2-T045; S12 en setup, paridad/índices/permisos y runner/gate; S13 en modelo P1, migraciones/paridad y `listSubjects` end-to-end. Cada hijo conserva su propio manifest, `HUMAN_BASE`, gate ≤400 y rollback. No se improvisa una excepción para evitar la división.

## Puertas

1. **Pre-apply**: branch correcta, checkout sin implementación accidental, estrategia `stacked-to-main` registrada y primer slice ≤400 líneas humanas.
2. **P0**: 11 tablas, paridad migración/setup, US1–US3, manifest exacto de 37 IDs sin faltantes, suite `Priority=P0` ejecutada y walkthrough reproducible.
3. **P1**: solo después de evidencia P0; agrega tres tablas y cuatro capacidades.

## Riesgos

- Los triggers y permisos no forman parte completa del modelo relacional EF; deben versionarse en migración y probarse contra `setup.sql`.
- `Serializable` reduce carreras de solapamiento, pero requiere índices correctos y retry/mapeo de deadlock.
- La única tabla no BCNF, `Enrollment`, exige mantener siempre el FK compuesto; quitarlo convertiría `AcademicYearId` en segunda fuente de verdad.
- El presupuesto de una jornada sigue siendo de riesgo alto; el mayor modelo no autoriza recortar `database/setup.sql`, concurrencia ni la puerta P0.
