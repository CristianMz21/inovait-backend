# Datos de demo (dataset estricto para evaluación)

`database/seed-demo.sql` siembra un dataset ficticio, determinista e idempotente pensado para
ejercitar **todas** las funcionalidades del backend (los 15 `operationId` del contrato) y responder
las cinco preguntas del caso con resultados exactos y verificables. **Supersede** al `demo-data.sql`
anterior (1 colegio / 1 grupo / 1 docente): ese archivo fue eliminado y ya no existe en el repositorio.

No hay autenticación (`security: []` en el contrato OpenAPI) — ningún comando ni request de esta guía
necesita credenciales.

## Cómo aplicarlo

| Vía | Comando |
| --- | --- |
| Script de despliegue (recomendado) | `./scripts/deploy-local.sh` (o `-NoDemoData`/`--no-demo-data` para omitirlo) |
| `sqlcmd` standalone | `sqlcmd -S <server> -d Inovait -i database/seed-demo.sql` |
| Flag de la API (.NET) | `dotnet run --project src/Inovait.Api -- --seed-demo` |
| Variable de entorno | `INOVAIT_SEED_DEMO=true dotnet run --project src/Inovait.Api` |
| Configuración (solo Development) | `Inovait:SeedDemoData=true` en `appsettings.Development.json` o config |

El seeder .NET (`DemoDataSeeder.ApplyAsync`, invocado desde `Program.cs` inmediatamente después del
chequeo de arranque de `AcademicConfiguration`) ejecuta el **mismo** archivo `database/seed-demo.sql`,
embebido como `EmbeddedResource` en `Inovait.Infrastructure` (`Link="Persistence/Seed/seed-demo.sql"`
apuntando al único archivo físico en `database/`) — no hay dos copias del SQL que puedan divergir.
Nunca se aplica en Production sin una de las tres flags explícitas de arriba.

**Reset**: `database/reset-demo.sql` (standalone con `sqlcmd`, o `ExecuteSqlRawAsync` desde código)
borra únicamente el namespace `DEMO-%`/`COL-%`, hijas→padres, dentro de una transacción. Nunca toca
el seed canónico (`SCH-001`, `AY-2026`, `G01`, `CC`) ni los `DocumentType` `DNI`/`PAS`/`CE` (el
formulario de matrícula del frontend los sigue necesitando después de un reset).

**Doble ejecución**: tanto `seed-demo.sql` como `reset-demo.sql` son idempotentes — correr
`seed-demo.sql` dos veces seguidas no duplica ninguna fila (cada bloque es `IF NOT EXISTS`/
`INSERT ... WHERE NOT EXISTS` sobre una clave natural), y `reset-demo.sql` sobre una base ya limpia
simplemente imprime 0 filas borradas por tabla. `tests/Inovait.IntegrationTests/Persistence/DemoDataSeedTests.cs`
(`Category=DemoSeed`) verifica ambos ciclos contra una base real.

## Dataset y totales esperados

| Entidad | Cantidad | Detalle |
| --- | --- | --- |
| Colegios | 4 | `COL-PUB-001`/`COL-PUB-002` (Public), `COL-PRI-001`/`COL-PRI-002` (Private) |
| Grados | 14 | `DEMO-PJ` (Prejardín) .. `DEMO-G11` (Undécimo), `SortOrder` 100-113 |
| Asignaturas | 4 | `DEMO-MAT`, `DEMO-LEN`, `DEMO-CIE`, `DEMO-SOC` |
| Años académicos demo | 2 | `DEMO-AY-<currentYear-1>`, `DEMO-AY-<currentYear-2>` (calendario completo Ene-Dic) |
| Estudiantes | 24 | `DEMO-EST-001`..`024` (DNI) |
| Matrículas | 40 | 14 estudiantes × 1 año + 4 × 2 años + 6 × 3 años |
| Docentes | 8 | `DEMO-DOC-001`..`008` (DNI) |
| Contratos docentes | 10 | 8 activos hoy + 1 vencido + 1 futuro, todos `Confirmed` |

`currentYear` **no** es wall-clock: se lee de `catalog.AcademicYear` vía
`catalog.AcademicConfiguration(Id=1)`. Los años históricos (`y-1`, `y-2`) y las fechas de nacimiento
de los estudiantes se calculan siempre relativos a ese año y a `GETDATE()`, así que el dataset sigue
siendo válido sin importar el día en que se lo siembre.

### Matrícula del año actual por colegio (24 matrículas)

| Colegio | Matrículas |
| --- | --- |
| `COL-PUB-001` | **10** (el colegio con más matrícula del año — pregunta 3) |
| `COL-PUB-002` | 6 |
| `COL-PRI-001` | 5 |
| `COL-PRI-002` | 3 |

Invariante estricto: Quinto (`DEMO-G05`) en `COL-PUB-001` en el año actual tiene **exactamente 3**
matrículas (`DEMO-EST-006`, `DEMO-EST-011`, `DEMO-EST-012`).

### Matriz resumen de matrículas (24 estudiantes)

| Doc | Edad | Colegio | Años | Progresión de grado (y-2 → y-1 → y) | Grupo(s) |
| --- | --- | --- | --- | --- | --- |
| DEMO-EST-001 | 3 | COL-PUB-001 | 1 | → Prejardín | A |
| DEMO-EST-002 | 5 | COL-PUB-002 | 1 | → Transición | A |
| DEMO-EST-003 | 6 | COL-PRI-001 | 2 | Transición → Primero | A / A |
| DEMO-EST-004 | 4 | COL-PUB-001 | 1 | → Jardín | A |
| DEMO-EST-005 | 7 | COL-PRI-001 | 1 | → Segundo | A |
| **DEMO-EST-006** | 10 | COL-PUB-001 | 3 | **Tercero → Cuarto → Quinto** (caso fijo) | A / B / A |
| DEMO-EST-007 | 4 | COL-PRI-002 | 1 | → Jardín | A |
| DEMO-EST-008 | 6 | COL-PUB-002 | 2 | Transición → Primero | A / A |
| DEMO-EST-009 | 5 | COL-PUB-001 | 1 | → Transición | A |
| DEMO-EST-010 | 8 | COL-PUB-002 | 3 | Primero → Segundo → Tercero | A / A / A |
| DEMO-EST-011 | 9 | COL-PUB-001 | 1 | → Quinto | A |
| DEMO-EST-012 | 10 | COL-PUB-001 | 1 | → Quinto | A |
| DEMO-EST-013 | 11 | COL-PUB-002 | 2 | Quinto → Sexto | A / A |
| DEMO-EST-014 | 12 | COL-PRI-001 | 3 | Quinto → Sexto → Séptimo | A / A / A |
| DEMO-EST-015 | 9 | COL-PUB-001 | 1 | → Cuarto | A |
| DEMO-EST-016 | 11 | COL-PUB-001 | 1 | → Sexto | A |
| DEMO-EST-017 | 13 | COL-PUB-001 | 1 | → Octavo | A |
| DEMO-EST-018 | 14 | COL-PUB-002 | 3 | Séptimo → Octavo → Noveno | A / A / A |
| DEMO-EST-019 | 13 | COL-PRI-001 | 2 | Séptimo → Octavo | A / A |
| DEMO-EST-020 | 15 | COL-PUB-001 | 1 | → Décimo | A |
| DEMO-EST-021 | 16 | COL-PRI-002 | 3 | Noveno → Décimo → Undécimo | A / A / A |
| DEMO-EST-022 | 14 | COL-PUB-002 | 1 | → Noveno | A |
| DEMO-EST-023 | 17 | COL-PRI-001 | 1 | → Undécimo | A |
| DEMO-EST-024 | 15 | COL-PRI-002 | 3 | Octavo → Noveno → Décimo | A / A / A |

Cada estudiante multi-año permanece en un único colegio a lo largo de todos sus años (nunca dos
colegios en el mismo año — lo garantiza además la unique `UQ_Enrollment_StudentPersonId_AcademicYearId`)
y su grado avanza exactamente +1 nivel por año.

### Docentes y contratos

| Doc | Colegio(s) | Estado | Notas |
| --- | --- | --- | --- |
| DEMO-DOC-001 | COL-PUB-001 | Activo desde ≤ y-2 | Cubre la asignación histórica de EST-006 en y-2 |
| DEMO-DOC-002 | COL-PUB-002 | Activo | |
| DEMO-DOC-003 | COL-PRI-001 | Activo | |
| DEMO-DOC-004 | COL-PRI-002 | Activo | |
| DEMO-DOC-005 | COL-PUB-001 + COL-PRI-001 | Activo simultáneo desde ≤ y-1 | Cubre la asignación actual de EST-006 |
| DEMO-DOC-006 | COL-PUB-001 + COL-PUB-002 | Activo simultáneo desde ≤ y-2 | Cubre la asignación de EST-006 en y-1 |
| DEMO-DOC-007 | COL-PUB-002 | **Vencido** (`EndDate` < hoy) | |
| DEMO-DOC-008 | COL-PRI-001 | **Futuro** (`StartDate` > hoy) | |

Totales activos hoy: **4** docentes públicos, **3** privados, **6** docentes distintos, **8**
contratos activos (10 contratos en total: 8 activos + 1 vencido + 1 futuro).

### Historia docente de DEMO-EST-006 (caso fijo de trazabilidad)

| Año | Grado | Grupo | Docente | Materia |
| --- | --- | --- | --- | --- |
| y-2 | Tercero (`DEMO-G03`) | A | DEMO-DOC-001 | Matemáticas |
| y-1 | Cuarto (`DEMO-G04`) | B | DEMO-DOC-006 | Lenguaje |
| y | Quinto (`DEMO-G05`) | A | DEMO-DOC-005 | Matemáticas |

`GET /api/students/DNI/DEMO-EST-006/history` devuelve estos 3 años con un docente distinto por año.

## Las 5 preguntas del caso, con este dataset

1. **Distribución de edades** (`GET /api/reports/age-distribution?academicYearId=<actual>`): **8**
   estudiantes en `[3-7]`, **8** en `[8-12]`, **8** en `13+` (edades exactas 3, 7, 8, 12 y 13
   presentes en el dataset).
2. **Docentes por sector** (`GET /api/reports/teacher-counts-by-sector`, sin `periodStart`/`periodEnd`
   usa hoy-hoy): `publicCount=4`, `privateCount=3`.
3. **Colegio con mayor matrícula** (`GET /api/reports/top-schools?academicYearId=<actual>`): un único
   resultado, `COL-PUB-001` ("Colegio Público Central"), `enrollmentCount=10`.
4. **Matrícula por colegio/grado/año** (`GET /api/enrollments?schoolId=<COL-PUB-001>&gradeId=<Quinto>&academicYearId=<actual>`):
   exactamente 3 resultados.
5. **Histórico grado/grupo/docente por estudiante** (`GET /api/students/DNI/DEMO-EST-006/history`): 3
   años, docente distinto por año, grados `DEMO-G03` → `DEMO-G04` → `DEMO-G05`, grupos A → B → A.

Ver `requests/evaluator.http` para el flujo completo request por request (incluye descubrimiento de
Id, contratos simultáneos de DEMO-DOC-005 y alta de un estudiante nuevo).
