# Inovait — Backend

API .NET para la gestión de matrículas escolares y contratación docente de una ciudad (prueba técnica full stack INOVAIT). Junto con [`inovait-frontend`](../inovait-frontend) (Angular), responde las cinco preguntas del caso: distribución de edades (3–7, 8–12, >12), docentes por sector público/privado, colegio con mayor matrícula e histórico grado/grupo/docente por estudiante.

- **Stack**: .NET 10 (`net10.0`, C# 14), ASP.NET Core minimal APIs, EF Core 10, SQL Server 2022, xUnit v3 + Testcontainers.
- **Modelo**: 14 tablas en 3NF/BCNF — [diagrama entidad-relación](docs/entity-relationship-model.md).
- **Contrato HTTP**: 15 operaciones OpenAPI inmutables — [bundle canónico](specs/001-school-enrollment-management/contracts/openapi.yaml).
- **Mapeo caso→implementación**: [requirements-traceability.md](docs/requirements-traceability.md).

## Inicio rápido (stack completo con un comando)

### Prerrequisitos

| Herramienta | Versión | Descarga |
| --- | --- | --- |
| Docker (con Compose v2) | reciente | <https://docs.docker.com/get-docker/> |
| .NET SDK | 10.x (fijada `10.0.109` en `global.json`) | <https://dotnet.microsoft.com/download> |
| Node.js + npm | Node 24.x, npm 11.x (solo para el frontend) | <https://nodejs.org/> |

Puertos libres necesarios: `1433` (SQL Server), `5000` (API) y `4200` (frontend) — todos parametrizables.

### Pasos

1. Clonar ambos repositorios como hermanos (el script asume `../inovait-frontend`):

   ```bash
   git clone git@github.com:CristianMz21/inovait-backend.git
   git clone git@github.com:CristianMz21/inovait-frontend.git
   cd inovait-backend
   ```

2. Levantar todo — SQL Server, esquema, datos de demo, API y frontend:

   ```bash
   # Linux / macOS
   ./scripts/deploy-local.sh
   ```

   ```powershell
   # Windows (PowerShell 5.1+)
   powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1
   ```

   La primera ejecución instala las dependencias del frontend (`npm ci`) y puede tardar unos minutos. Antes de tocar nada, `--check-only` / `-CheckOnly` valida los prerrequisitos y termina.

3. Verificar: el script termina con un banner de éxito y estas URLs quedan activas:

   | Servicio | URL |
   | --- | --- |
   | Frontend (5 pantallas) | <http://localhost:4200> — abrir por `localhost`, no `127.0.0.1` (allowlist de CORS) |
   | API | <http://localhost:5000> |
   | Health (probe real de BD) | <http://localhost:5000/health/ready> |

4. Bajar todo al terminar:

   ```bash
   ./scripts/deploy-local.sh --down        # o -Down en PowerShell
   ```

Qué hace el script, en orden: chequea prerrequisitos → levanta SQL Server 2022 vía `docker compose` (password SA generada en memoria, impresa una sola vez, nunca escrita a disco) → crea la base `Inovait` y aplica [`database/setup.sql`](database/setup.sql) (14 tablas, idempotente) → siembra [`database/demo-data.sql`](database/demo-data.sql) (datos ficticios de evaluación, opt-out) → inicia la API en `:5000` esperando `/health/ready` → sirve el frontend en `:4200` con configuración de producción (HTTP real, sin mocks). Estado y logs quedan en `.local-stack/` (gitignored).

### Parámetros del script

| Parámetro (bash) | Parámetro (PowerShell) | Descripción |
| --- | --- | --- |
| `--api-port <port>` | `-ApiPort <port>` | Puerto de la API (default `5000`). |
| `--sql-port <port>` | `-SqlPort <port>` | Puerto host del contenedor SQL Server (default `1433`). |
| `--frontend-path <path>` | `-FrontendPath <path>` | Ruta al checkout de `inovait-frontend` (default `../inovait-frontend`). |
| `--sa-password <password>` | `-SaPassword <password>` | Password SA de SQL Server (default: generada en runtime). |
| `--skip-frontend` | `-SkipFrontend` | Omite build/serve del frontend Angular. |
| `--no-demo-data` | `-NoDemoData` | Omite el seed de datos ficticios de evaluación local. |
| `--down` | `-Down` | Da de baja un stack levantado previamente. |
| `--check-only` | `-CheckOnly` | Corre solo los chequeos de prerrequisitos y termina. |

## Instalación manual (sin el script)

Los mismos pasos que automatiza `deploy-local`, uno a uno:

1. **SQL Server** (contenedor con healthcheck):

   ```bash
   export MSSQL_SA_PASSWORD='<clave-fuerte-propia>'
   docker compose -f compose.yaml up -d --wait
   ```

2. **Base de datos** — crear `Inovait` y aplicar los dos scripts entregables (esquema + datos), ambos idempotentes:

   ```bash
   docker compose cp database/setup.sql sql-server:/tmp/setup.sql
   docker compose cp database/demo-data.sql sql-server:/tmp/demo-data.sql
   docker compose exec -u root sql-server chmod 0444 /tmp/setup.sql /tmp/demo-data.sql
   docker compose exec sql-server /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" \
     -Q "IF DB_ID('Inovait') IS NULL CREATE DATABASE Inovait"
   docker compose exec sql-server /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d Inovait -i /tmp/setup.sql
   docker compose exec sql-server /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d Inovait -i /tmp/demo-data.sql
   ```

   Alternativa equivalente: aplicar la cadena de migraciones EF (`dotnet ef database update`); `setup.sql` está en paridad verificada con las migraciones.

3. **API** — la clave de conexión runtime es `ConnectionStrings__InovaitDatabase` (variable de entorno; nunca se versiona una connection string):

   ```bash
   export ASPNETCORE_URLS='http://localhost:5000'
   export ConnectionStrings__InovaitDatabase="Server=localhost,1433;Database=Inovait;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
   dotnet run --project src/Inovait.Api -c Release --no-launch-profile
   ```

   `TrustServerCertificate=True` aplica solo al certificado autofirmado del contenedor local; no es configuración de producción.

4. **Frontend** — ver el [README de inovait-frontend](../inovait-frontend/README.md); en resumen: `npm ci` y `npx ng serve --configuration production --port 4200`.

## Pruebas

```bash
dotnet restore
dotnet build --no-restore -c Debug          # 0 warnings (TreatWarningsAsErrors)
dotnet test  --no-build --no-restore -c Debug
./scripts/run-p0-tests.sh                   # gate P0: 37/37 IDs de evidencia
./scripts/run-p1-tests.sh                   # gate P1: 13/13 IDs de evidencia
```

Las pruebas de integración usan Testcontainers (SQL Server 2022 CU14 real, imagen fijada); no requieren configuración. Sin Docker, puede apuntarse un SQL Server externo con la variable `ConnectionStrings__InovaitTest` (fallback documentado solo para tests).

## Estructura del proyecto

| Ruta | Contenido |
| --- | --- |
| `src/Inovait.Core` | Dominio y workflows (entidades, validación transaccional, `AgeCalculator`). |
| `src/Inovait.Infrastructure` | EF Core: DbContext, configuraciones, migraciones, seeds, protecciones SQL. |
| `src/Inovait.Api` | Minimal APIs: endpoints, DTOs, ProblemDetails RFC 7807, health checks. |
| `tests/` | `Inovait.UnitTests` + `Inovait.IntegrationTests` (Testcontainers). |
| `database/` | Entregable de BD: `setup.sql` (esquema + seed canónico) y `demo-data.sql` (datos de demo). |
| `scripts/` | `deploy-local.{sh,ps1}`, runners de evidencia P0/P1. |
| `specs/001-school-enrollment-management/` | Especificación canónica y contrato OpenAPI inmutable. |
| `docs/` | [Diagrama ER](docs/entity-relationship-model.md), [trazabilidad](docs/requirements-traceability.md), [caso original](docs/assessment-baseline.md), [evidencia del evaluador](docs/evaluator-execution.md). |

## Contrato HTTP canónico

El bundle modular ([`openapi.yaml`](specs/001-school-enrollment-management/contracts/openapi.yaml)) preserva exactamente 15 `operationId`, sin autenticación (`security: []`). Rutas principales:

- `POST /api/enrollments` — alta atómica de Student/Enrollment (reutilización de identidad; conflicto anual → `409`).
- `GET /api/enrollments` — consulta conjunta por `schoolId`, `gradeId` y `academicYearId`.
- `POST /api/teachers/{teacherId}/contracts` — contratos multiescuela atómicos (todo-o-nada).
- `GET /api/teachers/{teacherId}/contracts` y `GET /api/schools/{schoolId}/teachers` — historia contractual.
- `GET /api/reports/*` — distribución de edades, docentes por sector, colegios líderes.
- `GET /api/students/{documentType}/{documentNumber}/history` — histórico grado/grupo/docente.

La prueba contractual primaria es igualdad del árbol respecto del baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` y ausencia de untracked:

```bash
git diff --exit-code 1223630ab99bf1bfaa4f5919fccf5ff539379c8e -- specs/001-school-enrollment-management/contracts
test -z "$(git status --porcelain --untracked-files=all -- specs/001-school-enrollment-management/contracts)"
```

Como evidencia secundaria, desde `specs/001-school-enrollment-management/contracts/`, el orden canónico es:

```bash
sha256sum openapi.yaml \
  paths/catalogs.yaml paths/enrollments.yaml \
  paths/teacher-contracts.yaml paths/reports.yaml \
  components/catalogs.yaml components/enrollments.yaml \
  components/teacher-contracts.yaml components/reports.yaml \
  components/problems.yaml | sha256sum
```

Produce `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a  -`.

## Datos de demo (ficticios, opt-out)

El seed canónico de producción solo trae `DocumentType` `CC` y ningún grupo/docente; el formulario de matrícula del frontend ofrece DNI/PAS/CE (ejemplos del contrato), de modo que sin datos de demo cualquier alta devuelve `404`. [`database/demo-data.sql`](database/demo-data.sql) siembra, de forma idempotente y solo para evaluación local:

- `DocumentType` DNI/PAS/CE.
- `School` `SCH-002` y `ClassGroup` `CG-01`.
- Docente Ana Gomez (`TCH-0001`) con contrato `Confirmed` en la escuela 1.
- `Subject` `MATH` con un `TeachingAssignment` sobre `CG-01` (días 1 y 3).

## Estado de la entrega y evidencia

`production-model-v2.0.0` está completo — 103/103 tareas (S01–S18 cerrados). El modelo materializa 14 tablas/5 triggers mediante cuatro migraciones EF (`InitialP0ProductionModel`, `AddP0DatabaseProtections`, `AddP1TeachingModel`, `AddP1DatabaseProtections`), con `database/setup.sql` en paridad verificada por tests (`IT-SQL-SCRIPT`/`IT-SQL-SCRIPT-P1`). Los 15 `operationId` del baseline están mapeados en runtime (`IT-OPENAPI`), incluidos inscripción atómica, consulta de inscritos, contratos multiescuela, catálogo de materias y los cuatro reportes/historia P1. `./scripts/run-p0-tests.sh` termina `P0 GATE PASSED: 37/37` y `./scripts/run-p1-tests.sh` termina `P1 GATE PASSED: 13/13`.

El task set actual es `production-model-v2.0.0` (`V2-T001`–`V2-T103`); los IDs v1 del baseline están supersedidos. La estrategia de revisión fue `stacked-to-main` con gate de líneas humanas por slice; las dispensas registradas (`EX-PLAN-2026-07-10`, `EX-INTEGRITY-2026-07-11`) constan en [evaluator-execution.md](docs/evaluator-execution.md), junto con los SHAs inmutables de cada gate. La generación OpenAPI runtime se descartó a propósito: `Microsoft.AspNetCore.OpenApi` `10.0.9` arrastra una dependencia transitiva con vulnerabilidad alta; el YAML canónico es la fuente contractual y su identidad se verifica por árbol + checksum.

Detalle operativo completo: [quickstart.md](specs/001-school-enrollment-management/quickstart.md).

## Fuera de alcance

Autenticación, autorización, microservicios, CQRS/event sourcing, cloud, CRUD administrativo, paginación y datos personales reales.
