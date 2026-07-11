---
description: "Plantilla de tareas de implementación"
---

# Tareas: [FEATURE NAME]

**Entrada**: documentos de diseño en `/specs/[###-feature-name]/`

**Prerrequisitos**: `plan.md` y `spec.md`; usar `research.md`, `data-model.md`,
`quickstart.md` y `contracts/` cuando existan.

**Pruebas**: son obligatorias para reglas de negocio críticas identificadas por la
especificación o el plan. No se agregan tareas para alcanzar cobertura artificial.

**Orden**: completar y validar P0 antes de iniciar tareas P1.

## Formato: `[ID] [P?] [Story] [Requirement] Descripción`

- **[P]**: puede ejecutarse en paralelo porque afecta archivos distintos y no tiene dependencia.
- **[Story]**: historia trazada, por ejemplo `[US1]`.
- **[Requirement]**: requisito trazado, por ejemplo `[REQ-001]`.
- Cada tarea DEBE incluir rutas concretas.

## Fase 1: Preparación mínima

**Propósito**: crear solo la infraestructura aprobada por el plan.

- [ ] T001 Crear la estructura mínima indicada en `plan.md`
- [ ] T002 Configurar runtime y dependencias expresamente aprobadas
- [ ] T003 [P] Configurar formato y análisis estático sin supresiones
- [ ] T004 Documentar configuración local sin secretos

**Control**: no incluir autenticación, microservicios, CQRS complejo, event sourcing, event bus,
cloud, Kubernetes, CI/CD, `Generic Repository` ni administración ajena al alcance.

## Fase 2: Fundamentos de P0

**Propósito**: habilitar integridad, contrato y errores requeridos por los recorridos P0.

- [ ] T005 Definir esquema y migraciones con historia y eliminaciones restrictivas
- [ ] T006 [P] Definir contrato OpenAPI canónico y decisiones compartidas de nombres/versión
- [ ] T007 Implementar fronteras de validación según `plan.md`
- [ ] T008 Implementar respuestas `ProblemDetails` estables
- [ ] T009 Preparar datos iniciales exclusivamente ficticios

**Control**: ningún recorrido P0 comienza hasta completar sus fundamentos necesarios.

## Fase 3: Historia P0 - [Title]

**Objetivo**: [resultado evaluable]

**Prueba independiente**: [cómo demostrar el recorrido de extremo a extremo]

### Pruebas de reglas críticas

- [ ] T010 [P] [US1] [REQ-001] Agregar prueba de [regla] en [ruta]
- [ ] T011 [P] [US1] [REQ-002] Agregar prueba de integración de [recorrido] en [ruta]

### Implementación

- [ ] T012 [US1] [REQ-001] Implementar [elemento] en [ruta]
- [ ] T013 [US1] [REQ-002] Implementar [operación] en [ruta]
- [ ] T014 [US1] [REQ-003] Integrar validación y `ProblemDetails` en [ruta]
- [ ] T015 [US1] [REQ-004] Actualizar OpenAPI y coordinación con `inovait-frontend`

**Control**: la historia funciona y se prueba de forma independiente.

## Fase 4: Validación completa de P0

- [ ] T016 Demostrar alta transaccional de `Student` y `Enrollment`
- [ ] T017 Demostrar consulta conjunta por `school`, `grade` y `year`
- [ ] T018 Demostrar contratos independientes de un `Teacher` con varias escuelas
- [ ] T019 Validar modelo ER, script mínimo y setup desde un entorno limpio
- [ ] T020 Verificar trazabilidad completa de todos los requisitos P0

**PUERTA P0**: todas las tareas anteriores DEBEN completarse antes de crear o ejecutar P1.

## Fase 5: Historia P1 - [Report Title]

**Objetivo**: [reporte solicitado derivado del modelo histórico]

- [ ] T021 [P] [USX] [REQ-XXX] Agregar prueba de la regla del reporte en [ruta]
- [ ] T022 [USX] [REQ-XXX] Implementar el reporte en [ruta]
- [ ] T023 [USX] [REQ-XXX] Exponer y documentar la operación OpenAPI en [ruta]

## Fase final: Entrega evaluable

- [ ] T024 Validar `quickstart.md` desde un entorno limpio
- [ ] T025 Validar que el script mínimo crea esquema y datos ficticios necesarios
- [ ] T026 Validar ausencia de secretos y datos personales reales
- [ ] T027 Verificar fuente backend, fuente frontend, modelo ER y documentación de ejecución
- [ ] T028 Coordinar criterios de accesibilidad y diseño adaptable con `inovait-frontend`

## Dependencias y orden

- Preparación mínima → fundamentos P0 → historias P0 → puerta P0 → reportes P1 → entrega.
- Modelos y restricciones preceden los servicios que dependen de ellos.
- Servicios preceden las operaciones HTTP correspondientes.
- Las pruebas se ubican en la capa más económica que demuestre la regla.
- Tareas `[P]` solo son paralelas cuando no comparten archivos ni dependencias.

## Notas

- No conservar tareas de ejemplo en el archivo generado.
- No crear tareas vagas, sin ruta o sin vínculo a un requisito.
- No agregar abstracciones o infraestructura no justificadas por P0 o P1.
- Los commits pertenecen a la fase autorizada de implementación, nunca a planificación.
