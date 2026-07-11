<!--
Sync Impact Report
- Cambio de versión: plantilla sin versión → 1.0.0
- Principios modificados: se reemplazaron cinco marcadores genéricos por ocho principios
  específicos del proyecto.
- Secciones añadidas: Alcance y restricciones técnicas; Flujo de planificación y entrega.
- Secciones eliminadas: ninguna sección constituida; solo se retiraron marcadores de plantilla.
- Plantillas actualizadas:
  - ✅ .specify/templates/plan-template.md
  - ✅ .specify/templates/spec-template.md
  - ✅ .specify/templates/tasks-template.md
- Plantillas inspeccionadas sin cambios:
  - ✅ .specify/templates/constitution-template.md
  - ✅ .specify/templates/checklist-template.md
- Guías inspeccionadas sin cambios: README.md, AGENTS.md.
- Comandos de plantilla: no existe .specify/templates/commands/ en esta instalación.
- Seguimientos pendientes: ninguno.
-->

# Constitución de Inovait Backend

## Principios fundamentales

### I. MVP simple y P0 primero

El proyecto DEBE optimizarse para un MVP demostrable en una jornada. P0 comprende únicamente:

1. crear un `Student` y su `Enrollment` en `School`, `Grade`, `Group` y año dentro de una
   única transacción;
2. consultar estudiantes aplicando conjuntamente `school`, `grade` y `year`; y
3. asignar un `Teacher` precargado a más de una escuela mediante contratos independientes y
   consultar los registros persistidos.

P0 DEBE estar completo y validado antes de iniciar P1. P1 contiene el conjunto de cinco reportes
solicitado, que cubre rangos de edad, docentes distintos por sector público o privado, escuela o
escuelas con más estudiantes e historial del estudiante con docentes. Toda capacidad ajena a P0
o P1 DEBE excluirse. La simplicidad tiene prioridad porque la evaluación exige una solución
terminada, comprensible y ejecutable, no amplitud especulativa.

### II. Integridad del modelo e historia preservada

El modelo DEBE representar `Enrollment` y `TeacherContract` como registros históricos, no como
campos sobrescribibles de `Student` o `Teacher`. Un cambio de año, grado, grupo, escuela o
docente DEBE agregar historia sin destruir la anterior. Las relaciones, nulabilidad,
unicidad y cardinalidades DEBEN quedar explícitas en el modelo ER y ser coherentes con las
consultas y reportes.

SQL Server es el motor preferido. La base de datos DEBE imponer claves, claves foráneas,
restricciones de unicidad y comprobaciones que pueda hacer cumplir de forma fiable. Las
eliminaciones sobre registros referenciados por historia DEBEN ser restrictivas; no se admite
el borrado en cascada de historia. Las fechas de negocio DEBEN almacenarse sin hora. Solo los
sellos técnicos opcionales PUEDEN incluir tiempo y, si existen, DEBEN usar UTC.

### III. Validación por frontera y errores HTTP predecibles

Cada regla DEBE validarse en la frontera que realmente pueda imponerla:

- API: formato, tipos, campos requeridos y límites del contrato HTTP;
- aplicación: existencia de referencias, reglas del caso de uso y consistencia entre campos;
- dominio: invariantes de negocio independientes del transporte y la persistencia; y
- base de datos: integridad relacional, unicidad y restricciones concurrentes.

Una regla NO DEBE duplicarse sin necesidad entre capas; cuando varias defensas sean necesarias,
DEBE existir una fuente conceptual única y resultados coherentes. Los errores HTTP DEBEN usar
`ProblemDetails`, con estado y estructura estables. Los errores de validación DEBEN identificar
los campos afectados sin exponer trazas, SQL, secretos ni detalles internos.

### IV. Trazabilidad de extremo a extremo y contrato canónico

Cada necesidad del enunciado DEBE mantener un identificador trazable desde la especificación
hasta escenarios de aceptación, plan, modelo ER, operación OpenAPI, tarea y prueba pertinente.
No se considera terminada una capacidad si el evaluador debe inferir alguno de esos vínculos.

`inovait-backend` e `inovait-frontend` DEBEN permanecer como repositorios independientes. El
contrato OpenAPI del backend es canónico; el frontend DEBE consumirlo sin inventar variantes.
Los nombres compartidos, formatos y decisiones de versión DEBEN documentarse y coordinarse en
ambos repositorios. Todo cambio de contrato DEBE declarar su impacto entre repositorios antes
de implementarse.

### V. Código legible y nomenclatura técnica consistente

Los nombres de proyectos, clases, métodos, variables, objetos de base de datos, endpoints,
campos JSON y demás identificadores técnicos DEBEN escribirse en inglés y conservar el mismo
concepto entre dominio, persistencia y API. El código DEBE favorecer flujos directos, tipos
explícitos, responsabilidades claras y módulos con propósito observable. La documentación de
planificación DEBE escribirse en español profesional neutro. La claridad para un evaluador
tiene prioridad sobre abstracciones reutilizables hipotéticas.

### VI. Pruebas de reglas de negocio, no cobertura artificial

Las pruebas DEBEN concentrarse en reglas con riesgo real: atomicidad de alta de estudiante e
inscripción, filtros conjuntos, preservación histórica, contratos independientes del docente,
restricciones de integridad, reportes y errores de validación. Cada requisito crítico DEBE
tener al menos un escenario verificable y la capa de prueba más económica que lo demuestre.

No se DEBEN crear pruebas triviales solo para elevar un porcentaje ni fijar una meta de cobertura
sin relación con el riesgo. Las pruebas de integración DEBEN cubrir comportamiento dependiente
de SQL Server o del contrato HTTP cuando una prueba unitaria no pueda demostrarlo. Hasta que
exista infraestructura ejecutable, las fases de planificación solo DEBEN definir la estrategia,
no simular resultados.

### VII. Interfaz accesible y adaptable como obligación compartida

Aunque este repositorio sea el backend, toda capacidad con interfaz DEBE incluir criterios
coordinados para navegación por teclado, etiquetas y mensajes comprensibles, contraste,
estados de foco, errores anunciables y adaptación a tamaños de pantalla razonables. El backend
DEBE aportar contratos y errores que permitan al frontend cumplir esos criterios. La revisión
del backend DEBE señalar el impacto UI; la implementación visual corresponde a
`inovait-frontend`.

### VIII. Entrega reproducible, segura y evaluable

Un evaluador DEBE poder configurar la solución, crear la base de datos, cargar datos iniciales,
ejecutar backend y frontend y recorrer P0 siguiendo documentación versionada. La entrega DEBE
incluir fuente de ambos repositorios, modelo ER y un script mínimo ejecutable con solo esquema y
datos necesarios. Los comandos, prerrequisitos, configuración y orden de ejecución DEBEN ser
explícitos y reproducibles desde un entorno limpio.

La revisión DEBE hacer visibles la calidad del diseño UI, la estructura, el modelado, la calidad
del código y las buenas prácticas, porque son criterios explícitos de la evaluación.

No se DEBEN versionar secretos, credenciales ni datos personales reales. Todo dato inicial,
ejemplo, captura o prueba DEBE ser ficticio. La configuración sensible DEBE resolverse mediante
variables de entorno o mecanismos locales documentados sin valores reales.

## Alcance y restricciones técnicas

- La solución evaluada DEBE conservar Angular para el frontend y .NET para el backend. Las
  versiones concretas y dependencias DEBEN decidirse y justificarse durante planificación.
- NO se implementarán autenticación, autorización, microservicios, complejidad CQRS, event
  sourcing, event bus, infraestructura cloud, Kubernetes, CI/CD, `Generic Repository` ni
  administración no solicitada.
- Escuelas, grados y docentes PUEDEN ser catálogos precargados. Los datos iniciales DEBEN ser
  ficticios y suficientes para demostrar P0 y, después, P1.
- La creación de `Student` y `Enrollment` DEBE ser atómica: ambos registros se confirman o
  ninguno persiste.
- Los contratos de un mismo `Teacher` con escuelas diferentes DEBEN persistir como registros
  independientes y consultables.
- Los reportes DEBEN derivarse del modelo histórico; no DEBEN depender de valores resumidos que
  destruyan trazabilidad.
- Una preferencia tecnológica no equivale a una dependencia autorizada. Las versiones y
  bibliotecas DEBEN justificarse en planificación antes de incorporarse.

## Flujo de planificación y entrega

1. La especificación DEBE clasificar cada historia como P0 o P1, declarar exclusiones y asignar
   identificadores de trazabilidad.
2. El plan DEBE superar el control constitucional antes de investigación y volver a evaluarlo
   después del diseño. Toda excepción DEBE registrar necesidad, riesgo y alternativa simple
   rechazada.
3. El diseño DEBE alinear modelo ER, restricciones SQL Server, validaciones, `ProblemDetails`,
   OpenAPI y estrategia de pruebas con los mismos requisitos.
4. Las tareas DEBEN ordenarse por P0 antes de P1 y conservar vínculos con requisitos e historias.
5. La validación de P0 DEBE demostrar los tres recorridos completos y la reproducibilidad para
   el evaluador antes de autorizar reportes P1.

La fase actual es exclusivamente de constitución y planificación. En esta fase NO se DEBEN crear
código de producción, carpetas fuente, scaffold de .NET, migraciones, dependencias, commits ni
pushes. Cada fase posterior DEBE respetar sus autorizaciones explícitas; avanzar en el flujo no
autoriza por sí mismo la implementación.

## Gobernanza

Esta constitución prevalece sobre planes, plantillas y prácticas locales incompatibles. Toda
propuesta, especificación, plan, lista de tareas y revisión DEBE verificar su cumplimiento. Las
desviaciones NO se aceptan por conveniencia: requieren justificación escrita, impacto sobre el
plazo de una jornada y evidencia de que una alternativa más simple no satisface P0.

Las enmiendas DEBEN actualizar esta constitución, incluir un informe de impacto de sincronización
y propagar cambios a las plantillas dependientes. La versión sigue SemVer: MAJOR para retirar o
redefinir garantías de gobernanza incompatibles; MINOR para agregar principios o ampliar
materialmente obligaciones; PATCH para aclaraciones sin cambio normativo. La fecha de última
enmienda DEBE actualizarse con cada cambio aprobado.

Antes de considerar una entrega terminada, la revisión DEBE comprobar: P0 completo, trazabilidad,
integridad histórica, contrato OpenAPI, pruebas de reglas críticas, ausencia de datos reales o
secretos y ejecución reproducible. P1 NO puede compensar un incumplimiento de P0.

**Versión**: 1.0.0 | **Ratificada**: 2026-07-10 | **Última enmienda**: 2026-07-10
