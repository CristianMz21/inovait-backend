# Guía de ejecución y validación

## Propósito

Esta guía ejecuta el scaffold S01 actual y separa claramente los pasos futuros de persistencia y dominio.

## Prerrequisitos

- .NET SDK `10.0.109`.
- Docker compatible para Testcontainers y SQL Server 2022 serán necesarios desde S02.
- Cliente HTTP o `inovait-frontend` desde un origen local permitido.
- Repositorios backend/frontend clonados por separado.

## Variables locales

```bash
: "${SQLSERVER_HOST:?Set SQLSERVER_HOST to the test SQL Server endpoint}"
: "${SQLSERVER_DATABASE:?Set SQLSERVER_DATABASE}"
: "${SQLSERVER_USER:?Set SQLSERVER_USER through environment or user-secrets}"
: "${SQLSERVER_PASSWORD:?Set SQLSERVER_PASSWORD through environment or user-secrets}"
export ConnectionStrings__Inovait="Server=${SQLSERVER_HOST};Database=${SQLSERVER_DATABASE};User Id=${SQLSERVER_USER};Password=${SQLSERVER_PASSWORD};Encrypt=True"
export Cors__AllowedOrigins__0="http://localhost:4200"
```

Son marcadores locales, nunca valores versionables.

## Estado contractual y entrega

El bundle OpenAPI permanece sin cambios: baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e`, checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`. La prueba primaria es igualdad del árbol contractual y ausencia de archivos untracked; el checksum es evidencia secundaria. La estrategia está resuelta como `stacked-to-main`, con gate pre-merge ≤400 líneas humanas por slice y salida generada aislada. `EX-PLAN-2026-07-10` cubre solo el work unit separado de planificación documental ya aprobado; no cubre S01 ni su registro posterior de evidencia.

Desde la raíz backend, la igualdad primaria se comprueba con:

```bash
git diff --exit-code 1223630ab99bf1bfaa4f5919fccf5ff539379c8e -- specs/001-school-enrollment-management/contracts
test -z "$(git status --porcelain --untracked-files=all -- specs/001-school-enrollment-management/contracts)"
```

Solo después, desde `specs/001-school-enrollment-management/contracts/`, el checksum se reproduce con este orden exacto:

```bash
sha256sum openapi.yaml \
  paths/catalogs.yaml paths/enrollments.yaml \
  paths/teacher-contracts.yaml paths/reports.yaml \
  components/catalogs.yaml components/enrollments.yaml \
  components/teacher-contracts.yaml components/reports.yaml \
  components/problems.yaml | sha256sum
```

La salida esperada es `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a  -`.

## Ruta crítica P0

El pronóstico de una jornada sigue siendo de riesgo alto. El refactor eleva P0 de 8 a 11 tablas y NO autoriza recortar integridad, `database/setup.sql` ni pruebas críticas.

| Orden | Slices/tareas | Salida obligatoria |
| --- | --- | --- |
| 1 | S01–S02 / V2-T005–V2-T019 | solución base, fixture SQL, normalización y harness genérico de auditoría/concurrencia; sin evidencia de tablas futuras |
| 2 | S03–S04 / V2-T020–V2-T031 | cinco tablas de catálogo con evidencia parcial ejecutable; luego Person con roles duales |
| 3 | S05–S07 / V2-T032–V2-T046 | Enrollment, contratos y migración P0 aislada; tras V2-T045, evidencia completa de 11 tablas, triggers, singleton y permisos |
| 4 | S08–S11 / V2-T047–V2-T067 | host, catálogos y US1–US3 end-to-end |
| 5 | S12 / V2-T068–V2-T075 | setup SQL, paridad, runner, walkthroughs y puerta P0 |

P1 solo puede iniciar en V2-T076 después de PASS en V2-T075. Agrega `catalog.Subject`, `academic.TeachingAssignment` y `academic.ClassSchedule`, elevando el total a 14 tablas; `listSubjects` se implementa y prueba explícitamente en V2-T084–V2-T087 antes de los reportes.

## Primer slice autónomo

S01 ya contiene tres proyectos de producción, dos de pruebas y smoke runner HTTP, sin entidades ni migraciones. El scaffold generado se revisa separado de la configuración humana mediante `docs/generated-manifests/s01.txt`. Su rollback elimina solo la solución creada.

## Ejecutar el scaffold S01

```bash
dotnet restore
dotnet build --no-restore --configuration Debug
dotnet test --no-build --no-restore --configuration Debug
dotnet run --project src/Inovait.Api
```

`GET /` devuelve `{"service":"Inovait API","status":"ready"}` y `GET /health` devuelve `{"status":"ok"}`, ambos como `application/json`.

## Preparar persistencia desde S02

Ruta EF futura:

```bash
dotnet restore
dotnet build --no-restore
dotnet ef database update --project src/Inovait.Infrastructure --startup-project src/Inovait.Api
dotnet run --project src/Inovait.Api
```

Ruta de script sobre base vacía:

```bash
sqlcmd -S "$SQLSERVER_HOST" -d "$SQLSERVER_DATABASE" -U "$SQLSERVER_USER" -P "$SQLSERVER_PASSWORD" -i database/setup.sql
dotnet run --project src/Inovait.Api
```

`database/setup.sql` futuro crea schemas, 11 tablas P0, constraints, índices/includes, triggers estrechos, seeds, singleton y permisos runtime; no crea database/login ni contiene credenciales. P1 agrega solo tres tablas y objetos asociados.

## Entorno local validado (S12)

`compose.yaml` levanta SQL Server 2022 CU14 para desarrollo y evaluación local, con `MSSQL_SA_PASSWORD` obligatorio y externo a Git:

```bash
export MSSQL_SA_PASSWORD='<clave-fuerte-propia>'
export INOVAIT_SQL_PORT=14333   # opcional; por defecto 1433
docker compose -f compose.yaml up -d --wait
```

La clave de conexión runtime de la API sigue siendo `ConnectionStrings__InovaitDatabase`:

```bash
export ConnectionStrings__InovaitDatabase="Server=localhost,${INOVAIT_SQL_PORT:-1433};Database=Inovait;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
dotnet run --project src/Inovait.Api -c Release --no-build
```

`TrustServerCertificate=True` en este comando es una variable efímera y local contra el certificado autofirmado del contenedor, con la misma política que el fixture Testcontainers (`MsSqlBuilder`); nunca se versiona en configuración de producción.

`database/setup.sql` se aplica sobre la base vacía con `sqlcmd` (dos ejecuciones consecutivas son un no-op seguro, verificado en el walkthrough de V2-T074).

`./scripts/run-p0-tests.sh`, validado en V2-T071, parsea los 37 IDs del manifest canónico, verifica cada uno con `--list-tests --filter "Priority=P0&Evidence=<ID>"` y solo después corre la suite completa `Priority=P0`; termina con `P0 GATE PASSED: 37/37`.

### Despliegue local integrado (backend + frontend)

Para levantar SQL Server + API + frontend con un solo comando (en vez de los pasos manuales de arriba), usar `./scripts/deploy-local.sh` (Linux/macOS) o `powershell -ExecutionPolicy Bypass -File scripts\deploy-local.ps1` (Windows), ambos en la raíz del repo backend. Siembra datos de demo ficticios (opt-out `--no-demo-data`/`-NoDemoData`) porque el seed canónico no alcanza para probar el formulario de matrícula del frontend, y sirve la API en `http://localhost:5000` y el frontend en `http://localhost:4200`. Detalle completo de parámetros, qué hace cada paso y teardown en el [README](../../README.md#despliegue-local-integrado-backend--frontend).

## Ejecutar pruebas P0 futuras

```bash
./scripts/run-p0-tests.sh
# Solo después del PASS P0 y de completar V2-T076–V2-T100:
./scripts/run-p1-tests.sh
```

Cada runner lista su prioridad y aplica fail-on-missing al manifest canónico de [testing-strategy.md](../../docs/testing-strategy.md) antes de ejecutar. V2-T071 rechaza IDs repetidos en el manifest y exige para cada uno de los 37 IDs P0 al menos una prueba descubierta con `Priority=P0&Evidence=<ID>`; solo después ejecuta toda la suite `Priority=P0`. El piso de 20 casos es secundario. P0 solo consume evidencia producida hasta V2-T070 y P1 solo consume evidencia producida hasta V2-T100. Testcontainers es la ruta relacional primaria. La paridad compara metadatos de dos bases limpias: migración EF y setup SQL.

El orden de evidencia es estricto: S02 prueba únicamente el interceptor/harness genérico; S03 ejecuta `IT-CATALOG-SCHEMA-S03`, `IT-CATALOG-MUTABILITY-S03` e `IT-CATALOG-SINGLETON-S03` solo contra sus cinco tablas. V2-T046, siempre después de V2-T045, produce y ejecuta `IT-SCHEMAS-P0`, `IT-IMMUTABILITY`, `IT-SINGLETON`, `IT-REFERENCE-PERMISSIONS`, `IT-AUDIT-UTC-P0` e `IT-ROWVERSION-P0` cuando ya existen las 11 tablas, triggers, rol y permisos. Los productores API son V2-T047 (`IT-CATALOGS`), V2-T052 (`IT-ENR-CREATE`, `IT-ENR-IDENTITY`, `IT-ENR-CONTEXT`), V2-T053 (`IT-ENR-ATOMIC`), V2-T058 (`IT-ENR-FILTER`) y V2-T062 (`IT-CON-MULTI`, `IT-CON-DATES`, `IT-CON-LIST`, `IT-PROBLEMS`, `IT-OPENAPI-P0`). V2-T069 produce setup/seed y V2-T070 produce una sola vez `IT-INDEXES-P0`; todos preceden al consumer V2-T071. V2-T083 produce `IT-SEED-P1` tras materializar 14 tablas y V2-T087 produce la matriz auditada P1.

## Checklist relacional P0

1. Existen `catalog`, `people`, `academic`, `staff`; `AcademicYear` solo está en `catalog`.
2. Existen 11 tablas P0 y `AcademicConfiguration(Id=1)` referencia un año válido.
3. La aplicación aplica NFC/trim/whitespace Unicode, incluidos tabs/newlines; SQL directo con `LEN(TRIM)>0` rechaza vacío y solo espacios ordinarios, sin sobrestimar su cobertura.
4. Una `Person` puede ser `Student` y `Teacher` sin duplicar identidad.
5. Exactamente `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract` tienen auditoría/check/rowversion; `Enrollment` solo `CreatedAtUtc`; `DocumentType`, `Student` y `AcademicConfiguration` no tienen auditoría genérica, check cronológico ni rowversion.
6. EF y SQL directo rechazan cambios de códigos estables/sector.
7. Cancelación exige sus tres datos; Enrollment conserva FK compuesto y unicidad anual.
8. Los índices tienen nombres, key order e includes definidos en `data-model.md`; `Id` no se repite en INCLUDE mientras la PK permanezca clustered.
9. El principal runtime puede leer `DocumentType`, pero INSERT/UPDATE/DELETE fallan y migration/setup tienen permisos equivalentes.

## Walkthrough P0

1. Consultar escuelas, grados, años, grupos y docentes ficticios.
2. Crear una inscripción con identidad nueva; esperar `Person`+`Student`+`Enrollment` atómicos.
3. Repetir la identidad en otro año; esperar reutilización. Repetir el año; esperar 409.
4. Consultar inscripciones por escuela/grado/año y verificar edad/orden; contexto existente sin grupos devuelve `200 []`.
5. Crear contratos para dos escuelas; esperar dos registros o ninguno.
6. Consultar por docente/escuela y verificar estado persistido/efectivo.

## Walkthrough P1 condicional

Antes del walkthrough, con las 14 tablas ya materializadas, `IT-AUDIT-UTC-P1`/`IT-ROWVERSION-P1` comprueban auditoría/check/rowversion en `Subject` y `TeachingAssignment`, y solo `CreatedAtUtc` en `ClassSchedule`.

1. Consultar `listSubjects` y verificar orden `name, code, id`.
2. Distribución 2/3/7/8/12/13.
3. Docentes distintos por sector, excluyendo cancelaciones según período.
4. Todas las escuelas empatadas en el máximo.
5. Historia anual con múltiples docentes/materias y períodos de asignación compatibles.

## Resultado esperado

- JSON/OpenAPI sin cambios observables; `DocumentTypeId` no se expone.
- Solo datos ficticios, cero secretos y errores `ProblemDetails`.
- Cero persistencia parcial y órdenes deterministas.
- Paridad de 11 tablas en P0 y 14 únicamente después de la puerta P1.
