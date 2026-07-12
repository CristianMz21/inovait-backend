# Trazabilidad de requisitos

## Fuentes de verdad

| Aspecto | Fuente canónica |
| --- | --- |
| comportamiento/prioridad | `specs/001-school-enrollment-management/spec.md` |
| decisiones/implementación | `plan.md`, `research.md`, `data-model.md` |
| HTTP | `contracts/openapi.yaml` y referencias; sin cambios en este refactor |
| ER | `docs/entity-relationship-model.md` |
| pruebas | `docs/testing-strategy.md` |
| secuencia/slices | `specs/001-school-enrollment-management/tasks.md` |

La matriz cubre 63 REQ, 35 SCN, 5 BQ, 9 OUT, 15 `operationId` y 103 tareas del task set `production-model-v2.0.0`. P0 comprende V2-T001–V2-T075; P1 condicional V2-T076–V2-T099; cierre V2-T100–V2-T103. Los IDs v1 `T001`–`T076` no son ejecutables; ver [task-id-supersession.md](./task-id-supersession.md).

## Operaciones HTTP

El modelo de persistencia no altera el contrato. Los 15 `operationId` siguen siendo:

| Prioridad | Operaciones | Evidencia / productor | Tareas backend |
| --- | --- | --- | --- |
| P0 catálogos | `listSchools`, `listGrades`, `listAcademicYears`, `listClassGroups`, `listTeachers` | `IT-CATALOGS` / V2-T047 | V2-T047–V2-T051 |
| P0 inscripción | `createEnrollment` | `IT-ENR-CREATE`, `IT-ENR-IDENTITY`, `IT-ENR-CONTEXT` / V2-T052; `IT-ENR-ATOMIC` / V2-T053 | V2-T052–V2-T057 |
| P0 inscripción | `listEnrollments` | `IT-ENR-FILTER` / V2-T058 | V2-T058–V2-T061 |
| P0 contratos | `createTeacherContracts`, `listTeacherContracts`, `listTeachersBySchool` | `IT-CON-MULTI`, `IT-CON-DATES`, `IT-CON-LIST`, `IT-OPENAPI-P0` / V2-T062 | V2-T062–V2-T067 |
| P0 transversal | errores 400/404/409/422 | `IT-PROBLEMS` / V2-T062 | V2-T047–V2-T067 |
| P1 catálogo | `listSubjects` | `IT-LIST-SUBJECTS` / V2-T084; orden `name, code, id` | V2-T084–V2-T087 |
| P1 reportes/historia | `getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`, `getStudentHistory` | `IT-RPT-AGE`, `IT-RPT-SECTOR`, `IT-RPT-TOP`, `IT-HISTORY` | V2-T088–V2-T100 |

`DocumentTypeId` y los schemas SQL no se exponen; `documentType` continúa proyectando `DocumentType.Code`. El frontend conserva 13 consumidores runtime y dos operaciones contract-only (`listSubjects`, `listTeachersBySchool`) hasta que exista consumidor visual.

## Índice de escenarios

| Historia | Escenarios | Slice de evidencia |
| --- | --- | --- |
| US1 | SCN-001–SCN-007 | S09 / V2-T052–V2-T057 |
| US2 | SCN-008–SCN-012 | S10 / V2-T058–V2-T061 |
| US3 | SCN-013–SCN-019 | S11 / V2-T062–V2-T067 |
| US4 | SCN-020–SCN-023 | S14 / V2-T088–V2-T090 |
| US5 | SCN-024–SCN-027 | S15 / V2-T091–V2-T093 |
| US6 | SCN-028–SCN-030 | S16 / V2-T094–V2-T096 |
| US7 | SCN-031–SCN-035 | S13/S17 / V2-T076–V2-T087,V2-T097–V2-T099 |

`SCN-035` permanece backend-only.

## Preguntas de negocio

| ID | Operación | Tablas fuente | Prueba/tareas |
| --- | --- | --- | --- |
| BQ-001/002 | `getAgeDistribution` | Person→Student→Enrollment→ClassGroup | `IT-RPT-AGE`; V2-T088–V2-T090 |
| BQ-003 | `getDistinctTeacherCountsBySector` | Person→Teacher→TeacherContract→School | `IT-RPT-SECTOR`; V2-T091–V2-T093 |
| BQ-004 | `getTopSchoolsByEnrollment` | Enrollment→ClassGroup→School | `IT-RPT-TOP`; V2-T094–V2-T096 |
| BQ-005 | `getStudentHistory` | Person/Student→Enrollment→ClassGroup→TeachingAssignment→Contract/Teacher/Subject | `IT-HISTORY`; V2-T076–V2-T083,V2-T097–V2-T099 |

## Matriz REQ → diseño → tareas → evidencia

| REQ | Diseño/restricción | Tareas | Evidencia |
| --- | --- | --- | --- |
| REQ-001–REQ-005 | Person única, UQ tipo+número, resolución/reuso/conflicto | V2-T027–V2-T031,V2-T052–V2-T057 | `UT-IDENTITY`, `IT-PERSON-COLLATION`, `IT-ENR-CREATE`, `IT-ENR-IDENTITY` (V2-T052) |
| REQ-006 | `Person.BirthDate` y regla de fecha futura | V2-T027–V2-T030,V2-T052–V2-T057 | `UT-IDENTITY`, `IT-ENR-CONTEXT` (V2-T052) |
| REQ-007–REQ-011 | catálogos, ClassGroup, FK compuesto, UQ anual, singleton actual | V2-T020–V2-T026,V2-T032–V2-T037,V2-T046,V2-T052–V2-T057 | `IT-SINGLETON`, `IT-ENR-ANNUAL`, `IT-ENR-CONTEXT`, `IT-ENR-ATOMIC` |
| REQ-012–REQ-017 | query conjunta desde ClassGroup/Enrollment/Person y edad cumplida | V2-T058–V2-T061 | `UT-AGE`, `IT-ENR-FILTER` |
| REQ-018–REQ-026 | rol Teacher, TeacherContract, checks, UQ y Serializable | V2-T038–V2-T043,V2-T062–V2-T067 | `UT-CONTRACT-*`, `IT-CON-*` |
| REQ-027 | puerta P0 antes de P1 | V2-T068–V2-T075 | runner, paridad y walkthrough P0 |
| REQ-028–REQ-032 | reporte de edades derivado | V2-T088–V2-T090 | `IT-RPT-AGE` |
| REQ-033–REQ-035 | contratos Confirmed/intersección y distinct por sector | V2-T038–V2-T043,V2-T091–V2-T093 | `IT-RPT-SECTOR` |
| REQ-036–REQ-037 | máximo derivado desde Enrollment, empates/orden | V2-T094–V2-T096 | `IT-RPT-TOP` |
| REQ-038–REQ-039 | lookup canónico e historia | V2-T097–V2-T099 | `IT-HISTORY` |
| REQ-040–REQ-041 | asignación/horarios e historia relacionada | V2-T076–V2-T083,V2-T097–V2-T099 | `UT-ASSIGNMENT`, `IT-ASSIGNMENT-PERIOD`, `IT-HISTORY` |
| REQ-042–REQ-045 | ProblemDetails, errores, sin auth, datos ficticios | V2-T047–V2-T051,V2-T055,V2-T062,V2-T066,V2-T073 | `IT-PROBLEMS`, `IT-OPENAPI-P0`, `IT-OPENAPI`, revisión de seeds |
| REQ-046 | `catalog.AcademicYear` + singleton actual | V2-T020–V2-T026,V2-T046,V2-T049–V2-T050 | `IT-SINGLETON`, `IT-CATALOGS` |
| REQ-047 | estado persistido, cancelación y vigencia derivada | V2-T038–V2-T043,V2-T062–V2-T067,V2-T091–V2-T093 | `UT-CONTRACT-STATUS/CANCELLATION`, `IT-CON-CANCELLATION` |
| REQ-048 | órdenes explícitos sin paginación; `listSubjects` por `name, code, id` | V2-T049–V2-T051,V2-T059–V2-T060,V2-T064–V2-T065,V2-T084–V2-T100 | pruebas de listas/reportes completas |
| REQ-049 | seeds P0/P1 ficticios | V2-T024,V2-T045,V2-T068–V2-T069,V2-T082–V2-T083,V2-T087 | `IT-SEED-P0/P1` |
| REQ-050 | fuentes especializadas y contrato intacto | V2-T001–V2-T004,V2-T073,V2-T084–V2-T087,V2-T103 | igualdad de árbol/`IT-LIST-SUBJECTS`/`IT-OPENAPI` |
| REQ-051 | presupuesto revisable | V2-T003 y gates V2-T010,V2-T019,V2-T026,V2-T031,V2-T037,V2-T043,V2-T046,V2-T051,V2-T057,V2-T061,V2-T067,V2-T075,V2-T087,V2-T090,V2-T093,V2-T096,V2-T099,V2-T103 | comando additions+deletions por slice |
| REQ-052 | 3NF; única dependencia controlada Enrollment | V2-T032–V2-T037,V2-T069,V2-T083,V2-T102 | `IT-NORMAL-FORMS`, paridad |
| REQ-053 | schemas exactos; `catalog.AcademicYear` | V2-T020–V2-T026,V2-T044–V2-T046,V2-T068–V2-T069 | parcial `IT-CATALOG-SCHEMA-S03`; completo `IT-SCHEMAS-P0` solo en V2-T046; `IT-SQL-SCRIPT*` |
| REQ-054 | Person + roles PK/FK independientes | V2-T027–V2-T031,V2-T052–V2-T057 | `IT-PERSON-DUAL-ROLE` |
| REQ-055 | normalizador Unicode; CHECK vacío/U+0020; CI_AS | V2-T011,V2-T013–V2-T014,V2-T022,V2-T027–V2-T030 | `UT-TEXT-NORMALIZATION`, `IT-TEXT-CHECKS`, `IT-PERSON-COLLATION` |
| REQ-056 | códigos/sector inmutables y `DocumentType` read-only runtime | V2-T020–V2-T026,V2-T045–V2-T046,V2-T068–V2-T069,V2-T077–V2-T079 | parcial `IT-CATALOG-MUTABILITY-S03`; completos `IT-IMMUTABILITY`/`IT-REFERENCE-PERMISSIONS` en V2-T046 |
| REQ-057 | lista auditada exacta y assertions negativas por disponibilidad | V2-T012–V2-T019,V2-T022,V2-T028–V2-T029,V2-T034,V2-T041,V2-T046,V2-T077–V2-T079,V2-T087 | `UT-AUDIT-INTERCEPTOR`, `IT-AUDIT-UTC-P0/P1`, `IT-ROWVERSION-P0/P1` |
| REQ-058 | singleton máximo/existencia/permisos/delete | V2-T020–V2-T026,V2-T044–V2-T046,V2-T068–V2-T069 | parcial `IT-CATALOG-SINGLETON-S03`; completo `IT-SINGLETON` en V2-T046 |
| REQ-059 | cancelación all-or-none | V2-T038–V2-T043,V2-T062–V2-T067 | `UT/IT-CON-CANCELLATION` |
| REQ-060 | período y compatibilidad de TeachingAssignment | V2-T076,V2-T078–V2-T080,V2-T097–V2-T099 | `UT-ASSIGNMENT`, `IT-ASSIGNMENT-PERIOD` |
| REQ-061 | índices OLTP key/include sin `Id` redundante bajo PK clustered | V2-T032,V2-T035,V2-T039,V2-T041,V2-T070,V2-T077,V2-T079 | `IT-INDEXES-P0/P1` |
| REQ-062 | paridad migración/setup, incluidos permisos | V2-T044–V2-T046,V2-T068–V2-T070,V2-T081–V2-T083 | `IT-SQL-SCRIPT/P1` |
| REQ-063 | stacked-to-main, gate por slice, generados aislados | V2-T003 y los 18 gates pre-merge | manifest, diff y forecast |

## Cobertura del modelo

| Hito | Tablas | Gate |
| --- | ---: | --- |
| P0 | 11 | V2-T075: US1–US3, metadatos, paridad, manifest exacto de 37 IDs ejecutado; ≥20 casos solo como sanity check; walkthrough |
| P1 | 14 | solo después de V2-T075; V2-T076–V2-T099 |

La prueba de normalización relacional queda en `data-model.md`: todas las tablas alcanzan BCNF salvo `Enrollment`, que cumple 3NF y conserva `AcademicYearId` únicamente para unicidad anual concurrente. `UQ_ClassGroup_Id_AcademicYear_ForEnrollment` soporta el FK y no expresa identidad de negocio.

## Resultados medibles

| OUT | Evidencia |
| --- | --- |
| OUT-001–OUT-004 | V2-T052–V2-T075: P0, atomicidad, walkthrough y orden |
| OUT-005, OUT-007 | V2-T088–V2-T100: cálculos P1 y empates/sectores |
| OUT-006 | V2-T072: observación local no gating |
| OUT-008 | V2-T008,V2-T024,V2-T068,V2-T082,V2-T101–V2-T103 |
| OUT-009 | V2-T032–V2-T046,V2-T068–V2-T070,V2-T076–V2-T087,V2-T102 |

## Estado

- REQ-001–REQ-063: 63/63 trazados.
- SCN-001–SCN-035 y BQ-001–BQ-005: completos.
- operationIds: 15/15, sin modificación del bundle; los 15 mapeados en runtime y verificados por `IT-OPENAPI` (V2-T100, `015cc6a`).
- Tareas: 103/103 en `production-model-v2.0.0` completas, 0 pendientes. S01–S18 cerrados.
- Existen el modelo completo (14 tablas/5 triggers), la cadena de cuatro migraciones EF (`InitialP0ProductionModel`, `AddP0DatabaseProtections`, `AddP1TeachingModel`, `AddP1DatabaseProtections`) y `database/setup.sql` en paridad verificada (`IT-SQL-SCRIPT`/`IT-SQL-SCRIPT-P1`). Inscripción atómica, consulta de inscritos, contratos docentes multiescuela, catálogo de materias y los cuatro reportes/historia P1 (`getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`, `getStudentHistory`) están entregados end-to-end.
- `./scripts/run-p0-tests.sh`: `P0 GATE PASSED: 37/37`. `./scripts/run-p1-tests.sh`: `P1 GATE PASSED: 13/13`.
- El empaquetado/paquete final fuera del repositorio mencionado en V2-T103 no se ejecutó en este cierre (requiere autorización explícita no otorgada).
