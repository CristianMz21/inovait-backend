# Línea base de la evaluación original

## Propósito y procedencia

Este documento preserva en español profesional la consigna disponible antes de la planificación y muestra cómo se refinó sin perder entregables ni preguntas.

**Fuentes preservadas**:

- `README.md` inicial preservado en el historial anterior al baseline de planificación `1223630ab99bf1bfaa4f5919fccf5ff539379c8e`, que resume alcance, reglas, endpoints esperados y entregables.
- constituciones ratificadas de backend y frontend, que preservan los entregables globales y criterios de evaluación.

No existe en estos repositorios un archivo externo con el texto literal completo de la consigna. Por ello, la sección siguiente es una transcripción estructurada fiel del contenido preservado, no una cita textual atribuida a una fuente ausente.

## Transcripción estructurada

### Tecnología y alcance

- Construir un frontend con Angular y un backend con .NET Core.
- Usar preferentemente SQL Server.
- Resolver la gestión escolar para una única ciudad.
- No se solicita autenticación ni administración general de catálogos.

### Reglas de negocio originales

1. Un docente puede trabajar simultáneamente en más de una escuela.
2. Un estudiante pertenece a una sola escuela dentro del alcance considerado.
3. Todas las escuelas pertenecen a la misma ciudad.
4. Cada escuela es pública o privada.
5. Las escuelas registran estudiantes por grado.
6. Los estudiantes pertenecen a un grupo dentro del grado y el cambio de grupo debe conservarse entre años.

La especificación canónica refina “una sola escuela” como máximo un `Enrollment` por estudiante y AcademicYear, sin traslados intranuales, y preserva años posteriores como historia.

### Capacidades funcionales originales

- Crear un estudiante asociado a escuela, grado, grupo y año.
- Consultar estudiantes aplicando conjuntamente escuela, grado y año.
- Asignar o contratar un docente en varias escuelas y consultar esas relaciones.
- Responder las cinco preguntas municipales indicadas abajo.

Las rutas canónicas `POST /api/enrollments` y `GET /api/enrollments` refinan la intención que el README inicial había parafraseado como rutas `/api/students`. No agregan una capacidad distinta: separan identidad `Student` del hecho histórico `Enrollment` y hacen explícita su atomicidad.

### Preguntas municipales originales

| ID | Pregunta preservada |
| --- | --- |
| BQ-001 | ¿Cuántos estudiantes tienen entre 3 y 7 años? |
| BQ-002 | ¿Cómo se distribuyen en 3–7, 8–12 y mayores de 12 años? |
| BQ-003 | ¿Cuántos docentes distintos trabajan en escuelas públicas y privadas? |
| BQ-004 | ¿Qué escuela o escuelas tienen la mayor cantidad de estudiantes? |
| BQ-005 | ¿Cuál es la historia anual de un estudiante, incluidos grado, grupo, docentes y materias? |

### Entregables globales originales

1. Código fuente frontend.
2. Código fuente backend.
3. Modelo entidad-relación.
4. Script mínimo de base de datos con tablas y datos necesarios.

### Aspectos evaluados

- Calidad y claridad del diseño de interfaz.
- Estructura de la solución.
- Calidad del modelado de datos.
- Calidad del código y buenas prácticas.
- Ejecución comprensible y reproducible.

## Mapeo a fuentes canónicas

| Elemento original | US / REQ / BQ | `operationId` | Artefacto canónico |
| --- | --- | --- | --- |
| Crear estudiante en contexto escolar | US1; REQ-001–011 | `createEnrollment` | `spec.md`, `data-model.md`, `paths/enrollments.yaml` |
| Consultar por escuela, grado y año | US2; REQ-012–017 | `listEnrollments` | `spec.md`, `paths/enrollments.yaml` |
| Contratar docente en varias escuelas | US3; REQ-018–024 | `createTeacherContracts` | `spec.md`, `paths/teacher-contracts.yaml` |
| Consultar relaciones docentes | US3; REQ-025–026 | `listTeacherContracts`, `listTeachersBySchool` | `paths/teacher-contracts.yaml`, `paths/catalogs.yaml` |
| Catálogos necesarios | US1–US3; REQ-007,018,046,048 | `listSchools`, `listGrades`, `listAcademicYears`, `listClassGroups`, `listTeachers` | `paths/catalogs.yaml` |
| BQ-001 y BQ-002 | US4; REQ-028–032; BQ-001/002 | `getAgeDistribution` | `paths/reports.yaml`, `components/reports.yaml` |
| BQ-003 | US5; REQ-033–035; BQ-003 | `getDistinctTeacherCountsBySector` | `paths/reports.yaml` |
| BQ-004 | US6; REQ-036–037; BQ-004 | `getTopSchoolsByEnrollment` | `paths/reports.yaml` |
| BQ-005 | US7; REQ-038–041; BQ-005 | `getStudentHistory` | `paths/enrollments.yaml`, `listSubjects` como catálogo P1 |
| Fuente backend | REQ-050–051 | 15 operaciones | `plan.md`, `tasks.md`, futuro `src/` |
| Fuente frontend | impacto de REQ-042–050 | consumo de las 15 operaciones | `../inovait-frontend/specs/001-school-enrollment-management/` |
| Modelo ER | REQ-052; OUT-009 | N/A | `docs/entity-relationship-model.md`, `data-model.md` |
| Script mínimo | REQ-045,049,052 | soporta P0 | futuro `database/setup.sql`; tarea previa a puerta P0 |

## Interpretaciones explícitas, no ampliaciones

- P0 es el único compromiso de una jornada; P1 conserva las cinco preguntas como extensión condicional.
- La identidad `Student` y la historia `Enrollment` se separan para no sobrescribir años.
- Cualquier School/Grade/AcademicYear existente es un contexto válido; sin ClassGroup se obtiene vacío y no se crea una tabla de oferta adicional.
- Las cinco preguntas usan datos históricos normalizados, sin agregados almacenados.
