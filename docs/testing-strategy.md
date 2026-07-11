# Estrategia de pruebas planificada

## Decisión

Las reglas puras se prueban con xUnit; collation, `rowversion`, defaults, checks, triggers, permisos, índices y transacciones se prueban contra SQL Server 2022 real mediante Testcontainers. EF InMemory y SQLite no constituyen evidencia relacional.

## Capas futuras

| Capa | Herramienta | Alcance |
| --- | --- | --- |
| Unit | xUnit v3 `3.2.2` | texto, edad, estados, períodos, transiciones |
| Integration | `WebApplicationFactory` + EF/SQL Server | HTTP, transacciones, modelo físico y concurrencia |
| Schema parity | consultas `sys.*` | migración frente a `database/setup.sql` |
| Contract | parser OpenAPI | 15 operationIds y respuestas compatibles |
| Walkthrough | quickstart | recorridos P0 y BQ P1 con datos ficticios |

Testcontainers es la única puerta relacional automatizada. Una instancia externa exclusiva de test es fallback, no segunda ejecución obligatoria.

## Unitarias

| ID | Casos mínimos |
| --- | --- |
| `UT-TEXT-NORMALIZATION` | NFC de secuencias equivalentes; whitespace Unicode exterior; tabs/newlines/whitespace interno→un espacio; required whitespace-only→rechazo; diacríticos y puntuación preservados; idempotencia |
| `UT-AUDIT-INTERCEPTOR` | con dobles disponibles en S02: reloj determinista, alta/modificación, preservación de creación y actualización genérica; no acredita entidades/tablas P0 o P1 |
| `UT-IDENTITY` | igualdad CI_AS conceptual por tipo+número canónico; mismo número con tipo distinto es identidad nueva; conflicto de nombres/nacimiento, límite de fecha y rol dual sin duplicación |
| `UT-AGE` | límites 3/7/8/12/13, cumpleaños y fecha anterior al nacimiento |
| `UT-CONTRACT-OVERLAP` | intervalos separados, toque inclusivo, contenido y fin abierto |
| `UT-CONTRACT-CANCELLATION` | Confirmed sin datos; Cancelled con tres datos; razón whitespace; fecha fuera del contrato |
| `UT-CONTRACT-STATUS` | Upcoming/Effective/Expired y Cancelled dominante |
| `UT-ASSIGNMENT` (P1) | rango local; misma escuela; contención en año/contrato/cancelación; fin nulo acotado por año |

## Integración relacional P0

| ID | Evidencia |
| --- | --- |
| `IT-CATALOG-SCHEMA-S03` | cinco tablas catalog: keys/checks y seed exacto, idempotente, vacío/parcial, concurrente y rollback-safe; no afirma el modelo P0 completo |
| `IT-CATALOG-MUTABILITY-S03` | evidencia catalog-only: save behavior y triggers impiden Code/Sector incluso solo por case; Name permanece mutable |
| `IT-CATALOG-SINGLETON-S03` | evidencia catalog-only: PK+CHECK, seed, startup positivo/fail-fast, anti-delete y permisos runtime sobre cinco tablas |
| `IT-SCHEMAS-P0` | producida y ejecutada en S07 después de V2-T045: 11 tablas en schemas exactos; nunca `academic.AcademicYear` |
| `IT-PERSON-COLLATION` | mismo tipo+número con variación de case colisiona; acento distinto no colisiona; UQ e índice de nombres conservan nombre, orden de keys e INCLUDE exactos |
| `IT-TEXT-CHECKS` | SQL directo con `''` o solo U+0020 falla por `LEN(TRIM)>0`; un valor solo tab/newline puede superar ese CHECK aislado, prueba negativa que fija la diferencia respecto del normalizador de aplicación |
| `IT-PERSON-DUAL-ROLE` | un `PersonId` admite `Student` y `Teacher`; roles no duplican identidad y fijan schema/tabla/PK/FK/dependent/principal exactos, con auditoría solo donde corresponde |
| `IT-AUDIT-UTC-P0` | después de materializar P0: positivo exacto en `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`, con ambos defaults/check y update real que preserva creación; `Enrollment` solo `CreatedAtUtc`; negativo en `DocumentType`, `Student`, `AcademicConfiguration` |
| `IT-ROWVERSION-P0` | después de materializar P0: token y conflicto entre dos contextos exactamente para las siete entidades auditables P0; ausencia en `Enrollment`, `DocumentType`, `Student`, `AcademicConfiguration` |
| `IT-SINGLETON` | producida y ejecutada en S07 después de V2-T045: PK+CHECK bloquean segundo Id; seed Id=1 existe; runtime no inserta/elimina; trigger bloquea delete; ausencia simulada provoca fail-fast |
| `IT-REFERENCE-PERMISSIONS` | producida y ejecutada en S07 después de V2-T045: con `EXECUTE AS USER` para un usuario de prueba miembro solo de `[inovait_runtime]`, SELECT de `catalog.DocumentType` funciona e INSERT/UPDATE/DELETE fallan por DENY explícito; la paridad posterior de setup pertenece a `IT-SQL-SCRIPT` |
| `IT-IMMUTABILITY` | producida y ejecutada en S07 después de V2-T045: EF rechaza cambio de Code/Sector; SQL directo activa cada trigger P0; otros campos mutables sí cambian |
| `IT-ENR-ANNUAL` | permite historia en años distintos; UNIQUE estudiante+año rechaza duplicado y FK compuesto con `UQ_ClassGroup_Id_AcademicYear_ForEnrollment` rechaza año divergente |
| `IT-ENR-ATOMIC` | alta Person/Student/Enrollment todo-o-nada; carrera anual tiene un ganador |
| `IT-CON-CANCELLATION` | checks all-or-none, razón no vacía y fecha efectiva dentro del período |
| `IT-CON-OVERLAP` | duplicado/toque/carrera `Serializable`; escuela diferente permitida |
| `IT-NORMAL-FORMS` | claves/FK prueban roles sin duplicación; Enrollment no contiene School/Grade/estado derivado ni auditoría genérica; cero agregados persistidos |
| `IT-INDEXES-P0` | nombres, orden de keys e `INCLUDE` exactos para ClassGroup/Enrollment/TeacherContract; `Id` ausente de INCLUDE y disponible implícitamente por PK clustered; FK soportadas sin índices redundantes declarados |
| `IT-SEED-P0` | 11 tablas, catálogos con códigos, DocumentType y singleton coherentes |
| `IT-SQL-SCRIPT` | setup y migración coinciden en schemas, columnas, collation, defaults, constraints, índices, triggers, seeds y permisos |

## Integración HTTP/contrato P0

| ID | Evidencia |
| --- | --- |
| `IT-CATALOGS` | cinco operationIds P0, DTOs canónicos, filtros acumulativos, `200 []` para contexto existente sin grupos y órdenes OpenAPI |
| `IT-PROBLEMS` | 400/404/409/422 con media type, `code`, campos y sin detalles internos |
| `IT-ENR-CREATE` | `Person`/`Student` nuevos + `Enrollment` atómicos, `201` y respuesta canónica |
| `IT-ENR-IDENTITY` | reutilización de identidad equivalente; documento existente discrepante devuelve 409 sin modificar datos |
| `IT-ENR-CONTEXT` | fecha futura, referencias inexistentes y grupo ajeno a escuela/grado/año se rechazan sin persistencia parcial |
| `IT-ENR-FILTER` | filtros conjuntos, vacío válido, edad/`asOfDate`, proyección documental y orden HTTP |
| `IT-CON-MULTI` | dos escuelas crean dos contratos o ninguno en una transacción |
| `IT-CON-DATES` | rango, contrato abierto, referencias inexistentes y solicitud inválida sin resultados parciales |
| `IT-CON-LIST` | consultas por docente y escuela, estado persistido/efectivo y órdenes contractuales canónicos |
| `IT-OPENAPI-P0` | bundle de 15 operationIds intacto y runtime P0 con exactamente sus 10 operationIds, sin cambios del contrato |

## Integración P1

| ID | Evidencia |
| --- | --- |
| `IT-ASSIGNMENT-PERIOD` | checks start/end y rechazo transaccional de escuela, año, contrato o cancelación incompatibles |
| `IT-INDEXES-P1` | ambos índices temporales de TeachingAssignment, includes sin `Id`, clustered PK implícita y soporte Subject |
| `IT-AUDIT-UTC-P1` | después de materializar las 14 tablas: defaults/check/update real en `Subject` y `TeachingAssignment`; `ClassSchedule` solo `CreatedAtUtc` |
| `IT-ROWVERSION-P1` | después de materializar las 14 tablas: token y conflicto entre dos contextos en `Subject` y `TeachingAssignment`; ausencia en `ClassSchedule` |
| `IT-SEED-P1` | 14 tablas y dataset de límites, empate, multisector e historia múltiple |
| `IT-LIST-SUBJECTS` | `200`, DTO canónico, orden `name, code, id`, sin paginación y operationId P1 intacto |
| `IT-RPT-AGE` | 2/3/7/8/12/13, filtros, cero y `asOfDate` |
| `IT-RPT-SECTOR` | distinct por sector, cancelación, estado y período |
| `IT-RPT-TOP` | máximo, empates, vacío y orden |
| `IT-HISTORY` | años, roles compartidos, múltiples materias/docentes y período de asignación |
| `IT-SQL-SCRIPT-P1` | delta a 14 tablas con paridad completa |
| `IT-OPENAPI` | bundle intacto, refs válidas y 15 operationIds únicos |

## Verificación de índices

Las pruebas consultan `sys.indexes` y `sys.index_columns`, distinguen key/include y comprueban orden. Verifican que las PK `Id` sigan clustered y que, por ello, `Id` esté disponible implícitamente en los índices nonclustered sin aparecer como INCLUDE. Si cambia el clustering, la prueba falla y obliga a re-evaluar proyecciones e índices. No fuerzan un plan exacto frágil; para queries críticas capturan `SET SHOWPLAN_XML ON` en una prueba diagnóstica separada y verifican que el índice esperado sea elegible con dataset representativo. La puerta exige definición correcta, no una estimación del optimizador dependiente del volumen.

## Staging y manifests de evidencia

S02 produce el harness probe. S03 posee tres IDs catalog-only y dos casos P0 auxiliares para concurrencia y rollback/cleanup de seed. S04 produce `UT-IDENTITY` y los tres IDs `IT-PERSON-*`/`IT-TEXT-CHECKS` contra `Person` y roles reales. S05/V2-T032 produce `IT-ENR-ANNUAL`, `IT-NORMAL-FORMS` y assertions parciales de índices sin anticipar `IT-INDEXES-P0`, reservado para V2-T070; V2-T036 agrega cobertura de command/resultados/errores, cancelación, rollback, carrera sincronizada y agotamiento de tres intentos sin crear IDs de evidencia nuevos. V2-T046 revalida las protecciones completas mediante `IT-SCHEMAS-P0`, `IT-IMMUTABILITY`, `IT-SINGLETON` e `IT-REFERENCE-PERMISSIONS` después de materializar 11 tablas y la migración manual.

### Manifest P0 canónico: ID → productor único

| ID obligatorio | Productor único anterior a V2-T071 |
| --- | --- |
| `UT-TEXT-NORMALIZATION` | V2-T011 |
| `UT-AUDIT-INTERCEPTOR` | V2-T012 |
| `IT-CATALOG-SCHEMA-S03` | V2-T020 |
| `IT-CATALOG-MUTABILITY-S03` | V2-T020 |
| `IT-CATALOG-SINGLETON-S03` | V2-T020 |
| `UT-IDENTITY` | V2-T027 |
| `IT-PERSON-COLLATION` | V2-T027 |
| `IT-TEXT-CHECKS` | V2-T027 |
| `IT-PERSON-DUAL-ROLE` | V2-T027 |
| `IT-ENR-ANNUAL` | V2-T032 |
| `IT-NORMAL-FORMS` | V2-T032 |
| `UT-CONTRACT-OVERLAP` | V2-T038 |
| `UT-CONTRACT-CANCELLATION` | V2-T038 |
| `UT-CONTRACT-STATUS` | V2-T038 |
| `IT-CON-CANCELLATION` | V2-T039 |
| `IT-CON-OVERLAP` | V2-T039 |
| `IT-SCHEMAS-P0` | V2-T046 |
| `IT-IMMUTABILITY` | V2-T046 |
| `IT-SINGLETON` | V2-T046 |
| `IT-REFERENCE-PERMISSIONS` | V2-T046 |
| `IT-AUDIT-UTC-P0` | V2-T046 |
| `IT-ROWVERSION-P0` | V2-T046 |
| `IT-CATALOGS` | V2-T047 |
| `IT-PROBLEMS` | V2-T062 |
| `IT-ENR-CREATE` | V2-T052 |
| `IT-ENR-IDENTITY` | V2-T052 |
| `IT-ENR-CONTEXT` | V2-T052 |
| `IT-ENR-ATOMIC` | V2-T053 |
| `UT-AGE` | V2-T058 |
| `IT-ENR-FILTER` | V2-T058 |
| `IT-CON-MULTI` | V2-T062 |
| `IT-CON-DATES` | V2-T062 |
| `IT-CON-LIST` | V2-T062 |
| `IT-OPENAPI-P0` | V2-T062 |
| `IT-SQL-SCRIPT` | V2-T069 |
| `IT-SEED-P0` | V2-T069 |
| `IT-INDEXES-P0` | V2-T070 |

El manifest P1 consumido por V2-T101 permanece separado: `UT-ASSIGNMENT`, `IT-ASSIGNMENT-PERIOD`, `IT-INDEXES-P1`, `IT-AUDIT-UTC-P1`, `IT-ROWVERSION-P1`, `IT-SEED-P1`, `IT-LIST-SUBJECTS`, `IT-RPT-AGE`, `IT-RPT-SECTOR`, `IT-RPT-TOP`, `IT-HISTORY`, `IT-SQL-SCRIPT-P1`, `IT-OPENAPI`.

- Toda prueba declara un trait `Priority` de su etapa y un trait `Evidence` presente en el manifest de esa etapa.
- V2-T071 rechaza IDs repetidos en este manifest y exige, para cada uno de los **37** IDs, al menos una prueba descubierta con el filtro conjunto `Priority=P0&Evidence=<ID>`; solo entonces ejecuta toda la suite `Priority=P0`. El piso de **20** casos es un sanity check secundario, no el gate de completitud.
- V2-T101 aplica el mismo fail-on-missing al manifest P1. Ningún runner exige evidencia cuyo productor sea posterior al propio consumer.
- P1 solo inicia cuando P0 pasa contra Testcontainers, la paridad de 11 tablas es verde y los tres walkthroughs quedan registrados.
- Ninguna prueba usa skip/supresión ni una base compartida. Cada escenario parte de estado conocido; carreras usan conexiones separadas y timeout acotado.

## Riesgos cubiertos

| Riesgo | Defensa de prueba |
| --- | --- |
| default confundido con actualización | `IT-AUDIT-UTC-P0/P1` prueba un update real después de materializar cada etapa |
| auditoría aplicada por convención a tablas incorrectas | assertions exactas y negativas de `IT-AUDIT-UTC-P0/P1`/`IT-ROWVERSION-P0/P1` |
| CHECK SQL sobreestimado para whitespace Unicode | `IT-TEXT-CHECKS` separa U+0020 directo de `UT-TEXT-NORMALIZATION` |
| CHECK supuesto como existencia | `IT-SINGLETON` separa máximo uno, seed y delete |
| inmutabilidad solo en EF | `IT-IMMUTABILITY` escribe SQL directo |
| collation distinta en sustitutos | `IT-PERSON-COLLATION` solo SQL Server |
| dependencia Enrollment divergente | `IT-ENR-ANNUAL` fuerza FK compuesto |
| índices documentados pero no creados | `IT-INDEXES-*` inspecciona key/include/filter |
| catálogo de referencia mutable por runtime | `IT-REFERENCE-PERMISSIONS` prueba los tres DENY y paridad |
| migración y script divergentes | `IT-SQL-SCRIPT*` compara metadatos de dos bases limpias |
