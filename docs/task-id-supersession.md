# Supersession de IDs de tareas

## Decisión vigente

El baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` contiene el task set `school-management-v1.0.0`, con IDs históricos `T001`–`T076`. El plan de modelo de producción publica `production-model-v2.0.0`, con 103 IDs estables `V2-T001`–`V2-T103`.

Los IDs v1 fueron reasignados semánticamente durante el replanteo y **no deben usarse para ejecución, dependencias, evidencia ni handoff actuales**. La coincidencia numérica entre `Tnnn` y `V2-Tnnn` no implica continuidad. Toda referencia vigente debe incluir el prefijo `V2-`.

## Mapa v1 → v2

| ID v1 | Significado en v1 | Estado | Reemplazo v2 |
| --- | --- | --- | --- |
| T001 | verificar repositorio sin implementación | reemplazado | V2-T001 |
| T002 | obtener autorización/commit del baseline OpenAPI | retirado como decisión pendiente; baseline ya existe | V2-T002 verifica árbol y checksum |
| T003 | elegir estrategia de cadena o excepción | retirado como decisión pendiente | V2-T003 fija `stacked-to-main`, gates y cero excepciones |
| T004 | crear guía temprana del evaluador | reemplazado | V2-T004 |
| T005 | crear scaffold de solución/proyectos | reemplazado | V2-T005 |
| T006 | referencias, C#, warnings y editor config | dividido | V2-T006,V2-T008 |
| T007 | instalar dependencias aprobadas | reemplazado | V2-T007 |
| T008 | settings/CORS/connection sin secretos | reemplazado | V2-T008 |
| T009 | validar unidad de scaffold | dividido | V2-T009,V2-T010 |
| T010 | pruebas unitarias P0 agregadas | dividido por política | V2-T011,V2-T012,V2-T027,V2-T038 |
| T011 | crear ocho entidades P0 antiguas | reemplazado por modelo Person/roles de 11 tablas | V2-T021,V2-T028,V2-T033,V2-T040 |
| T012 | normalización/edad/período/errores comunes | dividido | V2-T013–V2-T015,V2-T030,V2-T038 |
| T013 | fixture SQL Server | reemplazado | V2-T016 |
| T014 | metadata del modelo P0 de ocho tablas | reemplazado y ampliado | V2-T018,V2-T020,V2-T027,V2-T032,V2-T039 |
| T015 | DbContext/configuración P0 agregada | dividido por entidad/frontera | V2-T017,V2-T022,V2-T029,V2-T034–V2-T035,V2-T041 |
| T016 | seeds P0 | dividido | V2-T024,V2-T045,V2-T068 |
| T017 | migration `InitialP0` de ocho tablas | reemplazado | V2-T044–V2-T046 |
| T018 | registro de infraestructura | dividido | V2-T017,V2-T048 |
| T019 | host Controllers/OpenAPI/ProblemDetails | reemplazado | V2-T048 |
| T020 | validar modelo P0 agregado | dividido en gates de slices de modelo | V2-T019,V2-T026,V2-T031,V2-T037,V2-T043,V2-T046 |
| T021 | pruebas HTTP de cinco catálogos P0 | reemplazado | V2-T047,V2-T051 |
| T022 | servicios/proyecciones de catálogos | reemplazado | V2-T049 |
| T023 | queries de catálogos | reemplazado | V2-T049 |
| T024 | DTOs/controller de catálogos | reemplazado | V2-T050 |
| T025 | errores de catálogo | integrado | V2-T050,V2-T051 |
| T026 | validar catálogos P0 | reemplazado | V2-T051 |
| T027 | pruebas de alta Enrollment | reemplazado | V2-T052,V2-T057 |
| T028 | pruebas de atomicidad Enrollment | reemplazado | V2-T053,V2-T057 |
| T029 | command/servicio de alta | dividido por modelo y caso de uso | V2-T036,V2-T054 |
| T030 | transacción/traducción SQL Enrollment | reemplazado | V2-T054,V2-T055 |
| T031 | endpoint `createEnrollment` | reemplazado | V2-T056 |
| T032 | validar US1 | reemplazado | V2-T057 |
| T033 | pruebas `listEnrollments` | reemplazado | V2-T058 |
| T034 | query/proyección de inscritos | reemplazado | V2-T059 |
| T035 | endpoint `listEnrollments` | reemplazado | V2-T060 |
| T036 | validar US2 | reemplazado | V2-T061 |
| T037 | pruebas HTTP de contratos | reemplazado | V2-T062 |
| T038 | prueba de concurrencia contractual | reemplazado | V2-T063 |
| T039 | contratos/puertos Core | dividido | V2-T042,V2-T064 |
| T040 | transacción/queries contractuales | dividido | V2-T042,V2-T064 |
| T041 | DTOs/tres endpoints contractuales | dividido | V2-T065,V2-T066 |
| T042 | validar US3 | reemplazado | V2-T067 |
| T043 | `database/setup.sql` de ocho tablas | reemplazado por setup de 11 tablas | V2-T068 |
| T044 | paridad P0 | reemplazado y ampliado a permisos/índices | V2-T069,V2-T070 |
| T045 | pruebas de errores/OpenAPI | dividido | V2-T047,V2-T073 |
| T046 | runner P0 | reemplazado | V2-T071 |
| T047 | actualizar README de ejecución | reemplazado | V2-T072 |
| T048 | completar guía/evidencia del evaluador | dividido | V2-T004,V2-T072,V2-T074 |
| T049 | handoff frontend contractual | integrado al control contractual/cierre | V2-T073,V2-T103 |
| T050 | ejecutar puerta P0 | dividido | V2-T074,V2-T075 |
| T051 | emitir entrega/forecast P0 | reemplazado | V2-T075 |
| T052 | pruebas P1 de asignación/weekdays | reemplazado | V2-T076 |
| T053 | entidades/policy P1 | dividido | V2-T078,V2-T080 |
| T054 | configurar tres tablas P1 | reemplazado | V2-T079 |
| T055 | migration P1 | dividido en generada/manual | V2-T081,V2-T082 |
| T056 | seeds P1 | dividido | V2-T082,V2-T083 |
| T057 | implementar `listSubjects` en una tarea | descompuesto en ruta ejecutable | V2-T084–V2-T087 |
| T058 | pruebas seed/asignación P1 agregadas | dividido | V2-T076,V2-T077,V2-T087 |
| T059 | pruebas reporte de edades | reemplazado | V2-T088 |
| T060 | implementar reporte de edades | reemplazado | V2-T089,V2-T090 |
| T061 | pruebas reporte por sector | reemplazado | V2-T091 |
| T062 | implementar reporte por sector | reemplazado | V2-T092,V2-T093 |
| T063 | pruebas top escuelas | reemplazado | V2-T094 |
| T064 | implementar top escuelas | reemplazado | V2-T095,V2-T096 |
| T065 | pruebas historia estudiantil | reemplazado | V2-T097 |
| T066 | implementar historia estudiantil | reemplazado | V2-T098,V2-T099 |
| T067 | integrar DTOs/controllers P1 | dividido | V2-T086,V2-T100 |
| T068 | runner P1 | reemplazado | V2-T101 |
| T069 | extender setup/paridad P1 | reemplazado | V2-T083 |
| T070 | validar OpenAPI runtime completo | reemplazado | V2-T100,V2-T101 |
| T071 | recorrer BQ-001–BQ-005 | dividido entre gates de capacidad | V2-T090,V2-T093,V2-T096,V2-T099,V2-T101 |
| T072 | cierre de errores/seguridad | reemplazado | V2-T102 |
| T073 | suite/build/format | reemplazado | V2-T101 |
| T074 | documentación/estado final | reemplazado | V2-T103 |
| T075 | revisión 3NF y redundancias | reemplazado | V2-T102 |
| T076 | empaquetado final | reemplazado | V2-T103 |

## Significados nuevos o materialmente expandidos en v2

No se asigna un ID v1 retroactivo a estos trabajos. Son parte del namespace v2:

| IDs v2 | Significado nuevo/expandido |
| --- | --- |
| V2-T011–V2-T019 | normalizador Unicode por capa, auditoría exacta y assertions negativas |
| V2-T020–V2-T026 | `DocumentType`, singleton y permisos runtime explícitos del modelo de 11 tablas |
| V2-T027–V2-T031 | identidad única `Person` y roles `Student`/`Teacher` independientes |
| V2-T032–V2-T046 | modelo 3NF de Enrollment, índices de producción y migrations generada/manual separadas |
| V2-T070,V2-T077,V2-T079 | metadata de índices sin `Id` redundante bajo PK clustered |
| V2-T084–V2-T087 | prueba, query/use case, DTO/controller y gate de `listSubjects` |
| V2-T010,V2-T019,V2-T026,V2-T031,V2-T037,V2-T043,V2-T046,V2-T051,V2-T057,V2-T061,V2-T067,V2-T075,V2-T087,V2-T090,V2-T093,V2-T096,V2-T099,V2-T103 | gate humano pre-merge para S01–S18 |

## Regla de mantenimiento

`production-model-v2.0.0` queda congelado para ejecución. Un cambio futuro que altere el significado de una tarea debe publicar un nuevo task-set versionado y otro mapa de supersession; no puede reciclar `V2-Tnnn` silenciosamente.
