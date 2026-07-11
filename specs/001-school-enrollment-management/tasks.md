---
description: "Tareas P0-first para gestión de inscripción escolar y contratación docente"
---

# Tareas: Gestión de inscripción escolar y contratación docente

**Estado**: planificación únicamente; ningún comando fue ejecutado. P0 es el alcance comprometido de una jornada bajo un pronóstico de riesgo alto y las condiciones de la ruta crítica; P1 es una extensión condicional posterior a la puerta T050.

Las 76 tareas son ítems finos de checklist para dependencias, revisión y evidencia; no representan 76 horas secuenciales. La ejecución P0 asistida agrupa T001–T051 en los siete timeboxes de la ruta crítica de [quickstart.md](./quickstart.md). El pronóstico continúa siendo alto riesgo y no reduce las estimaciones de líneas siguientes.

## Review Workload Forecast

| Campo | P0 comprometido | P1 condicional / total posterior |
| --- | ---: | ---: |
| Líneas humanas estimadas | 2.400–3.500 | +1.500–2.200; total 3.900–5.700 |
| Scaffold/lock/config generado | 450–750 | sin incremento relevante |
| Migración/SQL generado | 300–650 | +180–320 |
| Riesgo 400 líneas | Alto | Alto |

La estrategia cacheada es `ask-on-risk`. Apply queda bloqueado hasta elegir una cadena o aprobar una excepción limitada a archivos generados. No existe una excepción aprobada hoy.

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High

### Unidades de implementación revisables

| Unidad | Resultado autónomo | Líneas humanas | Generado separado | Verificación |
| --- | --- | ---: | ---: | --- |
| WU00 | baseline de planificación autorizado | 0 | 0 | commit contiene bundle completo |
| WU01 | scaffold y configuración | 80–140 | 450–750 | restore/build/test base |
| WU02 | modelo/persistencia P0 | 300–400 | 180–320 migración | metadatos de 8 tablas |
| WU03 | host, errores y catálogos P0 | 280–400 | 0 | IT-CATALOGS/P0 |
| WU04 | alta de inscripción básica | 300–400 | 0 | alta/reuse/errores |
| WU05 | atomicidad y conflictos de inscripción | 260–380 | 0 | rollback/carrera anual |
| WU06 | consulta de inscripciones | 240–340 | 0 | filtros, vacío, orden |
| WU07 | contratos multiescuela | 320–400 | 0 | creación/lista/atomicidad |
| WU08 | concurrencia contractual | 220–320 | 0 | superposición serializable |
| WU09 | SQL/runner/docs de evaluación P0 | 240–360 | 120–330 SQL | paridad + gate ≥12 |
| WU10 | dominio/tablas/seeds P1 | 300–400 | 180–320 migración | 11 tablas, IT-SEED-P1 |
| WU11–WU14 | una capacidad P1 por unidad | 260–400 cada una | 0 | BQ aislada por unidad |
| WU15 | hardening y entrega | 260–400 | 0 | suite y walkthrough |

Los archivos generados no se mezclan con lógica humana. Si scaffold, lockfile, migración o SQL exceden 400 líneas, se requiere la decisión de T003; no se declara “cero excepciones” mientras siga pendiente.

## Fase 0: decisiones bloqueantes y guía temprana

- [ ] T001 Verificar en solo lectura que no existen `src/`, `tests/`, `database/setup.sql`, solución ni proyectos, y registrar el estado en `docs/evaluator-execution.md`; **Dep.** ninguna; **Criterio** planificación intacta, sin ejecutar scaffold/build/test.
- [ ] T002 [REQ-050; 15 operationIds] Obtener autorización explícita para crear un commit de baseline de planificación, versionar los diez YAML OpenAPI y registrar commit+checksum en `docs/evaluator-execution.md`; **Dep.** T001; **Criterio** el commit contiene el bundle completo y no se atribuye falsamente a `ce160e9...`. **Bloquea T005 si falta autorización.**
- [ ] T003 [REQ-051] Elegir antes del scaffold una estrategia chained/stacked o aprobar `size:exception` solo para archivos generados; **Dep.** T001; **Criterio** decisión, ramas objetivo, límites y rollback quedan en `docs/evaluator-execution.md`. **Bloquea T005 si sigue pendiente.**
- [ ] T004 Crear temprano `docs/evaluator-execution.md` con prerrequisitos, comandos futuros, expected P0 test count=12, walkthroughs, evidencias y secciones de handoff; **Dep.** T001; **Criterio** existe antes de cualquier handoff o puerta y no afirma resultados ejecutados.

## Fase 1: scaffold mínimo P0

- [ ] T005 Crear por comandos `global.json`, `Inovait.slnx`, tres proyectos de producción y dos de pruebas `net10.0`; **Dep.** T002–T004; **Criterio** estructura del plan, nullable habilitado y salida generada aislada para revisión.
- [ ] T006 Registrar referencias de proyectos sin ciclos y configurar C# 14, warnings-as-errors, `.editorconfig` y `.gitignore`; **Dep.** T005; **Criterio** Api/Infrastructure dependen de Core y pruebas de sus objetivos.
- [ ] T007 Instalar únicamente OpenAPI/EF SQL Server/EF Design/xUnit v3/Mvc.Testing/Testcontainers en versiones aprobadas; **Dep.** T005; **Criterio** versiones fijadas y `PrivateAssets=all` para Design.
- [ ] T008 [REQ-044,045] Configurar connection string vacía, CORS local y opciones Testcontainers sin secretos en `src/Inovait.Api/appsettings*.json` y tests; **Dep.** T005; **Criterio** ninguna credencial ni base compartida.
- [ ] T009 Cerrar WU01 con restore/build/format/test base y registrar diff humano versus generado; **Dep.** T006–T008; **Criterio** solución base verde y decisión T003 respetada.

## Fase 2: fundamento P0 — 8 tablas y pruebas críticas

- [ ] T010 [P] [US1–US3] [UT-IDENTITY,UT-AGE,UT-CONTRACT-*] Escribir pruebas unitarias P0 con `[Trait("Priority", "P0")]` en `tests/Inovait.UnitTests/Domain/`; **Dep.** T009; **Criterio** identidad, edad y períodos/estado efectivo aportan al mínimo de 12 casos.
- [ ] T011 [P] [REQ-001–026,REQ-046,REQ-052] Implementar solo School, AcademicYear, Grade, ClassGroup, Student, Enrollment, Teacher y TeacherContract en `src/Inovait.Core/Domain/`; **Dep.** T009; **Criterio** historia, fechas y composite FK conceptual sin Subject/Assignment/Schedule.
- [ ] T012 [REQ-001,006,020–023,042] Implementar normalización, edad, período contractual, business date y errores tipados P0 en `src/Inovait.Core/Domain/Common/`; **Dep.** T010,T011; **Criterio** pruebas T010 verdes.
- [ ] T013 Implementar fixture Testcontainers primario y guardia de fallback externo aislado en `tests/Inovait.IntegrationTests/Infrastructure/`; **Dep.** T007–T009; **Criterio** la puerta usa Testcontainers y el fallback no es una segunda ejecución obligatoria.
- [ ] T014 [REQ-009,020,022,046,052; IT-SQL-*] Escribir pruebas de metadatos P0 con `[Trait("Priority", "P0")]` en `tests/Inovait.IntegrationTests/Persistence/RelationalModelTests.cs`; **Dep.** T013; **Criterio** esperan 8 tablas, constraints, índices y deletes `NO ACTION`.
- [ ] T015 Crear `InovaitDbContext` y configuraciones de las 8 entidades P0, preservando `Enrollment(ClassGroupId,AcademicYearId)` compuesto y sin duplicar School/Grade; **Dep.** T011,T014; **Criterio** 3NF y T014 avanzan a verde.
- [ ] T016 Crear seeds P0 mínimos y deterministas para escuelas de ambos sectores, años, grados, grupos, docentes y contratos en `src/Inovait.Infrastructure/Persistence/Seed/`; **Dep.** T015; **Criterio** solo datos necesarios para US1–US3.
- [ ] T017 Generar y revisar migración `InitialP0` para 8 tablas y seeds; **Dep.** T015,T016; **Criterio** salida generada separada, sin Subject/TeachingAssignment/ClassSchedule ni agregados.
- [ ] T018 Registrar DbContext, business date y servicios de infraestructura en `DependencyInjection.cs`; **Dep.** T015; **Criterio** configuración obligatoria y operaciones async.
- [ ] T019 Configurar host Controllers, camelCase, CORS, OpenAPI, `ProblemDetails` y cancelación en Api; **Dep.** T018; **Criterio** sin auth ni detalles internos.
- [ ] T020 Validar WU02 con T010/T014 sobre Testcontainers; **Dep.** T012,T017–T019; **Criterio** 8 tablas P0 y unitarias críticas verdes.

## Fase 3: catálogos necesarios P0

- [ ] T021 [US1–US3] [IT-CATALOGS] Escribir pruebas HTTP P0 con `[Trait("Priority", "P0")]` para `listSchools`, `listGrades`, `listAcademicYears`, `listClassGroups` y `listTeachers`; **Dep.** T020; **Criterio** referencias existentes combinadas sin grupos devuelven 200 [] y orden estable.
- [ ] T022 Definir servicio/proyecciones de los cinco catálogos P0 en Core; **Dep.** T021; **Criterio** sin EF ni CRUD genérico.
- [ ] T023 Implementar consultas no-tracking y validación de existencia en Infrastructure; **Dep.** T022; **Criterio** no existe tabla de oferta ni 422 por combinación existente.
- [ ] T024 Crear DTOs y `CatalogsController` para los cinco operationIds P0; **Dep.** T023; **Criterio** schemas y órdenes coinciden con OpenAPI.
- [ ] T025 Integrar errores de catálogo 400/404 y `200 []`; **Dep.** T024; **Criterio** ninguna lista declara 422 sin regla semántica.
- [ ] T026 Validar WU03 con IT-CATALOGS P0; **Dep.** T025; **Criterio** cinco operaciones verdes en Testcontainers.

## Fase 4: US1 — inscripción atómica

- [ ] T027 [US1] [SCN-001–006; IT-ENR-CREATE/IDENTITY/CONTEXT] Escribir pruebas HTTP/SQL con `[Trait("Priority", "P0")]` en `CreateEnrollmentTests.cs`; **Dep.** T026; **Criterio** 201, reuse, 404, 409 y 422 solo para ClassGroup ajeno al contexto.
- [ ] T028 [US1] [SCN-004,007; IT-ENR-ATOMIC] Escribir rollback/carrera anual con `[Trait("Priority", "P0")]` en `EnrollmentAtomicityTests.cs`; **Dep.** T026; **Criterio** cero persistencia parcial y un único ganador.
- [ ] T029 Definir command/result/writer y servicio de alta en Core; **Dep.** T027,T028; **Criterio** normaliza, compara y valida sin ASP.NET/EF.
- [ ] T030 Implementar transacción y traducción de colisiones SQL en Infrastructure; **Dep.** T029; **Criterio** Student+Enrollment todo-o-nada.
- [ ] T031 Crear request, `CreateEnrollmentResponse` y POST `createEnrollment` sin header Location; **Dep.** T030; **Criterio** `studentReused` solo aparece en create.
- [ ] T032 Validar WU04/WU05 con T027–T028; **Dep.** T031; **Criterio** SCN-001–007 verdes en Testcontainers.

## Fase 5: US2 — consulta de inscripciones

- [ ] T033 [US2] [SCN-008–012; IT-ENR-FILTER] Escribir pruebas P0 con `[Trait("Priority", "P0")]` en `ListEnrollmentsTests.cs`; **Dep.** T032; **Criterio** tres filtros, 404 de referencia, 200 [] sin grupos, edad y orden ascendente completo.
- [ ] T034 Definir query/proyección y consulta no-tracking de enrollments; **Dep.** T033; **Criterio** IDs existentes no requieren una relación adicional.
- [ ] T035 Crear `EnrollmentListItem` y GET `listEnrollments`; **Dep.** T034; **Criterio** nunca serializa `studentReused`, no pagina y no declara 422 sin causa.
- [ ] T036 Validar WU06 con T033; **Dep.** T035; **Criterio** SCN-008–012 repetibles.

## Fase 6: US3 — contratos multiescuela

- [ ] T037 [US3] [REQ-018–026,REQ-047,REQ-048; SCN-013–019; IT-CON-MULTI/DATES/LIST] Escribir pruebas P0 con `[Trait("Priority", "P0")]` en `TeacherContractEndpointsTests.cs`; **Dep.** T036; **Criterio** array duplicado llega a aplicación y devuelve 409, creación atómica y órdenes ascendentes.
- [ ] T038 [US3] [IT-CON-OVERLAP] Escribir carrera serializable con `[Trait("Priority", "P0")]` en `TeacherContractConcurrencyTests.cs`; **Dep.** T036; **Criterio** una superposición falla y escuelas distintas funcionan.
- [ ] T039 Definir commands/queries/store y servicio contractual en Core; **Dep.** T037,T038; **Criterio** puertos específicos y validación completa previa.
- [ ] T040 Implementar transacción Serializable y consultas por docente/escuela en Infrastructure; **Dep.** T039; **Criterio** cero resultados parciales y orden canónico.
- [ ] T041 Crear DTOs y operaciones `createTeacherContracts`, `listTeacherContracts`, `listTeachersBySchool`; **Dep.** T040; **Criterio** sin Location, `evaluatedAt` presente y GET sin 422 injustificado.
- [ ] T042 Validar WU07/WU08 con T037–T038; **Dep.** T041; **Criterio** SCN-013–019 verdes en Testcontainers.

## Fase 7: entrega y puerta P0

- [ ] T043 [REQ-045,REQ-049,REQ-052] Crear `database/setup.sql` mínimo desde `InitialP0`, con 8 tablas, constraints, índices y seeds P0 en transacción; **Dep.** T017,T042; **Criterio** sin CREATE DATABASE, secretos, P1 ni agregados.
- [ ] T044 [IT-SQL-SCRIPT] Escribir paridad P0 con `[Trait("Priority", "P0")]` en `SetupSqlParityTests.cs`; **Dep.** T043; **Criterio** script y migración coinciden en 8 tablas y seeds sobre bases limpias.
- [ ] T045 [REQ-042–045,048,050; IT-PROBLEMS,IT-OPENAPI] Escribir pruebas P0 con `[Trait("Priority", "P0")]` para errores y contrato en `tests/Inovait.IntegrationTests/Api/`; **Dep.** T042; **Criterio** canonical bundle tiene 15 IDs, runtime P0 expone 10 y no filtra detalles.
- [ ] T046 Crear `scripts/run-p0-tests.sh`: listar `Priority=P0`, fallar bajo 12 casos y luego ejecutar el filtro; **Dep.** T010,T014,T021,T027,T028,T033,T037,T038,T044,T045; **Criterio** cero pruebas o descubrimiento incompleto nunca pasa.
- [ ] T047 Actualizar `README.md` con setup P0, Testcontainers primario, SQL del evaluador y fallback externo opcional; **Dep.** T043,T046; **Criterio** comandos copiables y estado de implementación real.
- [ ] T048 Completar `docs/evaluator-execution.md` con baseline commit/checksum, SQL, runner, requests/responses y observación local no bloqueante; **Dep.** T043–T047; **Criterio** walkthrough por persona sin contexto y sin prometer P1.
- [ ] T049 Preparar handoff frontend P0 que exija contrato backend tracked, clean y coincidente con baseline; **Dep.** T048; **Criterio** la verificación falla ante untracked/dirty/checksum distinto y no usa `ce160e9...`.
- [ ] T050 [REQ-027; OUT-001–004] Ejecutar `scripts/run-p0-tests.sh`, paridad de `database/setup.sql` y tres walkthroughs P0; **Dep.** T044–T049; **Criterio** ≥12 casos descubiertos, todos verdes y evidencia fechada. **Bloquea T052–T071 si falla.**
- [ ] T051 Emitir entrega/handoff P0 separado y registrar forecast real por WU; **Dep.** T050; **Criterio** P0 reproducible, frontend separado y estrategia de revisión cumplida.

## Fase 8: P1 condicional — modelo y datos, solo después de T050

- [ ] T052 [P] [US7] [REQ-040,041; SCN-035; UT-ASSIGNMENT] Escribir pruebas P1 para compatibilidad y weekdays con `[Trait("Priority", "P1")]`; **Dep.** T050; **Criterio** SCN-035 queda explícitamente backend-only.
- [ ] T053 [US7] [REQ-040,041] Implementar Subject, TeachingAssignment, ClassSchedule y policy en Core; **Dep.** T052; **Criterio** múltiples asignaciones y misma escuela/intersección temporal.
- [ ] T054 [REQ-040,041,052] Configurar las tres tablas P1 y extender DbContext; **Dep.** T053; **Criterio** modelo completo de 11 tablas, 3NF y sin duplicar contexto.
- [ ] T055 Generar migración `AddP1Reporting` separada; **Dep.** T054; **Criterio** solo tres tablas/constraints P1 y salida generada aislada.
- [ ] T056 [REQ-045,049] Crear seeds P1 de límites, empate, multisector y multiplicidad; **Dep.** T055; **Criterio** no altera evidencia P0.
- [ ] T057 [REQ-048,049] Implementar `listSubjects`; **Dep.** T054,T056; **Criterio** sexto catálogo, orden ascendente y operationId canónico.
- [ ] T058 [REQ-040,041,045,049,052] Escribir/ejecutar IT-SEED-P1 e IT-ASSIGNMENT con `[Trait("Priority", "P1")]`; **Dep.** T052–T057; **Criterio** 11 tablas y dataset P1 válidos.

## Fase 9: P1 condicional — cuatro capacidades

- [ ] T059 [P] [US4] [REQ-028–032,REQ-046,REQ-048; SCN-020–023; BQ-001,BQ-002; IT-RPT-AGE] Escribir pruebas P1 de edades/contexto con `[Trait("Priority", "P1")]`; **Dep.** T058; **Criterio** propiedades fijas age3To7/age8To12/ageOver12 y cero para referencias existentes sin grupos.
- [ ] T060 [US4] [REQ-028–032] Implementar `getAgeDistribution` end-to-end; **Dep.** T059; **Criterio** consulta derivada sin agregados ni buckets duplicables.
- [ ] T061 [P] [US5] [REQ-028,REQ-033–035,REQ-047; SCN-024–027; BQ-003; IT-RPT-SECTOR] Escribir pruebas P1 con `[Trait("Priority", "P1")]`; **Dep.** T058; **Criterio** propiedades fijas public/private distinct, período y estado.
- [ ] T062 [US5] [REQ-033–035,047] Implementar `getDistinctTeacherCountsBySector` end-to-end; **Dep.** T061; **Criterio** COUNT DISTINCT derivado y 422 solo para rango inválido.
- [ ] T063 [P] [US6] [REQ-028,REQ-036,REQ-037,REQ-048; SCN-028–030; BQ-004; IT-RPT-TOP] Escribir pruebas P1 con `[Trait("Priority", "P1")]`; **Dep.** T058; **Criterio** todos los empates, vacío y orden name/id ascendente.
- [ ] T064 [US6] [REQ-036,037] Implementar `getTopSchoolsByEnrollment` end-to-end; **Dep.** T063; **Criterio** agrupación derivada sin stored aggregate.
- [ ] T065 [P] [US7] [REQ-028,REQ-038–041,REQ-048; SCN-031–035; BQ-005; IT-HISTORY] Escribir historia/asignaciones P1 con `[Trait("Priority", "P1")]`; **Dep.** T058; **Criterio** años descendentes, asignaciones ascendentes y [] sin asignaciones.
- [ ] T066 [US7] [REQ-038–041] Implementar `getStudentHistory` end-to-end; **Dep.** T065; **Criterio** multiplicidad preservada y SCN-035 probado solo en backend.
- [ ] T067 [REQ-028,042–050] Integrar DTOs/controllers compartidos de P1 sin fusionar unidades de comportamiento; **Dep.** T060,T062,T064,T066; **Criterio** exactamente 15 operationIds runtime y ejemplos compatibles.
- [ ] T068 [REQ-027] Crear/ejecutar runner `Priority=P1`; **Dep.** T067; **Criterio** todas las pruebas P1 descubiertas y verdes sin afectar T050.
- [ ] T069 [REQ-045,049,052] Extender `database/setup.sql` y paridad de 8 a 11 tablas; **Dep.** T055,T056,T068; **Criterio** P0 sigue reproducible y P1 agrega solo su delta.
- [ ] T070 [REQ-042,044,048,050] Validar OpenAPI runtime completo contra bundle tracked/clean aprobado; **Dep.** T067,T069; **Criterio** refs válidos, 15 IDs únicos y schemas/examples compatibles.
- [ ] T071 [REQ-028–041; BQ-001–005] Recorrer BQ-001–BQ-005 y registrar cálculo manual; **Dep.** T068–T070; **Criterio** P1 queda evidenciado como stretch, no como compromiso diario.

## Fase 10: cierre

- [ ] T072 Completar matriz de errores, seguridad de datos y cancelación sin supresiones; **Dep.** T051 y, si se ejecutó P1, T071; **Criterio** P0 no depende de P1.
- [ ] T073 Ejecutar suite aplicable, build y format; **Dep.** T072; **Criterio** cero warnings, skips, secretos o base externa obligatoria duplicada.
- [ ] T074 Actualizar README, evaluator execution y trazabilidad con estado real, OUT-009 y conteos finales; **Dep.** T073; **Criterio** ninguna capacidad ejecutada se afirma pendiente ni viceversa.
- [ ] T075 Revisar 3NF, composite FK de Enrollment, cero School/Grade duplicados y cero agregados; **Dep.** T074; **Criterio** OUT-009 aprobado.
- [ ] T076 Empaquetar fuera del repositorio solo con baseline/implementación versionados y autorización vigente; **Dep.** T075; **Criterio** fuente, OpenAPI, ER, SQL y docs reproducibles sin secretos.

## Dependencias y cobertura

`T001–T004 → T005–T020 → T021–T026 → T027–T032 → T033–T036 → T037–T042 → T043–T049 → puerta T050 → T051 → P1 opcional T052–T071 → T072–T076`.

- **P0 comprometido**: T001–T051. Incluye 10 operaciones runtime: cinco catálogos, create/list enrollments y tres operaciones contractuales.
- **P1 condicional**: T052–T071. Agrega `listSubjects`, tres reportes e historia hasta completar 15 operationIds.
- **Tareas totales**: 76; P0 51, P1 20, cierre 5.
- **No existe conducta P1 antes de T050**; los documentos compartidos pueden describir el dominio completo.
- **Normalización**: se preservan 3NF, FK compuesto controlado de Enrollment y ausencia de School/Grade redundantes o agregados almacenados.
