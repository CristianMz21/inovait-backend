# Arquitectura planificada

## Decisión

El backend será un monolito modular REST con tres proyectos de producción. El refactor ubica 14 tablas en `catalog`, `people`, `academic` y `staff`; `catalog.AcademicYear` es la única ubicación válida. No cambia el contrato HTTP canónico.

```text
inovait-frontend
      │ HTTPS + camelCase JSON + ProblemDetails
      ▼
Inovait.Api ──► Inovait.Core ◄── Inovait.Infrastructure ──► SQL Server 2022
 Controllers    Domain/Features      EF/config/interceptors
```

| Proyecto | Responsabilidad | No contiene |
| --- | --- | --- |
| `Inovait.Api` | Controllers, DTOs, binding, ProblemDetails, OpenAPI, DI | entidades EF, reglas SQL |
| `Inovait.Core` | entidades, roles, invariantes, casos de uso y puertos específicos | ASP.NET, EF, SQL Server |
| `Inovait.Infrastructure` | DbContext, Fluent API, interceptors, migraciones, seeds, transacciones y queries | DTOs HTTP |

## Organización futura exacta

```text
src/Inovait.Core/Domain/
├── Catalogs/{School,AcademicYear,AcademicConfiguration,Grade,Subject,DocumentType}.cs
├── People/{Person,Student,Teacher}.cs
├── Academics/{ClassGroup,Enrollment,TeachingAssignment,ClassSchedule}.cs
├── Staff/TeacherContract.cs
└── Common/{ITextNormalizer,IAuditableEntity}.cs
src/Inovait.Infrastructure/Persistence/
├── InovaitDbContext.cs
├── Configurations/<Entity>Configuration.cs
├── Interceptors/{TextNormalizationInterceptor,AuditSaveChangesInterceptor}.cs
├── Migrations/
└── Seed/ProductionCatalogSeed.cs
```

Una configuración Fluent por entidad declara schema/table, tipos, longitudes, `Latin1_General_100_CI_AS`, defaults, checks, claves/FK `NoAction`, índices/includes y `IsRowVersion()`. No hay `Generic Repository`, buses ni handlers uno-a-uno.

## Flujo de escritura

1. Api valida forma y tipos y propaga `CancellationToken`.
2. Core resuelve `DocumentType.Code`, valida referencias e invariantes.
3. `TextNormalizationInterceptor` aplica NFC, trim y colapso de whitespace Unicode —incluidos tabs/newlines— a texto requerido; nunca crea columnas duplicadas de comparación.
4. `AuditSaveChangesInterceptor` actualiza `UpdatedAtUtc` solo en entidades modificadas y preserva `CreatedAtUtc`; SQL aplica defaults en altas.
5. EF emite `UPDATE` con PK+`RowVersion`; cero filas afectadas se traduce a conflicto de concurrencia.
6. Escrituras atómicas confirman una única transacción; lecturas usan proyección no tracking.

## Persona y roles

`people.Person` contiene la identidad documental una sola vez. `people.Student.PersonId` y `people.Teacher.PersonId` son PK+FK independientes; una misma persona puede tener ambos roles. La unicidad CI_AS de `(DocumentTypeId, DocumentNumber)` es la defensa concurrente. La aplicación conserva diacríticos y no elimina puntuación.

## Fronteras SQL y aplicación

| Regla | SQL Server | Aplicación |
| --- | --- | --- |
| texto | `LEN(TRIM)>0` rechaza vacío/U+0020-only en SQL directo; collation y UNIQUE | NFC/trim y colapso/rechazo de whitespace Unicode antes de persistir |
| códigos y `School.Sector` inmutables | triggers estrechos | setter restringido + `PropertySaveBehavior.Throw` |
| año actual/referencias | PK+CHECK permiten máximo uno; seed, permisos y trigger evitan ausencia operativa; `DocumentType` tiene SELECT y DENY de INSERT/UPDATE/DELETE para runtime | singleton con cambio acotado de FK y fail-fast; `DocumentType` solo lectura |
| cancelación | CHECK exige tres datos en `Cancelled` y nulos en `Confirmed` | transición y mensaje de negocio |
| asignación | FK y rango local | misma escuela y período contenido en contrato/año, dentro de transacción |
| contrato superpuesto | UNIQUE exacto | consulta indexada con aislamiento `Serializable` |

Los triggers se limitan a cuatro protecciones de código/sector y a impedir delete del singleton. No se usan para normalizar texto, actualizar timestamps, solapamientos ni reglas entre tablas.

## Atomicidad y concurrencia

- `createEnrollment`: resuelve/crea `Person` y rol `Student`, luego `Enrollment` en una transacción. El FK compuesto y los UNIQUE de identidad/año convierten carreras en `409`.
- `createTeacherContracts`: valida toda la selección, usa `Serializable`, crea contratos independientes o ninguno.
- `TeachingAssignment`: carga contrato, grupo y año con locking/transacción de escritura y valida escuela y contención temporal antes de insertar asignación y horarios.
- `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`, `Subject` y `TeachingAssignment` usan creación/actualización UTC y `rowversion`; `Enrollment` y `ClassSchedule` solo registran creación, y `DocumentType`, `Student`, `AcademicConfiguration` no reciben auditoría genérica ni rowversion.

## Persistencia y despliegue

La cadena EF separa el scaffold generado (`InitialP0ProductionModel`/`AddP1TeachingModel`) de migrations manuales mínimos para triggers/permisos. Esa cadena y `database/setup.sql` deben producir los mismos schemas, tablas, tipos, collations, defaults, constraints, índices/includes, triggers, seeds y permisos, incluidos los DENY de `DocumentType`. La paridad se verifica mediante catálogos `sys.*`, no comparando texto SQL. P0 crea 11 tablas; P1 agrega `Subject`, `TeachingAssignment` y `ClassSchedule`.

## Contrato y seguridad

El bundle OpenAPI mantiene sus 15 `operationId`: el refactor cambia persistencia, no comportamiento HTTP aprobado. Los códigos de `DocumentType` se proyectan como `documentType`, por lo que no se expone `DocumentTypeId`. Connection strings llegan por entorno; seeds son ficticios; logging omite identidad documental. No hay autenticación, borrado histórico ni soft delete genérico.

## Entrega

La estrategia aprobada es `stacked-to-main`: cada slice apunta a `main`, depende solo de slices ya integrados y mantiene ≤400 líneas humanas. Cada slice ejecuta antes de merge el gate determinista de additions+deletions definido en `tasks.md`; scaffold, lockfiles y migraciones generadas quedan antes de `HUMAN_BASE`. S03/S07/S12/S13 tienen fallbacks predefinidos y no existe `size:exception` aprobado. Pruebas y documentación permanecen con la conducta que verifican. P1 continúa bloqueado hasta la puerta P0.
