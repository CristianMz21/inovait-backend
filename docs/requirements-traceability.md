# Trazabilidad de requisitos

## Fuentes de verdad

| Aspecto | Fuente canónica |
| --- | --- |
| comportamiento y prioridad | `specs/001-school-enrollment-management/spec.md` |
| decisiones técnicas y modelo | `plan.md`, `research.md`, `data-model.md` |
| HTTP | `contracts/openapi.yaml` y referencias externas |
| ER | `docs/entity-relationship-model.md` |
| pruebas previstas | `docs/testing-strategy.md` |
| consigna preservada | `docs/assessment-baseline.md` |
| secuencia ejecutable | `specs/001-school-enrollment-management/tasks.md` |

Esta matriz relaciona, sin redefinir reglas, los 52 REQ, 35 SCN, 5 BQ, 9 OUT, 15 `operationId` y las 76 tareas vigentes. P0 comprende T001–T051; P1 condicional T052–T071; cierre T072–T076.

## Consumidores del repositorio frontend

La planificación frontend remediada define **13 consumidores runtime** y conserva las 15 operaciones en la verificación contractual. `listSubjects` y `listTeachersBySchool` son contract-only: no tienen método cliente ni llamada visual hasta que exista un consumidor real.

| `operationId` | Estado frontend | Superficie | Tareas frontend vigentes |
| --- | --- | --- | --- |
| `listSchools` | runtime | FE-S01/02/03/05 | T011–T012 |
| `listGrades` | runtime | FE-S01/02/05 | T011–T012 |
| `listAcademicYears` | runtime | FE-S01/02/05 | T011–T012 |
| `listClassGroups` | runtime | FE-S01/02 | T011–T012 |
| `listTeachers` | runtime | FE-S03 | T011–T012 |
| `listSubjects` | contract-only | sin consumidor visual | T008 |
| `listTeachersBySchool` | contract-only | flujo teacher-first sin llamada por escuela | T008 |
| `createEnrollment` | runtime | FE-S01 | T014–T019 |
| `listEnrollments` | runtime | FE-S02 | T020–T024 |
| `createTeacherContracts` | runtime | FE-S03 | T025–T030 |
| `listTeacherContracts` | runtime | FE-S04 | T025–T030 |
| `getAgeDistribution` | runtime P1 | FE-S05 | T036–T040,T047 |
| `getDistinctTeacherCountsBySector` | runtime P1 | FE-S05 | T036–T038,T041–T042,T047 |
| `getTopSchoolsByEnrollment` | runtime P1 | FE-S05 | T036–T038,T043–T044,T047 |
| `getStudentHistory` | runtime P1 | FE-S06 | T036,T045–T047 |

Todas las superficies deben conservar teclado, foco visible, labels, adaptación responsive y anuncio accesible de `ProblemDetails`; la implementación visual pertenece a `inovait-frontend`.

**Estado frontend verificado en planificación**: 49 tareas; P0 T001–T035, P1 condicional T036–T047 y cierre T048–T049; 0 tareas ejecutadas. El contenido de planificación entre repositorios está sincronizado y registra el checksum working-tree-only `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`. El contrato continúa untracked y no reproducible hasta que una autorización explícita permita crear un commit de baseline que contenga los diez YAML y registrar su commit+checksum aprobados.

## Índice exacto de escenarios

| Historia | Escenarios |
| --- | --- |
| US1 | SCN-001, SCN-002, SCN-003, SCN-004, SCN-005, SCN-006, SCN-007 |
| US2 | SCN-008, SCN-009, SCN-010, SCN-011, SCN-012 |
| US3 | SCN-013, SCN-014, SCN-015, SCN-016, SCN-017, SCN-018, SCN-019 |
| US4 | SCN-020, SCN-021, SCN-022, SCN-023 |
| US5 | SCN-024, SCN-025, SCN-026, SCN-027 |
| US6 | SCN-028, SCN-029, SCN-030 |
| US7 | SCN-031, SCN-032, SCN-033, SCN-034, SCN-035 |

`SCN-035` es **backend-only**: valida la creación interna/seed de TeachingAssignment y no corresponde a una acción ni endpoint de escritura frontend.

## Preguntas de negocio

| ID | Historia / escenarios | Operación | Tablas | Prueba |
| --- | --- | --- | --- | --- |
| BQ-001 | US4 / SCN-020,021 | `getAgeDistribution` | Enrollment, ClassGroup, Student | T059–T060; IT-RPT-AGE |
| BQ-002 | US4 / SCN-020–023 | `getAgeDistribution` | Enrollment, ClassGroup, Student | T059–T060; IT-RPT-AGE |
| BQ-003 | US5 / SCN-024–027 | `getDistinctTeacherCountsBySector` | TeacherContract, Teacher, School | T061–T062; IT-RPT-SECTOR |
| BQ-004 | US6 / SCN-028–030 | `getTopSchoolsByEnrollment` | Enrollment, ClassGroup, School | T063–T064; IT-RPT-TOP |
| BQ-005 | US7 / SCN-031–035 | `getStudentHistory` | Student, Enrollment, ClassGroup, TeachingAssignment, ClassSchedule, TeacherContract, Teacher, Subject | T052–T058,T065–T066; IT-HISTORY/ASSIGNMENT |

## Matriz exacta

| REQ | US / SCN | Modelo o restricción | `operationId` | Pantalla | Prueba prevista |
| --- | --- | --- | --- | --- | --- |
| REQ-001 | US1 / SCN-001–003 | UQ Student normalized document | `createEnrollment`, `getStudentHistory` | FE-S01/06 | UT-IDENTITY; IT-ENR-IDENTITY |
| REQ-002 | US1 / SCN-001–003 | Student required identity fields | `createEnrollment` | FE-S01 | UT-IDENTITY; IT-ENR-CREATE/IDENTITY |
| REQ-003 | US1 / SCN-001,007 | Student+Enrollment transaction | `createEnrollment` | FE-S01 | IT-ENR-CREATE; IT-ENR-ATOMIC |
| REQ-004 | US1 / SCN-002 | identity equivalence and reuse | `createEnrollment` | FE-S01 | UT-IDENTITY; IT-ENR-IDENTITY |
| REQ-005 | US1 / SCN-003 | identity conflict, no update | `createEnrollment` | FE-S01 | IT-ENR-IDENTITY; IT-PROBLEMS |
| REQ-006 | US1 / SCN-005 | Student.BirthDate domain rule | `createEnrollment` | FE-S01 | UT-AGE; IT-ENR-CONTEXT |
| REQ-007 | US1 / SCN-006 | FK catalogs and existence | `listSchools`, `listGrades`, `listAcademicYears`, `listClassGroups`, `createEnrollment` | FE-S01 | IT-CATALOGS; IT-ENR-CONTEXT |
| REQ-008 | US1 / SCN-006 | ClassGroup context + composite FK | `listClassGroups`, `createEnrollment` | FE-S01 | IT-ENR-CONTEXT |
| REQ-009 | US1 / SCN-004 | UQ Enrollment student+year | `createEnrollment` | FE-S01 | IT-ENR-ANNUAL |
| REQ-010 | US1 / SCN-002; US7 / SCN-031 | append-only Enrollment | `createEnrollment`, `getStudentHistory` | FE-S01/06 | IT-ENR-ANNUAL; IT-HISTORY |
| REQ-011 | US1/US7 / SCN-031 | AcademicYear.IsCurrent; no current Student FK | `getStudentHistory`, `listAcademicYears` | FE-S01/06 | IT-ENR-ANNUAL; IT-HISTORY |
| REQ-012 | US2 / SCN-008,011 | required joint query context | `listEnrollments` | FE-S02 | IT-ENR-FILTER; IT-PROBLEMS |
| REQ-013 | US2 / SCN-008 | Enrollment projection | `listEnrollments` | FE-S02 | IT-ENR-FILTER |
| REQ-014 | US2/US4 / SCN-008,022 | age calculation at asOfDate | `listEnrollments`, `getAgeDistribution` | FE-S02/05 | UT-AGE; IT-ENR-FILTER; IT-RPT-AGE |
| REQ-015 | US2 / SCN-009,012 | empty valid context | `listEnrollments` | FE-S02 | IT-ENR-FILTER |
| REQ-016 | US2 / SCN-010–012 | 404 de referencia; IDs existentes sin grupos ⇒ 200 [] | `listEnrollments` | FE-S02 | T033–T036; IT-ENR-FILTER |
| REQ-017 | US2 / SCN-008 | deterministic Enrollment ordering | `listEnrollments` | FE-S02 | IT-ENR-FILTER |
| REQ-018 | US3 / SCN-013,016 | Teacher/School FK and preloads | `listTeachers`, `listSchools`, `createTeacherContracts` | FE-S03 | IT-CATALOGS; IT-CON-MULTI/DATES |
| REQ-019 | US3 / SCN-013 | one TeacherContract per school | `createTeacherContracts` | FE-S03 | IT-CON-MULTI |
| REQ-020 | US3 / SCN-015 | CK contract end>=start | `createTeacherContracts` | FE-S03 | IT-CON-DATES; IT-PROBLEMS |
| REQ-021 | US3 / SCN-014 | nullable EndDate; status separate | `createTeacherContracts`, `listTeacherContracts` | FE-S03/04 | UT-CONTRACT-STATUS; IT-CON-DATES/LIST |
| REQ-022 | US3 / SCN-017 | UQ exact + serializable overlap | `createTeacherContracts` | FE-S03 | UT-CONTRACT-OVERLAP; IT-CON-OVERLAP |
| REQ-023 | US3 / SCN-018 | overlap key includes SchoolId | `createTeacherContracts` | FE-S03 | UT-CONTRACT-OVERLAP; IT-CON-OVERLAP |
| REQ-024 | US3 / SCN-015–017 | multi-school transaction | `createTeacherContracts` | FE-S03 | IT-CON-MULTI/DATES/OVERLAP |
| REQ-025 | US3 / SCN-019 | TeacherContract projection | `listTeacherContracts`, `listTeachersBySchool` | FE-S04 | IT-CON-LIST |
| REQ-026 | US3 / SCN-019 | start/school/id ordering | `listTeacherContracts` | FE-S04 | IT-CON-LIST |
| REQ-027 | US1–US3 / independent tests | delivery gate, no table | todas P0 antes de reportes | FE-S01–04 | P0 gate in testing-strategy |
| REQ-028 | US4–US7 / SCN-020,024,028,031 | four derived queries | four report/history operations | FE-S05/06 | IT-RPT-AGE/SECTOR/TOP; IT-HISTORY |
| REQ-029 | US4 / SCN-020,022,023 | filtros acumulativos; referencias existentes sin grupos ⇒ ceros | `getAgeDistribution` | FE-S05 | T059–T060; IT-RPT-AGE |
| REQ-030 | US4 / SCN-020,022 | Enrollment population + age | `getAgeDistribution` | FE-S05 | UT-AGE; IT-RPT-AGE |
| REQ-031 | US4 / SCN-020,021 | propiedades fijas age3To7/age8To12/ageOver12 | `getAgeDistribution` | FE-S05 | T059–T060; IT-RPT-AGE |
| REQ-032 | US4 / SCN-021,023 | exclude <3; zero result | `getAgeDistribution` | FE-S05 | UT-AGE; IT-RPT-AGE |
| REQ-033 | US5 / SCN-024,025 | Confirmed + inclusive intersection | `getDistinctTeacherCountsBySector` | FE-S05 | UT-CONTRACT-OVERLAP/STATUS; IT-RPT-SECTOR |
| REQ-034 | US5 / SCN-026 | DISTINCT TeacherId per sector | `getDistinctTeacherCountsBySector` | FE-S05 | IT-RPT-SECTOR |
| REQ-035 | US5 / SCN-027 | grouping independently by sector | `getDistinctTeacherCountsBySector` | FE-S05 | IT-RPT-SECTOR |
| REQ-036 | US6 / SCN-028 | group Enrollment by derived School | `getTopSchoolsByEnrollment` | FE-S05 | IT-RPT-TOP |
| REQ-037 | US6 / SCN-029,030 | all max ties; name/id order | `getTopSchoolsByEnrollment` | FE-S05 | IT-RPT-TOP |
| REQ-038 | US7 / SCN-031,034 | normalized lookup + annual order | `getStudentHistory` | FE-S06 | UT-IDENTITY; IT-HISTORY |
| REQ-039 | US7 / SCN-032,033 | left projection of assignments | `getStudentHistory` | FE-S06 | IT-HISTORY |
| REQ-040 | US7 / SCN-032,035 backend-only | Assignment FK; regla school/time interna | `getStudentHistory` (lectura) | FE-S06 sin acción de alta | T052–T058,T065–T066 |
| REQ-041 | US7 / SCN-032,035 backend-only | TeachingAssignment + ClassSchedule PK/CHECK | `getStudentHistory` | FE-S06 | T052–T058,T065–T066 |
| REQ-042 | US1–US7 / SCN de error | canonical ProblemDetails | todas | FE-S01–06 | IT-PROBLEMS; IT-OPENAPI |
| REQ-043 | US1–US7 / SCN-003,005,006,010,011,015–017,034,035 | HTTP 400/404/409/422 mapping | todas | FE-S01–06 | IT-PROBLEMS |
| REQ-044 | US1–US7 / todos | OpenAPI `security: []` | todas | FE-S01–06 | IT-OPENAPI; walkthrough |
| REQ-045 | US1–US7 / fixtures | seeds/examples ficticios por fase | todas | FE-S01–06 | IT-SEED-P0/IT-SEED-P1; review |
| REQ-046 | US1/2/4/6 / SCN-002,010,020,028 | AcademicYear table + filtered current UX | `listAcademicYears` y operaciones por año | FE-S01/02/05 | IT-CATALOGS; IT-ENR-*; IT-RPT-AGE/TOP |
| REQ-047 | US3/5 / SCN-014,019,024,025 | status CHECK + effective derivation | contract ops; `getDistinctTeacherCountsBySector` | FE-S03–05 | UT-CONTRACT-STATUS; IT-CON-LIST; IT-RPT-SECTOR |
| REQ-048 | US2/3/6/7 / SCN-008,019,029,031,032 | explicit ORDER BY; no paging | todas las listas/reportes | FE-S01–06 | IT-CATALOGS; IT-ENR-FILTER; IT-CON-LIST; IT-RPT-*; IT-HISTORY |
| REQ-049 | US1–US7 / independent tests | 8 tablas/seeds P0; extensión a 11 y dataset especializado P1 | todas, incluido `listSubjects` | FE-S01–06 | T016,T043–T044,T056,T058,T069 |
| REQ-050 | US1–US7 / planning review | artifact ownership table | N/A governance | N/A | plan review; IT-OPENAPI |
| REQ-051 | US1–US7 / review units | modular YAML/docs under 400 lines | N/A governance | N/A | changed-line review |
| REQ-052 | US1–US7 / model review | 3NF; Enrollment composite FK; no aggregates | todas las consultas derivadas | FE-S01–06 | schema review; IT-SQL-SCRIPT/IT-SQL-SCRIPT-P1; IT-RPT-* |

## Tareas vigentes por capacidad

| Capacidad | Tareas | Estado de prioridad |
| --- | --- | --- |
| decisiones pre-apply y guía temprana | T001–T004 | bloqueante antes de scaffold |
| scaffold, 8 tablas y Testcontainers | T005–T020 | P0 |
| cinco catálogos P0 | T021–T026 | P0 |
| US1 alta/atomicidad | T027–T032 | P0 |
| US2 consulta | T033–T036 | P0 |
| US3 contratos | T037–T042 | P0 |
| SQL, runner, README, handoff y gate | T043–T051 | P0 |
| tres tablas/seeds/listSubjects P1 | T052–T058 | P1 condicional |
| US4–US7 | T059–T071 | P1 condicional |
| cierre aplicable | T072–T076 | posterior a P0; P1 solo si fue autorizado |

## Resultados medibles

| OUT | Evidencia planificada |
| --- | --- |
| OUT-001–OUT-004 | T027–T050: tres recorridos, atomicidad, tiempo manual y orden |
| OUT-005 | T059–T071, únicamente P1 condicional |
| OUT-006 | T048: observación local calentada, no gating y sin umbral |
| OUT-007 | T061–T064,T071, únicamente P1 condicional |
| OUT-008 | T008,T016,T043,T072–T076 |
| OUT-009 | T014–T017,T043–T044,T054–T055,T069,T075: 3NF, FK compuesto y cero agregados/redundancias injustificadas |

## Cobertura de operaciones y tablas

Las 15 operaciones aparecen al menos una vez. El backend P0 implementa 10 operationIds runtime (cinco catálogos, create/list enrollments y tres contratos) y P1 agrega `listSubjects`, tres reportes e historia. El frontend consume 9 de las operaciones P0 y 4 de P1: 13 runtime en total; `listTeachersBySchool` y `listSubjects` permanecen contract-only. El modelo completo tiene 11 tablas, pero P0 materializa 8 y P1 agrega 3. `ClassSchedule` solo se expone dentro de historia; no se crea un endpoint de mantenimiento ni uno duplicado para BQ-005.

## Estado de planificación

- REQ-001–REQ-052: 52/52 trazados.
- BQ-001–BQ-005: 5/5 trazadas.
- US1–US7 y SCN-001–SCN-035: presentes.
- OUT-001–OUT-009: 9/9 enlazados; OUT-006 es observación no gating.
- operationIds: 15/15; 10 P0 y 5 P1 condicionales.
- Tareas: 76/76 vigentes; T001–T051 P0, T052–T071 P1, T072–T076 cierre.
- Frontend observado: 13 consumidores runtime + 2 contract-only; 49/49 tareas vigentes, contenido de planificación sincronizado y contrato aún untracked/no reproducible hasta un baseline explícitamente autorizado.
- Estado de implementación: 0 tareas ejecutadas; no existe scaffold, código, pruebas, migraciones ni `database/setup.sql`.
