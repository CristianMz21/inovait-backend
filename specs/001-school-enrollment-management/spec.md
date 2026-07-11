# Especificación de feature: Gestión de inscripción escolar y contratación docente

**Feature**: `001-school-enrollment-management`

**Rama**: `feat/production-data-model`

**Creada**: 2026-07-10

**Estado**: Borrador validado para planificación

**Entrada**: descripción del usuario sobre inscripción histórica de estudiantes, contratación docente multiescuela y reportes municipales.

## Alcance y prioridad *(obligatorio)*

**Prioridad**: el compromiso de una jornada abarca únicamente P0: tres recorridos end-to-end, catálogos necesarios, SQL mínimo, pruebas críticas y guía del evaluador. P1 conserva especificación completa como extensión condicional, solo si existe evidencia P0 y tiempo remanente.

**Resultado evaluable comprometido**: una persona operadora puede inscribir estudiantes sin duplicar su identidad, consultar inscripciones mediante el contexto académico obligatorio y registrar contratos independientes de docentes en varias escuelas usando documentación y datos P0 reproducibles. Las cinco preguntas municipales permanecen planificadas como P1 condicional y no forman parte del compromiso de una jornada.

**Orden de entrega**:

1. P0: `US1`, `US2` y `US3` completas y validadas de forma independiente.
2. P1 condicional: `US4` a `US7`, únicamente después de evidencia P0; no compensa defectos ni integra el compromiso diario.

**Fuera de alcance**:

- Autenticación, autorización y recuperación de contraseñas.
- Microservicios, mensajería, event bus, CQRS complejo y event sourcing.
- Auditoría avanzada, importación masiva, notificaciones y gestión de archivos.
- Matrícula financiera, cobros y facturación.
- Infraestructura cloud, Kubernetes y CI/CD.
- `Generic Repository` y otras abstracciones no justificadas por el alcance.
- Interfaces administrativas no relacionadas con los recorridos y reportes definidos.
- Traslados entre escuelas dentro del mismo año académico.
- Creación o mantenimiento general de catálogos de escuelas, años, grados, grupos, docentes, materias o estados contractuales.
- Flujos de eliminación destructiva de estudiantes, inscripciones, contratos o asignaciones históricas.

## Clarifications

### Session 2026-07-10

- Q: ¿Cómo se resuelven identidad, pertenencia escolar e inscripción actual? → A: Se normaliza el documento; una coincidencia de identidad reutiliza `Student`, una discrepancia produce `409`, existe un solo `Enrollment` por año y la inscripción actual se deriva del `AcademicYear` actual.
- Q: ¿Cómo se interpretan períodos, superposiciones y estado de `TeacherContract`? → A: Los extremos son inclusivos, fin nulo significa período abierto, toda superposición del mismo docente y escuela produce `409`, la creación multiescuela es atómica y el estado persistido no reemplaza la validez temporal.
- Q: ¿Qué población y resultados deben usar los reportes municipales? → A: Las edades son años cumplidos a `asOfDate`, menores de 3 se excluyen, docentes se cuentan de forma distinta por sector, los empates de escuelas se devuelven completos y cinco preguntas conservan trazabilidad sobre cuatro capacidades.
- Q: ¿Qué reglas observables rigen asignaciones, listas y errores HTTP? → A: Se permiten múltiples `TeachingAssignment` por grupo, cada una usa contrato, grupo, materia y uno o más días compatibles; las listas acotadas no se paginan, se ordenan determinísticamente y distinguen `400`, `404`, `409` y `422` por causa.
- Q: ¿Qué gobernanza debe respetar la planificación posterior? → A: La feature mantiene una fuente canónica y artefactos especializados trazables, unidades revisables de hasta 400 líneas, modelo relacional en 3NF sin agregados almacenados injustificados y datos ficticios que cubren los casos críticos.
- Q: ¿Cómo se lleva el modelo a producción sin duplicar identidad ni fingir garantías SQL? → A: `catalog.AcademicYear` es autoritativo; `Person` concentra identidad y admite roles `Student`/`Teacher` simultáneos; el texto se canoniza en aplicación y se protege con collation/checks; auditoría, concurrencia, singleton, inmutabilidad e índices se definen por frontera real.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia de usuario 1 - Inscribir un estudiante (Prioridad: P0)

Una persona operadora ingresa tipo y número de documento, nombres, apellidos, fecha de nacimiento, escuela, año académico, grado y grupo. El sistema canoniza el texto, crea una `Person` y su rol `Student` o reutiliza una identidad coincidente según las reglas definidas, y registra la inscripción como una sola operación de negocio.

**Motivo de prioridad**: es el origen de la historia académica y de todos los conteos de estudiantes.

**Prueba independiente**: con catálogos ficticios existentes, enviar una inscripción válida y comprobar que se puede recuperar exactamente un `Student` y un `Enrollment` relacionados; repetir con la misma identidad en un año posterior y comprobar que se reutiliza `Student` sin perder la inscripción anterior.

**Escenarios de aceptación**:

1. **SCN-001 — Alta completa**: **Given** que el documento no identifica a ningún `Student` y la combinación académica es válida, **When** se solicita la inscripción, **Then** se crean un `Student` y un `Enrollment` relacionados.
2. **SCN-002 — Identidad existente coincidente**: **Given** una `Person` cuyo tipo/número canónico coincide y cuyos nombres, apellidos y nacimiento son equivalentes, **When** se solicita su inscripción para un año posterior sin inscripción previa, **Then** se reutilizan `Person`/`Student` y solo se crea el nuevo `Enrollment`.
3. **SCN-003 — Identidad en conflicto**: **Given** un tipo/número canónico existente cuyos nombres, apellidos o nacimiento difieren, **When** se solicita la inscripción, **Then** se devuelve `409 ProblemDetails` y no se crea ni modifica ningún registro.
4. **SCN-004 — Segunda inscripción anual**: **Given** un `Student` ya inscrito en el año solicitado, **When** se intenta inscribirlo en cualquier escuela, grado o grupo de ese mismo año, **Then** se informa un conflicto y se conserva la inscripción original.
5. **SCN-005 — Fecha de nacimiento futura**: **Given** una fecha de nacimiento posterior a la fecha actual, **When** se solicita la inscripción, **Then** se rechaza la operación sin persistencia parcial.
6. **SCN-006 — Referencia o combinación inválida**: **Given** una referencia inexistente o un grupo que no pertenece a la escuela, grado y año indicados, **When** se solicita la inscripción, **Then** se devuelve un error identificable y no se crea ningún dato.
7. **SCN-007 — Atomicidad**: **Given** que la creación del `Enrollment` no puede completarse, **When** la solicitud había requerido crear también el `Student`, **Then** ninguno de los dos queda registrado.

### Historia de usuario 2 - Consultar estudiantes inscritos (Prioridad: P0)

Una persona operadora consulta estudiantes usando conjuntamente escuela, grado y año académico. Cada resultado permite reconocer al estudiante y su contexto académico, con edad calculada para una fecha de referencia.

**Motivo de prioridad**: demuestra que la inscripción quedó disponible para una consulta municipal precisa y evita conteos ambiguos.

**Prueba independiente**: preparar inscripciones ficticias en distintos contextos, consultar una combinación concreta y verificar que solo aparezcan las inscripciones coincidentes con todos los campos solicitados y con la edad correcta.

**Escenarios de aceptación**:

1. **SCN-008 — Consulta con coincidencias**: **Given** inscripciones en varias escuelas, grados y años, **When** se informa una combinación válida de escuela, grado y año, **Then** solo se devuelven las inscripciones que cumplen simultáneamente los tres filtros.
2. **SCN-009 — Resultado vacío**: **Given** una combinación académica válida sin estudiantes inscritos, **When** se consulta, **Then** se devuelve una colección vacía y no un error.
3. **SCN-010 — Año inexistente**: **Given** un identificador de `AcademicYear` inexistente, **When** se consulta, **Then** se informa que el año no existe.
4. **SCN-011 — Filtros inválidos**: **Given** que falta un filtro obligatorio o un identificador/formato no cumple el contrato HTTP, **When** se consulta, **Then** se rechaza la solicitud con los campos afectados.
5. **SCN-012 — Contexto válido sin grupos**: **Given** una combinación existente de escuela, grado y año que aún no tiene `ClassGroup`, **When** se consulta, **Then** se devuelve una colección vacía.

### Historia de usuario 3 - Contratar un docente en varias escuelas (Prioridad: P0)

Una persona operadora selecciona un `Teacher` precargado, una o más escuelas, fecha de inicio y fecha de fin opcional. El sistema registra un `TeacherContract` independiente por escuela, conserva un estado contractual separado de la validez de sus fechas y permite consultar posteriormente los contratos persistidos.

**Motivo de prioridad**: cubre la relación multiescuela requerida sin fusionar ni sobrescribir contratos históricos.

**Prueba independiente**: seleccionar un docente y dos escuelas ficticias, registrar los contratos y comprobar mediante una consulta posterior que existen dos registros independientes con sus respectivas escuelas.

**Escenarios de aceptación**:

1. **SCN-013 — Contratos multiescuela**: **Given** un `Teacher` y dos `School` existentes sin períodos incompatibles, **When** se confirma la contratación, **Then** se crea un `TeacherContract` independiente para cada escuela.
2. **SCN-014 — Contrato abierto**: **Given** una contratación sin fecha de fin, **When** se registra, **Then** su intervalo queda abierto sin límite final, independientemente del estado contractual persistido.
3. **SCN-015 — Rango inválido**: **Given** una fecha de fin anterior a la fecha de inicio, **When** se solicita la contratación, **Then** se rechaza toda la solicitud.
4. **SCN-016 — Referencia inexistente**: **Given** un docente o una escuela inexistente, **When** se solicita la contratación, **Then** no se crea ningún contrato de la solicitud.
5. **SCN-017 — Duplicado o superposición**: **Given** un contrato existente para el mismo docente y escuela cuyo intervalo se superpone total o parcialmente, **When** se solicita otro contrato superpuesto, **Then** se informa un conflicto y no se crea ningún contrato de la solicitud.
6. **SCN-018 — Superposición entre escuelas distintas**: **Given** contratos del mismo docente para escuelas distintas, **When** sus períodos coinciden, **Then** ambos son válidos y permanecen independientes.
7. **SCN-019 — Consulta posterior**: **Given** contratos persistidos, **When** se consultan para el docente, **Then** se devuelven todos con escuela, fechas y estado en orden determinista.

### Historia de usuario 4 - Consultar conteos por rangos de edad (Prioridad: P1)

Una persona analista solicita para un año académico el conteo de estudiantes de 3 a 7 años y la distribución en los rangos de 3 a 7, de 8 a 12 y mayores de 12. Esta única capacidad responde las preguntas de negocio 1 y 2.

**Motivo de prioridad**: aporta dos respuestas municipales sobre la misma población y regla de cálculo, sin duplicar capacidades.

**Prueba independiente**: usar inscripciones ficticias con edades exactas de 2, 3, 7, 8, 12 y 13 años para una fecha de referencia y comprobar los tres rangos y el conteo específico de 3 a 7.

**Escenarios de aceptación**:

1. **SCN-020 — Preguntas 1 y 2 en una respuesta**: **Given** un año con estudiantes inscritos, **When** se solicita la distribución, **Then** el resultado incluye el conteo de 3 a 7 y los conteos de los tres rangos definidos.
2. **SCN-021 — Límites inclusivos**: **Given** estudiantes de 3, 7, 8, 12 y 13 años, **When** se calcula la distribución, **Then** 3 y 7 pertenecen al primer rango, 8 y 12 al segundo y 13 al tercero.
3. **SCN-022 — Fecha de referencia**: **Given** una `asOfDate` informada, **When** se calcula la edad, **Then** se usan años cumplidos en esa fecha; si se omite, se usa la fecha actual.
4. **SCN-023 — Contexto sin inscripciones**: **Given** un contexto académico válido sin inscripciones, **When** se solicita el reporte, **Then** todos los conteos son cero.

### Historia de usuario 5 - Contar docentes distintos por sector (Prioridad: P1)

Una persona analista consulta cuántos docentes distintos tienen contratos pertinentes por fecha y estado válido en escuelas públicas y privadas para una fecha o período.

**Motivo de prioridad**: responde la pregunta de negocio 3 sin contar contratos repetidos como docentes adicionales.

**Prueba independiente**: preparar un docente con varios contratos públicos, otro con contratos en ambos sectores y un tercero fuera del período; verificar el conteo distinto por sector.

**Escenarios de aceptación**:

1. **SCN-024 — Conteo actual**: **Given** que no se informa período, **When** se consulta, **Then** se cuentan contratos cuyo intervalo contiene la fecha actual y cuyo estado pertenece al conjunto válido para reportes.
2. **SCN-025 — Conteo por período**: **Given** un rango válido, **When** se consulta, **Then** se consideran los contratos con estado válido cuyo intervalo tiene al menos un día de intersección inclusiva con el período.
3. **SCN-026 — Distinción por docente y sector**: **Given** varios contratos del mismo docente en escuelas del mismo sector, **When** se cuenta, **Then** el docente aporta una sola unidad a ese sector.
4. **SCN-027 — Docente en ambos sectores**: **Given** un docente con contratos pertinentes en escuelas públicas y privadas, **When** se cuenta, **Then** aporta una unidad a cada sector.

### Historia de usuario 6 - Identificar las escuelas con más estudiantes (Prioridad: P1)

Una persona analista solicita la escuela o las escuelas con mayor cantidad de estudiantes inscritos en un año académico.

**Motivo de prioridad**: responde la pregunta de negocio 4 y hace visibles todos los empates.

**Prueba independiente**: preparar dos escuelas empatadas en el máximo y una con menor cantidad; verificar que se devuelvan ambas líderes y siempre en el mismo orden.

**Escenarios de aceptación**:

1. **SCN-028 — Máximo anual**: **Given** inscripciones de varias escuelas en el año solicitado, **When** se consulta, **Then** se devuelve cada escuela cuya cantidad coincide con el máximo anual.
2. **SCN-029 — Empate**: **Given** dos o más escuelas empatadas en el máximo, **When** se consulta, **Then** se devuelven todas ordenadas por nombre y luego por identificador.
3. **SCN-030 — Año sin inscripciones**: **Given** un año existente sin inscripciones, **When** se consulta, **Then** se devuelve una colección vacía.

### Historia de usuario 7 - Consultar historia académica y docente (Prioridad: P1)

Una persona operadora identifica un estudiante por tipo y número de documento y consulta sus inscripciones por año, grado, grupo y escuela, junto con todos los docentes y materias que atendieron el grupo en cada año.

**Motivo de prioridad**: responde la pregunta de negocio 5 y demuestra que la historia no fue sobrescrita.

**Prueba independiente**: preparar dos años de inscripción y varias asignaciones docentes para uno de ellos; verificar que ambos años aparezcan y que el año con múltiples docentes o materias no pierda ninguna relación.

**Escenarios de aceptación**:

1. **SCN-031 — Historia multianual**: **Given** un estudiante con inscripciones en varios años, **When** se consulta su historia, **Then** se devuelve cada año con escuela, grado y grupo sin sobrescribir registros anteriores.
2. **SCN-032 — Múltiples docentes y materias**: **Given** varias `TeachingAssignment` pertinentes para un grupo y año, **When** se consulta la historia, **Then** se incluyen todos los docentes y materias relacionados con esa inscripción.
3. **SCN-033 — Año sin asignaciones**: **Given** una inscripción sin asignaciones docentes asociadas, **When** se consulta la historia, **Then** el año se devuelve con una colección vacía de docentes y materias.
4. **SCN-034 — Estudiante inexistente**: **Given** un documento que no identifica a ningún estudiante, **When** se consulta la historia, **Then** se informa que el estudiante no existe.
5. **SCN-035 — Compatibilidad temporal de asignación (solo backend)**: **Given** una asignación cuya relación temporal no es compatible con el `AcademicYear` del grupo, **When** el backend intenta asociarla mediante seed o caso de uso interno P1, **Then** se rechaza como combinación semántica inválida. No existe una acción frontend ni un endpoint administrativo para crear asignaciones.

### Casos límite

- Dos documentos con igual número pero distinto tipo representan identidades diferentes; la unicidad usa conjuntamente tipo y número.
- Para comparar identidad, los textos requeridos se convierten a NFC, se recortan y colapsan whitespace interno. La comparación no distingue mayúsculas, pero sí acentos; no elimina puntuación ni diacríticos. El tipo se resuelve mediante `DocumentType.Code` y el nacimiento debe coincidir exactamente.
- Una misma `Person` puede tener simultáneamente roles `Student` y `Teacher`; crear un rol nunca duplica documento, nombres ni nacimiento.
- Una persona cuyo cumpleaños coincide con `asOfDate` ya cumplió el año correspondiente; una fecha de referencia anterior al nacimiento es inválida.
- Una inscripción del mismo estudiante en otra escuela durante el mismo año se rechaza, aunque pretenda representar un traslado.
- Un año posterior puede contener otra escuela, grado o grupo sin alterar años anteriores.
- Los intervalos contractuales incluyen sus fechas de inicio y fin; tocar el mismo día cuenta como superposición.
- Un contrato sin fecha de fin se superpone con todo contrato posterior del mismo docente y escuela desde su fecha de inicio.
- La selección repetida de una misma escuela en una solicitud de contratación se considera duplicada y se rechaza.
- Una combinación académica válida sin grupos o inscripciones produce una colección vacía, no una ausencia del recurso de catálogo.
- Estudiantes menores de 3 años no pertenecen a ninguno de los tres rangos solicitados y no alteran sus conteos.
- Las escuelas empatadas en el máximo se devuelven todas; no se elige una de forma arbitraria.
- Un grupo puede tener varias asignaciones con distintos docentes, materias o días de semana.
- Una `TeachingAssignment` cuya escuela difiera de la escuela de su `TeacherContract` es inválida.
- Una asignación debe tener al menos un día de semana y ser temporalmente compatible con el `AcademicYear` de `ClassGroup`; la regla exacta de compatibilidad se definirá en planificación sin alterar este resultado observable.

## Requisitos *(obligatorio)*

### Requisitos funcionales

#### Inscripción e identidad — P0 / US1

- **REQ-001**: El sistema DEBE identificar de forma única a `Person` por `DocumentType` y número de documento canónico, y reutilizar esa identidad para el rol `Student`.
- **REQ-002**: El sistema DEBE recibir nombres, apellidos y fecha de nacimiento como datos de identidad necesarios para crear o verificar un `Student`.
- **REQ-003**: El sistema DEBE crear un `Student` nuevo y su `Enrollment` de manera atómica; ambos se confirman o ninguno persiste.
- **REQ-004**: Cuando ya exista el tipo/número canónico y nombres, apellidos y fecha de nacimiento coincidan según las reglas de identidad, el sistema DEBE reutilizar `Person`/`Student` y crear únicamente el `Enrollment` solicitado.
- **REQ-005**: Cuando ya exista el tipo/número canónico y algún dato de identidad suministrado no coincida, el sistema DEBE devolver `409 ProblemDetails` sin modificar la identidad existente.
- **REQ-006**: El sistema DEBE rechazar una fecha de nacimiento futura.
- **REQ-007**: El sistema DEBE comprobar la existencia de `School`, `AcademicYear`, `Grade` y `ClassGroup` antes de crear la inscripción.
- **REQ-008**: El sistema DEBE aceptar la inscripción solo cuando `ClassGroup` corresponda conjuntamente a la escuela, grado y año solicitados.
- **REQ-009**: El sistema DEBE permitir como máximo un `Enrollment` por `Student` y `AcademicYear`, sin admitir traslados en el mismo año.
- **REQ-010**: El sistema DEBE permitir una inscripción en un año posterior, incluso en otra escuela, preservando todas las inscripciones anteriores.
- **REQ-011**: El sistema DEBE derivar la inscripción actual únicamente de `catalog.AcademicConfiguration.CurrentAcademicYearId`; `Student` NO DEBE mantener un campo mutable de escuela o inscripción actual y las restantes inscripciones DEBEN seguir disponibles como historia.

#### Consulta de inscripciones — P0 / US2

- **REQ-012**: La consulta de inscritos DEBE exigir conjuntamente `School`, `Grade` y `AcademicYear`; ninguno de los tres filtros puede omitirse.
- **REQ-013**: Cada resultado DEBE incluir identidad documental, nombre completo, fecha de nacimiento, edad calculada, escuela, grado, grupo y año académico.
- **REQ-014**: La edad DEBE expresarse en años cumplidos a `asOfDate`; si no se informa, DEBE usarse la fecha actual tanto en la consulta de inscritos como en el reporte de edades.
- **REQ-015**: Una combinación académica válida sin grupos o sin inscripciones DEBE devolver una colección vacía.
- **REQ-016**: Una referencia de filtro inexistente DEBE producir `404`; cualquier combinación de `School`, `Grade` y `AcademicYear` existentes forma un contexto consultable y, si no posee `ClassGroup` o inscripciones, DEBE producir `200 []`.
- **REQ-017**: La consulta DEBE devolver resultados sin paginación para el conjunto acotado de evaluación y con orden determinista por apellidos, nombres, tipo de documento, número de documento e identificador de inscripción.

#### Contratación docente — P0 / US3

- **REQ-018**: La contratación DEBE aceptar únicamente un `Teacher` precargado y una o más `School` existentes.
- **REQ-019**: Una solicitud válida DEBE crear un `TeacherContract` independiente por cada escuela seleccionada, registrando como mínimo escuela, fecha de inicio, fecha de fin opcional y estado.
- **REQ-020**: La fecha de fin, cuando exista, DEBE ser igual o posterior a la fecha de inicio.
- **REQ-021**: Un contrato sin fecha de fin DEBE representar un intervalo abierto sin límite final; esta condición temporal NO DEBE determinar por sí sola su estado contractual.
- **REQ-022**: El sistema DEBE rechazar escuelas repetidas, contratos duplicados y cualquier superposición inclusiva de períodos para el mismo docente y escuela.
- **REQ-023**: El sistema DEBE permitir períodos superpuestos del mismo docente cuando correspondan a escuelas diferentes.
- **REQ-024**: Una solicitud multiescuela DEBE ser atómica: si alguna selección es inválida o conflictiva, no se crea ninguno de sus contratos.
- **REQ-025**: Los contratos persistidos DEBEN poder consultarse posteriormente por docente e incluir escuela, fecha de inicio, fecha de fin y estado.
- **REQ-026**: La consulta de contratos DEBE ordenar ascendentemente por fecha de inicio, nombre de escuela e identificador de contrato.

#### Reportes municipales — P1 / US4 a US7

- **REQ-027**: Las capacidades P1 NO DEBEN considerarse listas para entrega hasta que las pruebas independientes de `US1`, `US2` y `US3` hayan sido validadas.
- **REQ-028**: El sistema DEBE ofrecer cuatro capacidades de reporte para cinco preguntas: una capacidad compartida de distribución por edad para las preguntas 1 y 2, conteo docente por sector para la pregunta 3, máximo de inscripciones por escuela para la pregunta 4 e historia académica-docente para la pregunta 5.
- **REQ-029**: La capacidad de edad DEBE exigir `AcademicYear`; PUEDE aceptar `School` y `Grade` como filtros opcionales acumulativos y DEBE rechazar referencias inexistentes. Toda combinación de referencias existentes es consultable y produce conteos cero cuando no tiene grupos o inscripciones.
- **REQ-030**: Los conteos por edad DEBEN considerar únicamente estudiantes con `Enrollment` en el contexto académico solicitado y calcular su edad mediante `asOfDate`, con fecha actual como valor predeterminado.
- **REQ-031**: La misma respuesta DEBE incluir el conteo de 3 a 7 años inclusive, que responde la pregunta 1, y la distribución de 3 a 7, 8 a 12 y mayores de 12, que responde la pregunta 2.
- **REQ-032**: Los estudiantes menores de 3 años DEBEN excluirse de los tres rangos y un contexto válido sin inscripciones DEBE devolver cero en todos los conteos.
- **REQ-033**: El conteo docente por sector DEBE usar la fecha actual cuando no se informe período; para un período explícito DEBE exigir fecha inicial y final, validar fin igual o posterior a inicio y considerar contratos con al menos un día de intersección inclusiva que además tengan un estado válido para reportes.
- **REQ-034**: El conteo docente DEBE contar cada `Teacher` una sola vez por sector, sin importar cuántos contratos pertinentes tenga en ese sector.
- **REQ-035**: Un docente con contratos pertinentes en escuelas públicas y privadas DEBE contarse una vez en cada sector.
- **REQ-036**: El reporte de escuelas con más estudiantes DEBE exigir un `AcademicYear`, contar sus `Enrollment` por `School` y devolver todas las escuelas empatadas en el máximo.
- **REQ-037**: Las escuelas empatadas DEBEN ordenarse por nombre y luego por identificador; un año válido sin inscripciones DEBE devolver una colección vacía.
- **REQ-038**: La historia de un estudiante DEBE localizarlo por tipo y número de documento canónicos y devolver sus inscripciones por inicio de año académico descendente y luego identificador de inscripción ascendente, con escuela, grado y grupo.
- **REQ-039**: Para cada inscripción histórica, el sistema DEBE incluir todos los docentes y materias que sirvieron a su grupo en ese año; la ausencia de asignaciones DEBE representarse como una colección vacía.
- **REQ-040**: Una `TeachingAssignment` DEBE relacionar un `TeacherContract`, un `Subject` y un `ClassGroup`; la escuela del contrato DEBE coincidir con la del grupo y su relación temporal DEBE ser compatible con el `AcademicYear` del grupo conforme a una regla definida en planificación.
- **REQ-041**: El sistema DEBE permitir múltiples `TeachingAssignment` para un mismo grupo y representar uno o más días de semana por asignación mediante `ClassSchedule`, sin eliminar docentes o materias múltiples del historial.

#### Reglas transversales

- **REQ-042**: Los errores observables DEBEN usar una estructura `ProblemDetails` estable, identificar campos afectados cuando corresponda y no exponer trazas, consultas, secretos ni detalles internos.
- **REQ-043**: El sistema DEBE distinguir datos mal formados, referencias inexistentes y conflictos de integridad para que la persona usuaria pueda corregir la causa.
- **REQ-044**: El MVP NO DEBE exigir autenticación ni autorización para los recorridos incluidos.
- **REQ-045**: Todos los datos iniciales, ejemplos, pruebas y demostraciones DEBEN ser ficticios y no contener datos personales reales ni secretos.
- **REQ-046**: `AcademicYear` DEBE ser un catálogo administrado y precargado, referenciado por identificador en las solicitudes; NO DEBE interpretarse como un entero libre, y como máximo un año PUEDE estar designado como actual.
- **REQ-047**: El estado de `TeacherContract` DEBE persistirse y permanecer separado de la validez temporal calculada por sus fechas. La planificación DEBE definir un conjunto mínimo y estable de estados y cuáles son válidos para reportes; ningún estado desactualizado PUEDE sustituir la evaluación del intervalo.
- **REQ-048**: Toda lista de búsqueda o reporte DEBE usar un orden determinista y no paginarse únicamente por el volumen acotado del conjunto de evaluación. Las colecciones anidadas de historia DEBEN ordenarse de forma estable por materia, docente e identificador de asignación.
- **REQ-049**: El dataset comprometido P0 DEBE contener únicamente catálogos e historia ficticia necesarios para `US1`–`US3`, incluidos varios años, grupos, docentes, escuelas de ambos sectores y contratos abiertos/cerrados. La extensión P1 condicional DEBE agregar límites de edad, empate de líderes y múltiples docentes/materias sin inflar la puerta P0.
- **REQ-050**: La planificación DEBE preservar una única fuente de verdad por decisión y producir artefactos especializados trazables para especificación funcional, investigación, arquitectura, modelo de datos, OpenAPI, modelo ER, UX, trazabilidad y tareas, sin duplicar reglas canónicas.
- **REQ-051**: Cada unidad revisable posterior DEBE respetar un presupuesto máximo de 400 líneas humanas modificadas; scaffold, lockfiles y migraciones generadas DEBEN aislarse para revisión explícita y no mezclarse con lógica humana.
- **REQ-052**: El modelo relacional canónico DEBE satisfacer como mínimo 3NF. NO DEBE almacenar agregados de reportes ni introducir estructuras físicas desnormalizadas sin justificación y evidencia documentadas, y ninguna excepción PUEDE reemplazar la fuente normalizada. En particular, la planificación DEBE evitar duplicar `School`, `Grade` o `AcademicYear` en `Enrollment` cuando `ClassGroup` ya los determine, salvo que documente una estrategia de integridad que lo justifique sin crear una segunda fuente de verdad.

#### Modelo de producción — P0/P1 transversal

- **REQ-053**: Las tablas DEBEN pertenecer exactamente a los schemas aprobados: catálogos en `catalog`, identidad/roles en `people`, hechos académicos en `academic` y contratos en `staff`; `AcademicYear` DEBE ser `catalog.AcademicYear`.
- **REQ-054**: `people.Person` DEBE almacenar una sola identidad; `Student.PersonId` y `Teacher.PersonId` DEBEN ser PK+FK independientes y una persona DEBE poder mantener ambos roles sin duplicar datos.
- **REQ-055**: El texto Unicode requerido DEBE persistirse en NFC, sin whitespace exterior y con whitespace interno colapsado; la aplicación DEBE tratar tabs, saltos de línea y whitespace Unicode antes de persistir. SQL DEBE conservar `LEN(TRIM([Column])) > 0`, que rechaza vacío y valores formados solo por espacios ordinarios en escritura directa sin atribuirle cobertura de todo whitespace Unicode, y aplicar comparación CI_AS sensible a acentos, sin columnas duplicadas de comparación.
- **REQ-056**: `DocumentType` DEBE exponer código estable y único y el principal runtime DEBE poder leerlo pero tener DENY explícito de INSERT, UPDATE y DELETE; los códigos de `School`, `AcademicYear`, `Grade` y `Subject`, y `School.Sector`, DEBEN ser inmutables después del alta tanto en EF como ante escrituras SQL directas.
- **REQ-057**: Exactamente `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`, `Subject` y `TeachingAssignment` DEBEN registrar creación/actualización UTC, validar su orden y detectar actualizaciones concurrentes; `Enrollment` y `ClassSchedule` DEBEN registrar solo creación. `DocumentType`, `Student` y `AcademicConfiguration` NO DEBEN recibir auditoría genérica ni rowversion.
- **REQ-058**: El año actual DEBE residir en el singleton `catalog.AcademicConfiguration(Id=1)`; la solución DEBE distinguir la garantía declarativa de máximo una fila de la garantía operativa de existencia mediante seed, permisos restringidos, prevención de delete y detección de ausencia.
- **REQ-059**: Un `TeacherContract` `Cancelled` DEBE registrar timestamp UTC, razón no vacía y fecha efectiva dentro del período; un contrato `Confirmed` DEBE mantener los tres campos nulos.
- **REQ-060**: `TeachingAssignment` DEBE registrar `StartDate` y fin opcional con rango válido; la aplicación DEBE comprobar en transacción que contrato y grupo comparten escuela y que el período de asignación queda contenido en contrato y año.
- **REQ-061**: El esquema DEBE declarar índices OLTP nombrados y no redundantes para inscripciones por grupo/año, contratos por docente/escuela y fecha, y asignaciones por grupo/contrato y fecha, incluyendo las columnas de proyección requeridas.
- **REQ-062**: La migración EF y `database/setup.sql` DEBEN mantener paridad de schemas, tablas, tipos, collation, defaults, constraints, índices/includes, triggers, seeds y permisos relevantes sobre bases limpias.
- **REQ-063**: La implementación DEBE entregarse como slices `stacked-to-main` de hasta 400 líneas humanas, con scaffold y migraciones generadas aislados; P1 DEBE continuar detrás de la puerta P0.

### Datos e historia *(obligatorio cuando corresponda)*

- **Entidades**: `School`, `AcademicYear`, `AcademicConfiguration`, `Grade`, `Subject`, `DocumentType`, `Person`, `Student`, `Teacher`, `ClassGroup`, `Enrollment`, `TeacherContract`, `TeachingAssignment` y `ClassSchedule`.
- **Historia**: `Enrollment` conserva la trayectoria anual del estudiante y `TeacherContract` conserva los períodos por escuela. Una nueva inscripción anual, contrato o asignación agrega información; no sobrescribe años o períodos anteriores.
- **Integridad**: `Person` es única por tipo y número de documento; los roles no duplican identidad; existe un máximo de un `Enrollment` por estudiante y año; cada `Enrollment` referencia una combinación válida; cada `TeacherContract` pertenece a un docente y una escuela; cada `TeachingAssignment` usa la misma escuela que el contrato docente. Las eliminaciones que destruyan historia deben rechazarse.
- **Fechas**: nacimiento, inicio y fin contractual, `asOfDate` y límites de períodos son fechas de negocio sin hora. Los extremos de intervalos son inclusivos.
- **Normalización relacional**: el modelo canónico debe alcanzar al menos 3NF y derivar reportes desde registros históricos. Cualquier estructura física redundante excepcional requiere justificación de integridad y evidencia en planificación y no puede convertirse en una segunda fuente de verdad.

### Validación y errores *(obligatorio)*

La clasificación se basa en la causa observable: `400` cubre un contrato HTTP que no puede validarse por ausencia, tipo o formato; `404`, una referencia que no existe; `409`, una colisión con identidad o historia ya persistida; y `422`, una solicitud bien formada cuyas referencias existen pero forman una combinación semántica inválida o incumplen una regla de negocio no conflictiva.

| Regla | Frontera responsable | Resultado HTTP observable |
| --- | --- | --- |
| Campos requeridos ausentes, formatos o tipos inválidos y fechas ilegibles | API | `400 ProblemDetails` con campos afectados |
| Fecha de nacimiento futura, rango de fechas inválido u otra regla de negocio no conflictiva sobre datos bien formados | Dominio | `422 ProblemDetails` con regla incumplida |
| `Student`, `Teacher`, `School`, `AcademicYear`, `Grade`, `ClassGroup` o `Subject` inexistente | Aplicación | `404 ProblemDetails` con referencia afectada |
| `ClassGroup` suministrado al alta que no pertenece a School/Grade/AcademicYear, o asignación P1 con escuela/tiempo incompatible | Aplicación/dominio | `422 ProblemDetails` sin persistencia parcial |
| Identidad existente con datos diferentes | Aplicación | `409 ProblemDetails` sin modificar `Student` |
| Escuela repetida en la solicitud, segunda inscripción anual, contrato duplicado o período superpuesto | Aplicación/base de datos | `409 ProblemDetails` con integridad preservada |
| Combinación válida sin grupos, inscripciones, contratos o asignaciones | Aplicación | `200` con colección vacía o conteos en cero, según corresponda |
| School, Grade y AcademicYear existentes sin una tabla de oferta que los relacione | Aplicación | Contexto válido; `200 []` o conteos cero si no existen grupos |
| Conflicto en una selección de contratación multiescuela | Aplicación | Error correspondiente y cero contratos creados por la solicitud |

### Contrato entre repositorios *(obligatorio si existe impacto UI)*

- **Backend canónico**: las operaciones y esquemas que representen los recorridos y reportes definidos deberán quedar descritos posteriormente en el contrato OpenAPI canónico, sin inventar variantes en el frontend.
- **Nombres/versiones compartidos**: los conceptos `School`, `Student`, `AcademicYear`, `Grade`, `ClassGroup`, `Enrollment`, `Teacher`, `TeacherContract`, `Subject`, `TeachingAssignment` y `ClassSchedule`, sus campos y reglas observables deben conservar nomenclatura técnica en inglés.
- **Impacto en `inovait-frontend`**: deberá permitir completar los tres recorridos P0, mostrar errores asociados a campos, anunciar resultados y errores de forma comprensible, conservar navegación por teclado y adaptarse a tamaños de pantalla razonables. La implementación visual permanece fuera de este repositorio.

### Entidades clave *(incluir si la feature maneja datos)*

- **`School`**: escuela de la única ciudad del sistema; pertenece al sector Public o Private y agrupa contextos académicos, inscripciones y contratos.
- **`DocumentType`**: referencia estable de tipos documentales mediante código único y estado de disponibilidad.
- **`Person`**: identidad única por tipo y número de documento, con nombres, apellidos y nacimiento, compartida por roles.
- **`Student`**: rol uno-a-uno de `Person`; conserva múltiples inscripciones anuales.
- **`AcademicYear`**: catálogo `catalog.AcademicYear` administrado y precargado, con código estable y límites.
- **`AcademicConfiguration`**: singleton que referencia el único año actual configurado.
- **`Grade`**: nivel académico al que pertenece una inscripción y en el que se organizan grupos.
- **`ClassGroup`**: grupo válido para una escuela, grado y año académico; recibe estudiantes y asignaciones docentes.
- **`Enrollment`**: registro histórico que vincula un estudiante con su `ClassGroup`; el grupo determina escuela, año y grado, solo existe uno por estudiante y año y cualquier redundancia relacional requiere justificación de integridad en planificación.
- **`Teacher`**: rol uno-a-uno de `Person`, compatible con el rol `Student`, que puede mantener contratos simultáneos con varias escuelas.
- **`TeacherContract`**: período contractual independiente entre docente y escuela, con inicio, fin opcional, estado persistido y cancelación consistente.
- **`Subject`**: materia que puede impartirse a un grupo mediante una asignación docente.
- **`TeachingAssignment`**: servicio temporal de un docente contratado para un grupo, materia y contexto académico compatible con la escuela y período del contrato.
- **`ClassSchedule`**: uno o más días de semana asociados a una asignación docente.

## Trazabilidad *(obligatorio)*

Las cinco preguntas municipales permanecen como objetivos de trazabilidad independientes, aunque `BQ-001` y `BQ-002` compartan una sola capacidad y regla de cálculo.

| Business Question ID | Pregunta | Capacidad / evidencia principal |
| --- | --- | --- |
| BQ-001 | ¿Cuántos estudiantes tienen de 3 a 7 años? | US4 / REQ-031 / SCN-020, SCN-021 |
| BQ-002 | ¿Cómo se distribuyen entre 3-7, 8-12 y mayores de 12? | US4 / REQ-031, REQ-032 / SCN-020 a SCN-023 |
| BQ-003 | ¿Cuántos docentes distintos trabajan por sector? | US5 / REQ-033 a REQ-035 / SCN-024 a SCN-027 |
| BQ-004 | ¿Qué escuelas tienen más estudiantes en un año? | US6 / REQ-036, REQ-037 / SCN-028 a SCN-030 |
| BQ-005 | ¿Qué docentes y materias integran la historia del estudiante? | US7 / REQ-038 a REQ-041 / SCN-031 a SCN-035 |

| Requirement ID | Story/Scenario ID | Evidencia de aceptación | Prioridad |
| --- | --- | --- | --- |
| REQ-001, REQ-002 | US1 / SCN-001, SCN-002, SCN-003 | Identidad creada, reutilizada o rechazada según documento y datos | P0 |
| REQ-003 | US1 / SCN-001, SCN-007 | `Student` y `Enrollment` persisten juntos o ninguno | P0 |
| REQ-004, REQ-005 | US1 / SCN-002, SCN-003 | Reutilización sin duplicado y conflicto sin modificación | P0 |
| REQ-006 | US1 / SCN-005 | Fecha futura rechazada | P0 |
| REQ-007, REQ-008 | US1 / SCN-006 | Referencias y combinación académica validadas | P0 |
| REQ-009, REQ-010, REQ-011 | US1 / SCN-004, SCN-002; US7 / SCN-031 | Un registro anual, años posteriores e historia preservada | P0 |
| REQ-012 | US2 / SCN-008, SCN-011 | Los tres filtros son obligatorios y conjuntos | P0 |
| REQ-013, REQ-014 | US2 / SCN-008; US4 / SCN-022 | Resultado completo y edad consistente con `asOfDate` | P0 |
| REQ-015, REQ-016 | US2 / SCN-009, SCN-010, SCN-011, SCN-012 | Vacío válido distinguible de error | P0 |
| REQ-017 | US2 / SCN-008 | Resultado completo, no paginado y determinista | P0 |
| REQ-018, REQ-019 | US3 / SCN-013, SCN-016 | Referencias existentes y un contrato por escuela | P0 |
| REQ-020, REQ-021 | US3 / SCN-014, SCN-015 | Rango válido y contrato abierto coherente | P0 |
| REQ-022, REQ-023 | US3 / SCN-017, SCN-018 | Superposición rechazada por escuela y permitida entre escuelas | P0 |
| REQ-024 | US3 / SCN-015, SCN-016, SCN-017 | Solicitud multiescuela sin resultados parciales | P0 |
| REQ-025, REQ-026 | US3 / SCN-019 | Contratos persistidos recuperables en orden estable | P0 |
| REQ-027 | US1-US3 / pruebas independientes | P1 condicionado a la validación completa de P0 | P0 |
| REQ-028 | US4-US7 / SCN-020, SCN-024, SCN-028, SCN-031 | Cinco preguntas cubiertas por cuatro capacidades | P1 |
| REQ-029, REQ-030 | US4 / SCN-020, SCN-022, SCN-023 | Población y fecha de cálculo delimitadas | P1 |
| REQ-031, REQ-032 | US4 / SCN-020, SCN-021, SCN-023 | Preguntas 1 y 2, límites y ceros verificados | P1 |
| REQ-033 | US5 / SCN-024, SCN-025 | Conteo actual o por período inclusivo | P1 |
| REQ-034, REQ-035 | US5 / SCN-026, SCN-027 | Docentes distintos por sector y doble pertenencia válida | P1 |
| REQ-036, REQ-037 | US6 / SCN-028, SCN-029, SCN-030 | Máximo anual, empates y orden determinista | P1 |
| REQ-038, REQ-039 | US7 / SCN-031, SCN-032, SCN-033, SCN-034 | Historia anual completa con cero o múltiples asignaciones | P1 |
| REQ-040, REQ-041 | US7 / SCN-032, SCN-035 | Asignaciones compatibles y múltiples servicios preservados | P1 |
| REQ-042, REQ-043 | US1-US7 / todos los escenarios de error | Errores estables, accionables y sin detalles internos | P0/P1 |
| REQ-044 | US1-US7 / pruebas independientes | Todos los recorridos son accesibles sin autenticación | P0/P1 |
| REQ-045 | US1-US7 / datos de prueba | Cero datos personales reales o secretos | P0/P1 |
| REQ-046 | US1, US2, US4, US6 / SCN-002, SCN-010, SCN-020, SCN-028 | Año referenciado desde catálogo y designación actual inequívoca | P0/P1 |
| REQ-047 | US3, US5 / SCN-014, SCN-019, SCN-024, SCN-025 | Estado persistido y validez temporal evaluados por separado | P0/P1 |
| REQ-048 | US2, US3, US6, US7 / SCN-008, SCN-019, SCN-029, SCN-031, SCN-032 | Listas completas y repetibles para el conjunto acotado | P0/P1 |
| REQ-049 | US1-US7 / pruebas independientes | Datos ficticios demuestran límites, empates, sectores e historia | P0/P1 |
| REQ-050, REQ-051 | US1-US7 / revisión de planificación | Artefactos especializados trazables y unidades revisables acotadas | P0/P1 |
| REQ-052 | US1-US7 / revisión del modelo y reportes | 3NF mínima y ausencia de redundancia o agregados injustificados | P0/P1 |
| REQ-053 | US1-US7 / revisión de metadatos | Cuatro schemas exactos y `catalog.AcademicYear` | P0/P1 |
| REQ-054, REQ-055 | US1, US3, US7 / SCN-001–003,013,031–034 | Persona única, roles duales, texto canónico y collation | P0/P1 |
| REQ-056 | Catálogos / revisión de escritura | Códigos/sector inmutables en EF y SQL | P0/P1 |
| REQ-057 | Escrituras mutables / conflicto concurrente | Auditoría exacta, UpdatedAt y RowVersion | P0/P1 |
| REQ-058 | US1, US2, US4, US6 | Singleton existente y protegido para año actual | P0/P1 |
| REQ-059 | US3, US5 / SCN-014,019,024,025 | Cancelación all-or-none y fecha válida | P0/P1 |
| REQ-060 | US7 / SCN-032,035 | Período de asignación y compatibilidad transaccional | P1 |
| REQ-061, REQ-062 | US1-US7 / pruebas SQL | Índices OLTP y paridad migración/setup | P0/P1 |
| REQ-063 | US1-US7 / revisión de entrega | `stacked-to-main`, ≤400 líneas humanas y generados aislados | P0/P1 |

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **OUT-001**: El 100 % de las pruebas independientes y escenarios de aceptación de `US1`, `US2` y `US3` produce el resultado esperado antes de iniciar la validación P1.
- **OUT-002**: En el 100 % de los intentos inválidos de alta de estudiante, inscripción o contratación multiescuela, no queda persistencia parcial ni se altera historia válida.
- **OUT-003**: Una persona evaluadora puede completar cada recorrido P0 válido en menos de 3 minutos usando datos ficticios y sin asistencia técnica.
- **OUT-004**: El 100 % de las consultas repetidas sobre los mismos datos y filtros devuelve el mismo contenido y orden.
- **OUT-005**: Para un conjunto ficticio con límites de edad, contratos multisector, empates e historia multiasignación, los cinco resultados de negocio coinciden exactamente con un cálculo manual de referencia.
- **OUT-006**: Durante la demostración local se registran, solo como observación informativa, tiempos de una ejecución calentada de cada consulta P0. No existe umbral de latencia ni puerta de entrega; una degradación evidente se documenta para seguimiento sin desplazar corrección funcional.
- **OUT-007**: El 100 % de los empates por escuela devuelve todas las escuelas líderes y el 100 % de los docentes con contratos pertinentes en ambos sectores aparece una vez en cada sector.
- **OUT-008**: La revisión de datos iniciales, ejemplos y evidencias encuentra cero datos personales reales, credenciales o secretos.
- **OUT-009**: La revisión del modelo de datos confirma 3NF como mínimo, cero agregados de reporte almacenados y cero redundancias sin justificación y evidencia documentadas.

## Supuestos

- Todas las escuelas pertenecen a una única ciudad y cada `School` tiene un sector Public o Private.
- “Una escuela” significa ausencia de membresías simultáneas: existe un solo `Enrollment` por estudiante y año. Los traslados en el mismo año están fuera de alcance y se rechazan.
- Un año académico posterior puede usar otra escuela, grado o grupo y siempre agrega historia.
- La identidad existente se resuelve por tipo y número de documento. Coincidencia completa reutiliza `Student`; diferencias en nombres, apellidos o fecha de nacimiento generan conflicto.
- La inscripción actual es la del `AcademicYear` referenciado por `AcademicConfiguration(Id=1)`; cualquier otra sigue disponible como historia.
- `AcademicYear` es un catálogo administrado y precargado en schema `catalog`, no un entero libre; el singleton selecciona exactamente uno en operación normal.
- `ClassGroup` pertenece a una combinación única de escuela, grado y año académico.
- Un `TeacherContract` sin fecha de fin tiene un intervalo abierto. Los intervalos incluyen ambos extremos.
- Toda superposición para el mismo docente y escuela se rechaza porque el MVP no contempla justificaciones; las superposiciones entre escuelas son válidas.
- La solicitud para varias escuelas se valida como una unidad y crea todos sus contratos o ninguno.
- El estado contractual se persiste pero no equivale a vigencia temporal; su conjunto mínimo estable y los estados válidos para reportes se decidirán en planificación y su mantenimiento no forma parte de esta feature.
- Varias personas docentes pueden servir un mismo grupo mediante distintas `TeachingAssignment`, y cada asignación puede relacionar materia y días de semana mediante `ClassSchedule`.
- La consulta de inscritos requiere escuela, grado y año. El reporte de edad requiere año y admite escuela y grado como filtros opcionales acumulativos.
- El reporte docente sin período usa la fecha actual; con período usa la intersección inclusiva entre el intervalo solicitado y el contrato.
- La edad se calcula en años cumplidos para `asOfDate`; su valor predeterminado es la fecha actual.
- No habrá paginación por tratarse de un conjunto acotado de evaluación; todos los listados tendrán orden determinista.
- Todos los empates en el máximo de estudiantes serán devueltos.
- P0 precargará escuelas, años, grados, grupos, docentes y estados estrictamente necesarios para los tres recorridos. Subjects, TeachingAssignments, ClassSchedules, límites de reporte, empates y multiplicidad docente se agregan solo en la extensión P1.
- La feature seguirá siendo una sola unidad principal; la planificación posterior distribuirá responsabilidades entre artefactos especializados trazables sin replicar fuentes de verdad y respetará unidades revisables de hasta 400 líneas modificadas.
- El modelo relacional canónico alcanzará al menos 3NF y no almacenará agregados de reporte; cualquier estructura física redundante excepcional requerirá justificación basada en integridad y evidencia sin reemplazar la fuente normalizada.
- La estrategia de entrega es `stacked-to-main`; cada slice depende de los ya integrados en `main`, y scaffold/migraciones generadas se revisan separados de lógica humana.

## Dependencias y riesgos

- El dataset P0 intencionalmente mínimo no demuestra P1; si se autoriza la extensión, sus seeds y pruebas deben agregarse después de la puerta sin reabrir el compromiso diario.
- La regla de un único `Enrollment` anual simplifica el MVP, pero excluye explícitamente traslados intranuales y deberá revisarse si el alcance futuro los incorpora.
- La ausencia de paginación es adecuada solo para el conjunto acotado de evaluación; un volumen municipal real requeriría una decisión posterior.
- El estado de `TeacherContract` necesita un conjunto mínimo coherente en planificación; los reportes deberán combinar las reglas de estado aprobadas con la intersección de fechas y no confiar en un estado desactualizado.
- La historia docente depende de la correcta asociación temporal y escolar de `TeachingAssignment`; planificación deberá definir la compatibilidad exacta con `AcademicYear` sin contradecir el rechazo observable de combinaciones inválidas.
- Un artefacto posterior que repita reglas canónicas en vez de referenciarlas aumentaría el riesgo de divergencia; la trazabilidad debe indicar responsabilidad y fuente de verdad.

## Seguridad y privacidad

- No se usarán secretos ni datos personales reales.
- El MVP no incluye autenticación ni autorización por decisión explícita de alcance.
- Los errores no expondrán trazas, consultas, configuración ni detalles internos.
