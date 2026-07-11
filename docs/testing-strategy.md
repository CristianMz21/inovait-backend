# Estrategia de pruebas planificada

## Decisión

Usar pruebas unitarias para reglas puras y un conjunto enfocado de integración HTTP contra SQL Server real. No usar EF InMemory como evidencia de consultas, transacciones o restricciones.

## Capas y herramientas futuras

| Capa | Herramienta | Alcance |
| --- | --- | --- |
| Unit | xUnit v3 3.2.2 | normalización, edad, solapamiento, estado efectivo, compatibilidad temporal |
| Integration | `WebApplicationFactory` 10.0.9 + EF/SQL Server | pipeline HTTP, transacciones, FK/UNIQUE/CHECK, LINQ SQL y orden |
| Contract | parser OpenAPI disponible en implementación | sintaxis, refs, 15 operationIds únicos y respuesta real compatible |
| Walkthrough | quickstart + cliente HTTP | recorridos P0 y reportes P1 con seeds ficticios |

No se fija porcentaje de cobertura. Cada prueba existe por un riesgo identificado.

## SQL Server: opción proporcional

**Ruta P0 primaria**: `Testcontainers.MsSql` 4.13.0 con SQL Server 2022, un contenedor por colección de integración y base reiniciada entre escenarios. Es la única puerta automatizada relacional obligatoria de P0.

**Fallback documentado**: instancia SQL Server 2022 indicada por `ConnectionStrings__Inovait`, con base exclusiva marcada como test. Sirve cuando Docker no está disponible, pero no se ejecuta además de Testcontainers como gate duplicado. El runner aborta sobre bases no marcadas; LocalDB queda limitado a Windows.

**Rechazadas como prueba relacional**: EF InMemory carece de transacciones y semántica SQL; SQLite difiere en collation, `CHECK`, SQL y traducciones. Pueden servir para prototipos, no como evidencia constitucional.

## Unitarias

| ID | Regla | Casos mínimos |
| --- | --- | --- |
| UT-IDENTITY | normalización/comparación | tipo trim/case; número sin espacios/puntos/guiones; nombres con espacios y diacríticos |
| UT-AGE | años cumplidos | edades 3, 7, 8, 12, 13; cumpleaños; fecha anterior al nacimiento |
| UT-CONTRACT-OVERLAP | intervalos inclusivos | separados, mismo día, contenido, extremos, fin abierto |
| UT-CONTRACT-STATUS | estado efectivo | Confirmed antes/durante/después; Cancelled domina |
| UT-ASSIGNMENT (P1) | compatibilidad | misma escuela y al menos un día de intersección con `AcademicYear` |

## Integración P0

| ID | Evidencia |
| --- | --- |
| IT-CATALOGS | catálogos ficticios, filtros de grupos y orden determinista |
| IT-ENR-CREATE | `Student` nuevo + `Enrollment` atómicos y 201 |
| IT-ENR-IDENTITY | reutilización coincidente; documento duplicado discrepante devuelve 409 |
| IT-ENR-ANNUAL | UNIQUE `StudentId+AcademicYearId`; año posterior conserva historia |
| IT-ENR-CONTEXT | FK compuesto rechaza un ClassGroup de alta incompatible; referencias existentes sin grupos devuelven 200 [] en listas |
| IT-ENR-ATOMIC | fallo al insertar inscripción revierte estudiante nuevo |
| IT-ENR-FILTER | filtros conjuntos, vacío válido, edad/asOfDate y orden |
| IT-CON-MULTI | dos escuelas crean dos contratos en una transacción |
| IT-CON-DATES | CHECK fin/inicio, abierto y escuela/docente inexistente |
| IT-CON-OVERLAP | duplicado, toque inclusivo y carrera serializable; otra escuela permitida |
| IT-CON-LIST | consulta por docente y por escuela, estado persistido/efectivo y orden |
| IT-PROBLEMS | 400/404/409/422 con media type, `code`, campos y sin detalles internos |
| IT-SEED-P0 | 8 tablas y seeds mínimos cubren tres recorridos, varios años, grupos y contratos |
| IT-SQL-SCRIPT | `database/setup.sql` P0 recrea el mismo esquema, constraints y seeds desde una base limpia |

## Integración P1

| ID | Evidencia |
| --- | --- |
| IT-RPT-AGE | límites 2/3/7/8/12/13, filtros acumulativos, cero y `asOfDate` |
| IT-RPT-SECTOR | `COUNT(DISTINCT TeacherId)` por sector, ambos sectores, estado y período inclusivo |
| IT-RPT-TOP | máximo anual, todos los empates, vacío y orden name/id |
| IT-HISTORY | múltiples años, múltiples docentes/materias, año vacío e identidad normalizada |
| IT-ASSIGNMENT | escuela de contrato distinta y período sin intersección se rechazan; weekdays 1..7 y único |
| IT-SEED-P1 | extensión ficticia cubre límites, sectores, empate, abiertos/cerrados e historia múltiple |
| IT-SQL-SCRIPT-P1 | extensión de `database/setup.sql` agrega 3 tablas y seeds P1 con paridad |
| IT-OPENAPI | YAML y refs válidos; operationIds únicos; respuestas HTTP compatibles con schemas |

## Identificación y runner P0

- Toda prueba backend P0, unitaria o de integración, declara `[Trait("Priority", "P0")]`.
- `scripts/run-p0-tests.sh` ejecuta primero `dotnet test --list-tests --filter "Priority=P0"`, cuenta nombres descubiertos y falla si son menos de **12**.
- Solo después ejecuta `dotnet test --filter "Priority=P0"`; por ello cero pruebas nunca produce un falso verde.
- El mínimo cubre al menos identidad/edad/contratos, catálogos, alta/atomicidad, consulta, contratación/lista, problemas, OpenAPI y paridad SQL P0.

## Aislamiento y datos

- Cada clase parte de esquema conocido; no depende del orden de pruebas.
- Las pruebas que verifican carreras usan conexiones separadas y timeout acotado.
- Las listas se comparan completas, incluido orden.
- `IBusinessDateProvider` inyectado fija “hoy” para no volver frágiles edad y estado; producción usa fecha calendario UTC.
- Todos los nombres y documentos son ficticios; logs de fallo no imprimen connection strings.

## Puerta P0 → P1

P1 solo se inicia cuando el runner descubre ≥12 casos P0 y pasa con Testcontainers, `database/setup.sql` mínimo demuestra paridad P0 y el walkthrough queda registrado. La base externa no se repite como gate. Una prueba unitaria verde no sustituye esa puerta.

## Observación local de rendimiento

El walkthrough registra una ejecución calentada de `listEnrollments`, `listTeacherContracts` y `listTeachersBySchool` con el dataset P0. Los tiempos son diagnósticos, sin porcentaje, umbral ni condición de release; una demora evidente abre seguimiento, pero no sustituye evidencia funcional.
