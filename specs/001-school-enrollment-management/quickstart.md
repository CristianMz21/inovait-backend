# Guía futura de ejecución y validación

## Propósito

Esta guía describe cómo una persona evaluadora podrá ejecutar y recorrer la solución cuando la implementación exista. Los comandos no fueron ejecutados durante planificación.

## Prerrequisitos futuros

- .NET SDK `10.0.109`.
- Docker compatible para la ruta primaria de integración Testcontainers y SQL Server 2022 accesible para ejecutar el script del evaluador.
- Cliente HTTP o la aplicación `inovait-frontend` ejecutada desde un origen local permitido.
- Repositorios `inovait-backend` e `inovait-frontend` clonados por separado.

## Variables de entorno

```bash
export SQLSERVER_HOST="localhost,1433"
export SQLSERVER_DATABASE="Inovait"
export SQLSERVER_USER="sa"
export SQLSERVER_PASSWORD="valor-local-no-versionado"
export ConnectionStrings__Inovait="Server=${SQLSERVER_HOST};Database=${SQLSERVER_DATABASE};User Id=${SQLSERVER_USER};Password=${SQLSERVER_PASSWORD};TrustServerCertificate=True"
export Cors__AllowedOrigins__0="http://localhost:4200"
export ASPNETCORE_ENVIRONMENT="Development"
```

Los valores son marcadores locales, no secretos versionables. En Windows se usarán variables equivalentes.

## Evidencia contractual de planificación

El checksum combinado local de los diez YAML es `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`. Es evidencia del working tree sin seguimiento, no de `HEAD` ni de un release. Antes de apply deberá existir autorización explícita, commit que contenga el bundle completo y working tree clean; el consumidor frontend debe fallar si cualquiera de esas condiciones no se cumple.

## Ruta crítica P0 asistida por IA — ocho horas

**Pronóstico**: alto riesgo. Esta secuencia vuelve operativo el intento de una jornada, pero no garantiza el resultado. Supone planificación y puertas pre-apply aprobadas antes de iniciar el reloj; SDK .NET, Node y Docker listos; una sola persona operadora usando Codex/CLI; y P1 completamente excluido.

| Reloj | Timebox | Checklist agrupado | Salida obligatoria |
| --- | ---: | --- | --- |
| 00:00–00:45 | 45 min | T001–T009 — baseline y scaffold | baseline verificado/registrado, estrategia de revisión resuelta y solución base verde |
| 00:45–02:15 | 90 min | T010–T026 — persistencia y catálogos | 8 tablas P0, migración/seeds, host y cinco catálogos sobre SQL Server |
| 02:15–03:15 | 60 min | T027–T032 — inscripción | alta/reutilización, conflictos y atomicidad verdes |
| 03:15–03:50 | 35 min | T033–T036 — consulta | filtros conjuntos, vacío válido, edad y orden verdes |
| 03:50–04:55 | 65 min | T037–T042 — contratos | creación multiescuela, lista, atomicidad y solapamiento verdes |
| 04:55–07:00 | 125 min | T043–T048 — SQL, pruebas y documentación mínima | `database/setup.sql`, paridad, contrato/errores, runner, README y guía del evaluador listos |
| 07:00–08:00 | 60 min | T049–T051 — puerta y handoff frontend | gate P0 ejecutado, evidencia fechada y handoff separado |

Los rangos agrupan checklist fino y trabajo asistido; no convierten cada tarea en una hora ni autorizan omitir sus criterios.

### Cortes obligatorios

- **Mitad de jornada (04:00)**: T001–T036 deben estar verdes y el slice de contratos debe haber comenzado. Si no, se detiene el compromiso temporal y se registra el desvío; no se rebautiza un P0 incompleto como entrega de una jornada.
- **Antes de la última hora (07:00)**: toda conducta backend P0, el script SQL y las pruebas críticas de integridad deben existir y pasar de forma enfocada. Si no, no se emite handoff “listo”; la hora final se usa para corregir o documentar el bloqueo y el objetivo diario queda incumplido.

Ante desvíos solo se corta hardening no esencial, como observación de latencia, refactor cosmético o pruebas redundantes sin riesgo nuevo. Nunca se corta un entregable original requerido, `database/setup.sql` ni una prueba crítica de atomicidad, unicidad, FK compuesto, solapamiento o paridad SQL.

## Preparar el backend

Ruta EF futura:

```bash
dotnet restore
dotnet build --no-restore
dotnet ef database update --project src/Inovait.Infrastructure --startup-project src/Inovait.Api
dotnet run --project src/Inovait.Api
```

Ruta de script para evaluación limpia:

```bash
sqlcmd -S "$SQLSERVER_HOST" -d "$SQLSERVER_DATABASE" -U "$SQLSERVER_USER" -P "$SQLSERVER_PASSWORD" -i database/setup.sql
dotnet run --project src/Inovait.Api
```

`database/setup.sql` se implementará antes de la puerta P0. Su primera versión crea las 8 tablas y seeds mínimos de P0; P1 podrá ampliarla a 11 tablas después de la puerta. No existe en esta fase.

## Ejecutar pruebas futuras

```bash
./scripts/run-p0-tests.sh
```

El script futuro primero listará las pruebas con `Priority=P0`, fallará si descubre menos de 12 casos y luego ejecutará exactamente ese filtro. Todas las pruebas P0 declararán `[Trait("Priority", "P0")]`. Testcontainers es la ruta primaria obligatoria de integración; una conexión externa puede documentarse como fallback aislado, nunca como segunda puerta ni contra una base compartida/productiva.

## Walkthrough P0

1. `GET /api/schools`, `/api/grades`, `/api/academic-years` y `/api/class-groups` para obtener referencias ficticias.
2. `POST /api/enrollments` con un documento nuevo; esperar 201 y un único `Student`/`Enrollment`.
3. Repetir la identidad en otro año; esperar reutilización. Repetir en el mismo año; esperar 409 sin cambios.
4. `GET /api/enrollments?schoolId=...&gradeId=...&academicYearId=...&asOfDate=...`; verificar filtros conjuntos, edad y orden.
   Una combinación de IDs existentes sin grupos debe devolver `200 []`, no `422`.
5. `GET /api/teachers`, elegir un docente y ejecutar `POST /api/teachers/{teacherId}/contracts` con dos escuelas; esperar dos contratos o ninguno.
6. `GET /api/teachers/{teacherId}/contracts` y `GET /api/schools/{schoolId}/teachers`; verificar registros independientes y estados persistido/efectivo.

P1 no forma parte del compromiso de una jornada y solo puede iniciarse después de completar estos pasos, el runner P0, la paridad de `database/setup.sql` y la evidencia en `docs/evaluator-execution.md`.

## Walkthrough P1 condicional

1. `GET /api/reports/age-distribution` y verificar edades 2/3/7/8/12/13.
2. `GET /api/reports/teacher-counts-by-sector` y verificar docentes distintos, incluido uno en ambos sectores.
3. `GET /api/reports/top-schools` y verificar que se devuelvan todas las escuelas empatadas.
4. `GET /api/students/{documentType}/{documentNumber}/history` y verificar años, docentes y materias múltiples.

Las solicitudes y respuestas concretas están en [OpenAPI](./contracts/openapi.yaml); el modelo está en [data-model.md](./data-model.md).

## Resultado esperado

- Sin autenticación (`security: []`).
- JSON camelCase y errores RFC 7807.
- Datos ficticios únicamente.
- Consultas repetidas devuelven contenido y orden idénticos.
- Ninguna operación inválida deja persistencia parcial.
