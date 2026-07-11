# Modelo de datos canónico de producción

## Resultado

El modelo usa 14 tablas en cuatro esquemas SQL Server y conserva P0 antes de P1. `AcademicYear` pertenece de forma autoritativa a `catalog`; la mención aislada `academic.AcademicYear` queda descartada. P0 materializa 11 tablas y P1 agrega `catalog.Subject`, `academic.TeachingAssignment` y `academic.ClassSchedule`.

| Esquema | Tablas |
| --- | --- |
| `catalog` | `School`, `AcademicYear`, `AcademicConfiguration`, `Grade`, `Subject`, `DocumentType` |
| `people` | `Person`, `Student`, `Teacher` |
| `academic` | `ClassGroup`, `Enrollment`, `TeachingAssignment`, `ClassSchedule` |
| `staff` | `TeacherContract` |

No se planifican columnas duplicadas para comparación, agregados de reportes, duplicación de `School`/`Grade` en `Enrollment` ni soft delete genérico. Todas las FK usan `ON DELETE NO ACTION`.

## Convenciones transversales

- PK enteras `IDENTITY`, salvo `catalog.AcademicConfiguration.Id`, `people.Student.PersonId`, `people.Teacher.PersonId` y la PK compuesta de `academic.ClassSchedule`.
- Fechas de negocio: SQL `date` y .NET `DateOnly`. Sellos técnicos: `datetime2(3)` UTC.
- Collation explícita para texto comparable: `Latin1_General_100_CI_AS`, disponible en SQL Server 2022, insensible a mayúsculas y sensible a acentos. Evita que `Jose` y `José` se confundan sin mantener columnas duplicadas.
- `TextNormalizationInterceptor` aplica antes de persistir todo texto Unicode requerido: NFC (`NormalizationForm.FormC`), recorte y colapso de whitespace Unicode interno —incluidos tabs y saltos de línea— a un espacio. Si un required string queda vacío, la aplicación lo rechaza. No elimina puntuación ni diacríticos.
- Todo string requerido conserva un `CHECK` nombrado con `LEN(TRIM([Column])) > 0`; en SQL Server, `TRIM` sin lista de caracteres elimina U+0020 ordinario. Por tanto, esta defensa SQL directa rechaza `''` y valores formados solo por espacios ordinarios, pero no promete rechazar tabs, saltos de línea ni todo whitespace Unicode. Los strings opcionales aplican la misma regla cuando tienen valor; la cobertura Unicode amplia corresponde a la aplicación.

## Auditoría y concurrencia

Exactamente `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`, `Subject` y `TeachingAssignment` contienen:

| Columna | SQL | Regla |
| --- | --- | --- |
| `CreatedAtUtc` | `datetime2(3)` | `DEFAULT SYSUTCDATETIME()` |
| `UpdatedAtUtc` | `datetime2(3)` | `DEFAULT SYSUTCDATETIME()` |
| `RowVersion` | `rowversion` | token de concurrencia EF mediante `IsRowVersion()` |

Cada tabla declara `CK_<Table>_UpdatedAtUtc` con `[UpdatedAtUtc] >= [CreatedAtUtc]`. `AuditSaveChangesInterceptor` deja actuar los defaults en altas y, para entidades modificadas, fija `UpdatedAtUtc` desde `TimeProvider.GetUtcNow()`, preserva `CreatedAtUtc` y no escribe `RowVersion`. Los defaults por sí solos NO actualizan filas. Un conflicto de `RowVersion` se traduce a `409 ProblemDetails`.

`Enrollment` y `ClassSchedule` son hechos históricos/inmutables y solo contienen `CreatedAtUtc DEFAULT SYSUTCDATETIME()`. `DocumentType`, `Student` y `AcademicConfiguration` omiten auditoría genérica intencionalmente: son, respectivamente, referencia estable, marcador de rol y singleton de configuración. No se introduce soft delete.

## Tablas `catalog`

### `catalog.School`

`Id int` PK; `Code varchar(20)`; `Name nvarchar(160)`; `Sector varchar(8)` (`Public|Private`); auditoría/concurrencia.

- `UQ_School_Code`, `UQ_School_Name`.
- `CK_School_Code_NotBlank`, `CK_School_Name_NotBlank`, `CK_School_Sector_NotBlank`, `CK_School_Sector`, `CK_School_UpdatedAtUtc`.
- `Code` y `Sector` son inmutables después del alta. EF usa setter no público y `PropertySaveBehavior.Throw`; SQL usa el trigger estrecho `TR_School_ProtectStableValues`, que solo compara esos dos campos entre `inserted` y `deleted` y ejecuta `THROW` si cambiaron.

### `catalog.AcademicYear`

`Id int` PK; `Code varchar(20)`; `Name nvarchar(80)`; `StartDate date`; `EndDate date`; auditoría/concurrencia.

- `UQ_AcademicYear_Code`, `UQ_AcademicYear_Name`.
- `CK_AcademicYear_Code_NotBlank`, `CK_AcademicYear_Name_NotBlank`, `CK_AcademicYear_DateRange`, `CK_AcademicYear_UpdatedAtUtc`.
- `Code` es inmutable en EF y mediante `TR_AcademicYear_ProtectCode`.
- No existe `IsCurrent`; la fuente única del año actual es `catalog.AcademicConfiguration`.

### `catalog.AcademicConfiguration`

`Id tinyint` PK con `CK_AcademicConfiguration_Singleton ([Id] = 1)`; `CurrentAcademicYearId int` FK `FK_AcademicConfiguration_AcademicYear`.

PK+CHECK garantizan declarativamente **como máximo** una fila, no su existencia. La migración y `database/setup.sql` insertan `Id=1`; la aplicación solo puede leer y cambiar `CurrentAcademicYearId`. El rol de runtime recibe `DENY DELETE` y no recibe `INSERT`; `TR_AcademicConfiguration_PreventDelete` ejecuta `THROW` ante cualquier delete. El principal de migraciones queda separado y controlado. La aplicación falla al iniciar si falta la fila. Esta combinación mantiene exactamente una fila en operación normal sin afirmar que un `CHECK` garantiza existencia; un propietario con DDL siempre conserva capacidad administrativa explícita.

El campo HTTP existente `AcademicYearSummary.isCurrent` se calcula como `AcademicYear.Id == AcademicConfiguration.CurrentAcademicYearId`; no corresponde a una columna de `AcademicYear` ni requiere cambiar OpenAPI.

### `catalog.Grade`

`Id int` PK; `Code varchar(20)`; `Name nvarchar(80)`; `SortOrder smallint`; auditoría/concurrencia.

- `UQ_Grade_Code`, `UQ_Grade_Name`, `UQ_Grade_SortOrder`.
- `CK_Grade_Code_NotBlank`, `CK_Grade_Name_NotBlank`, `CK_Grade_SortOrder`, `CK_Grade_UpdatedAtUtc`.
- `Code` inmutable en EF y mediante `TR_Grade_ProtectCode`.

### `catalog.Subject` (P1)

`Id int` PK; `Code varchar(20)`; `Name nvarchar(120)`; auditoría/concurrencia. `UQ_Subject_Code`, `UQ_Subject_Name`; checks `CK_Subject_Code_NotBlank`, `CK_Subject_Name_NotBlank`, `CK_Subject_UpdatedAtUtc`; `Code` inmutable en EF y mediante `TR_Subject_ProtectCode`.

### `catalog.DocumentType`

`Id smallint` PK; `Code varchar(20)`; `Name nvarchar(80)`; `IsActive bit`.

- `UQ_DocumentType_Code`.
- `CK_DocumentType_Code_NotBlank`, `CK_DocumentType_Name_NotBlank`.
- Se precarga y el runtime solo lo lee. Su ausencia de auditoría responde a su semántica de referencia estable, no a una omisión accidental.

## Tablas `people`

### `people.Person`

`Id int` PK; `DocumentTypeId smallint` FK; `DocumentNumber nvarchar(32)`; `FirstNames nvarchar(120)`; `LastNames nvarchar(120)`; `BirthDate date`; auditoría/concurrencia.

- `UQ_Person_DocumentTypeId_DocumentNumber(DocumentTypeId, DocumentNumber)`; `DocumentNumber` usa `Latin1_General_100_CI_AS`.
- `CK_Person_DocumentNumber_NotBlank`, `CK_Person_FirstNames_NotBlank`, `CK_Person_LastNames_NotBlank`, `CK_Person_UpdatedAtUtc`.
- `IX_Person_LastNames_FirstNames_Id(LastNames, FirstNames, Id) INCLUDE (DocumentTypeId, DocumentNumber, BirthDate)` soporta listas de personas/roles en orden canónico. Aquí `Id` es una key explícita para ordenar, no una columna INCLUDE redundante.

La identidad se almacena una sola vez. El servicio resuelve `DocumentType.Code` a `DocumentTypeId`, normaliza el texto de entrada en aplicación y confía en la unicidad SQL CI_AS para carreras. Una persona puede ser estudiante, docente, ambos o ninguno.

### `people.Student`

`PersonId int` es simultáneamente `PK_Student` y FK `FK_Student_Person` a `people.Person(Id)`. No contiene identidad, auditoría ni estado “actual”. Es un rol uno-a-uno independiente.

### `people.Teacher`

`PersonId int` es simultáneamente `PK_Teacher` y FK `FK_Teacher_Person`; añade auditoría/concurrencia por pertenecer a la lista mutable aprobada. La existencia de `Student(PersonId=X)` no impide `Teacher(PersonId=X)`.

## Tablas `academic`

### `academic.ClassGroup`

`Id int` PK; `SchoolId int`, `AcademicYearId int`, `GradeId int` FK; `Code varchar(20)`; auditoría/concurrencia.

- `UQ_ClassGroup_Context(SchoolId, AcademicYearId, GradeId, Code)`.
- `UQ_ClassGroup_Id_AcademicYear_ForEnrollment(Id, AcademicYearId)` existe únicamente para que SQL Server acepte el FK compuesto de `Enrollment`; no representa una identidad de dominio ni una clave alternativa lógica.
- `CK_ClassGroup_Code_NotBlank`, `CK_ClassGroup_UpdatedAtUtc`.
- `IX_ClassGroup_AcademicYearId_GradeId_SchoolId(AcademicYearId, GradeId, SchoolId) INCLUDE (Code)` soporta contexto anual y reportes; `Id` está disponible implícitamente por la PK clustered. `IX_ClassGroup_GradeId` cubre la FK no liderada por otro índice. `UQ_ClassGroup_Context` ya cubre `SchoolId` y el índice anual cubre `AcademicYearId`.

### `academic.Enrollment`

`Id int` PK; `StudentPersonId int` FK a `people.Student(PersonId)`; `ClassGroupId int`; `AcademicYearId int`; `CreatedAtUtc datetime2(3)`.

- `UQ_Enrollment_StudentPersonId_AcademicYearId(StudentPersonId, AcademicYearId)` impone una inscripción anual concurrentemente.
- `FK_Enrollment_ClassGroupId_AcademicYearId(ClassGroupId, AcademicYearId)` referencia `UQ_ClassGroup_Id_AcademicYear_ForEnrollment` e impide divergencia.
- `IX_Enrollment_ClassGroupId_StudentPersonId(ClassGroupId, StudentPersonId) INCLUDE (AcademicYearId, CreatedAtUtc)` permite seek desde grupos filtrados y cubre la proyección antes del join a `Person`; `Id` está disponible implícitamente por la PK clustered.

`AcademicYearId` es la única dependencia controlada: se conserva para imponer la unicidad anual sin trigger entre tablas. No se duplican `SchoolId`, `GradeId` ni agregados.

### `academic.TeachingAssignment` (P1)

`Id int` PK; `TeacherContractId int`, `ClassGroupId int`, `SubjectId int` FK; `StartDate date`; `EndDate date NULL`; auditoría/concurrencia.

- `UQ_TeachingAssignment_Contract_Group_Subject`.
- `CK_TeachingAssignment_DateRange`, `CK_TeachingAssignment_UpdatedAtUtc`.
- `IX_TeachingAssignment_ClassGroupId_StartDate_EndDate(ClassGroupId, StartDate, EndDate) INCLUDE (TeacherContractId, SubjectId)` cubre historia por grupo/período; `Id` llega por la PK clustered.
- `IX_TeachingAssignment_TeacherContractId_StartDate_EndDate(TeacherContractId, StartDate, EndDate) INCLUDE (ClassGroupId, SubjectId)` cubre validación/lista por contrato; `Id` llega por la PK clustered.
- `IX_TeachingAssignment_SubjectId` preserva soporte de FK sin duplicar los dos índices anteriores.

La aplicación valida dentro de la misma transacción: escuela de contrato=escuela de grupo; inicio dentro de contrato y año; y fin efectivo (`EndDate` o `AcademicYear.EndDate`) no posterior al fin del año, del contrato ni a `CancellationEffectiveDate` cuando corresponda. SQL solo puede garantizar existencia y rango local sin duplicar escuela/fechas.

### `academic.ClassSchedule` (P1)

`TeachingAssignmentId int` FK; `Weekday tinyint`; `CreatedAtUtc datetime2(3)`; PK `PK_ClassSchedule(TeachingAssignmentId, Weekday)`; `CK_ClassSchedule_Weekday`. La aplicación inserta al menos un día junto con la asignación; SQL valida atomicidad y valores 1..7, pero no existencia de hijos sin un trigger injustificado.

## Tabla `staff`

### `staff.TeacherContract`

`Id int` PK; `TeacherPersonId int` FK a `people.Teacher(PersonId)`; `SchoolId int` FK; `StartDate date`; `EndDate date NULL`; `Status varchar(10)`; `CancelledAtUtc datetime2(3) NULL`; `CancellationReason nvarchar(300) NULL`; `CancellationEffectiveDate date NULL`; auditoría/concurrencia.

- `CK_TeacherContract_DateRange`; `CK_TeacherContract_Status_NotBlank`; `CK_TeacherContract_Status`; `CK_TeacherContract_StatusCancellation`; `CK_TeacherContract_CancellationReason_NotBlank`; `CK_TeacherContract_CancellationEffectiveDate`; `CK_TeacherContract_UpdatedAtUtc`.
- Regla elegida: `Confirmed` exige los tres campos de cancelación en `NULL`; `Cancelled` exige los tres con razón no vacía. La fecha efectiva debe estar entre `StartDate` y `EndDate` cuando este exista.
- `UQ_TeacherContract_Exact(TeacherPersonId, SchoolId, StartDate, EndDate)` evita duplicados exactos, incluidos contratos abiertos conforme a la semántica de `UNIQUE` de SQL Server.
- `IX_TeacherContract_TeacherPersonId_StartDate_EndDate(TeacherPersonId, StartDate, EndDate) INCLUDE (SchoolId, Status, CancelledAtUtc, CancellationReason, CancellationEffectiveDate)` cubre lista/historia por docente; `Id` llega por la PK clustered.
- `IX_TeacherContract_SchoolId_StartDate_EndDate(SchoolId, StartDate, EndDate) INCLUDE (TeacherPersonId, Status, CancellationEffectiveDate)` cubre lista por escuela y reporte de sector; `Id` llega por la PK clustered. No se agrega un índice filtrado `Confirmed` hasta que mediciones justifiquen mantener un tercer árbol casi solapado.

La superposición inclusiva para docente+escuela se valida con consulta indexada dentro de transacción `Serializable`; el `UNIQUE` resuelve duplicados exactos. No se usa trigger para comparar períodos entre filas.

### Supuesto de clustering

Los INCLUDE anteriores suponen que las PK `Id` permanecen clustered, que es el comportamiento predeterminado planificado de SQL Server/EF; la clustered key queda disponible en cada índice nonclustered sin declararla en INCLUDE. Si cualquier PK pasa a nonclustered o cambia la clustered key, se deben re-evaluar todos los índices y sus pruebas de metadata antes de integrar ese cambio.

## Prueba de normalización relacional

| Tabla | Forma | Prueba resumida |
| --- | --- | --- |
| `School`, `AcademicYear`, `Grade`, `Subject`, `DocumentType` | BCNF | `Id` y cada `Code` único son claves candidatas; todo atributo depende de una clave completa. |
| `AcademicConfiguration` | BCNF | `Id` es la única clave y determina el año actual; el singleton no duplica datos del año. |
| `Person` | BCNF | `Id` y `(DocumentTypeId, DocumentNumber)` son claves candidatas; nombres y nacimiento dependen de la persona. |
| `Student`, `Teacher` | BCNF | `PersonId` determina la fila de rol; no repiten identidad. Los roles independientes eliminan la duplicación anterior. |
| `ClassGroup` | BCNF | `Id` y contexto+code son claves; el `UNIQUE(Id, AcademicYearId)` contiene la superclave `Id` y solo soporta FK. |
| `Enrollment` | 3NF, no BCNF | `ClassGroupId → AcademicYearId` tiene determinante no superclave; `AcademicYearId` es primo en `(StudentPersonId, AcademicYearId)`. El FK compuesto cierra la dependencia. Se conserva para unicidad anual concurrente sin trigger entre tablas. |
| `TeacherContract` | BCNF | contrato e identidad exacta determinan fechas, estado, cancelación y auditoría; no almacena vigencia derivada. |
| `TeachingAssignment` | BCNF | `Id` y contrato+grupo+materia determinan período; escuela/año no se duplican. |
| `ClassSchedule` | BCNF | la PK completa determina `CreatedAtUtc`; cada weekday es atómico. |

El resultado global cumple 3NF. La única desviación de BCNF es explícita, controlada y comprobable en `Enrollment`.

## Responsabilidades EF Core y archivos futuros

| Ruta futura | Responsabilidad exacta |
| --- | --- |
| `src/Inovait.Core/Domain/Catalogs/{School,AcademicYear,AcademicConfiguration,Grade,Subject,DocumentType}.cs` | entidades y mutabilidad de dominio |
| `src/Inovait.Core/Domain/People/{Person,Student,Teacher}.cs` | identidad única y roles independientes |
| `src/Inovait.Core/Domain/Academics/{ClassGroup,Enrollment,TeachingAssignment,ClassSchedule}.cs` | historia académica y períodos |
| `src/Inovait.Core/Domain/Staff/TeacherContract.cs` | período y transición Confirmed→Cancelled |
| `src/Inovait.Core/Domain/Common/ITextNormalizer.cs` | contrato de NFC/trim/whitespace |
| `src/Inovait.Infrastructure/Persistence/InovaitDbContext.cs` | DbSets y aplicación de configuraciones |
| `src/Inovait.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` | una configuración Fluent por entidad: schema/table, tipos, collation, defaults, checks, keys, FK `NoAction`, índices e includes |
| `src/Inovait.Infrastructure/Persistence/Interceptors/TextNormalizationInterceptor.cs` | normalizar propiedades requeridas antes de INSERT/UPDATE |
| `src/Inovait.Infrastructure/Persistence/Interceptors/AuditSaveChangesInterceptor.cs` | actualizar `UpdatedAtUtc` en entidades modificadas |
| `src/Inovait.Infrastructure/Persistence/Seed/ProductionCatalogSeed.cs` | referencias ficticias, códigos estables y singleton Id=1 |
| `tests/Inovait.UnitTests/Domain/` | normalización, roles, cancelación y períodos |
| `tests/Inovait.IntegrationTests/Persistence/` | SQL Server real: metadatos, collation, triggers, concurrencia, índices y paridad |

Cada configuración usa `ToTable(name, schema)`, tipos/longitudes explícitos, `UseCollation`, `HasDefaultValueSql("SYSUTCDATETIME()")`, `IsRowVersion`, `HasCheckConstraint`, nombres exactos de keys/FK/indexes, `IncludeProperties` y `DeleteBehavior.NoAction`. La inmutabilidad EF de códigos y sector usa acceso restringido más `SetAfterSaveBehavior(PropertySaveBehavior.Throw)`; los triggers son la defensa SQL ante clientes ajenos a EF.

## Responsabilidades futuras de `database/setup.sql`

Sobre una base vacía, sin crear database/login ni guardar credenciales: activar `XACT_ABORT`, abrir `TRY/CATCH`+transacción; crear schemas; crear 11 tablas P0 en orden; declarar tipos, collations, defaults, PK/FK/UNIQUE/CHECK; crear índices y cuatro triggers P0 estrechos; insertar catálogos ficticios y `AcademicConfiguration(Id=1)`; crear/configurar el rol database `[inovait_runtime]` sin login con permisos mínimos, `DENY DELETE`/sin `INSERT` sobre el singleton, `GRANT SELECT ON OBJECT::catalog.DocumentType TO [inovait_runtime]` y `DENY INSERT, UPDATE, DELETE ON OBJECT::catalog.DocumentType TO [inovait_runtime]`; validar conteos y permisos; confirmar o revertir y relanzar. P1 agrega el quinto trigger para `Subject.Code`.

La cadena EF separa `InitialP0ProductionModel` generado del migration manual `AddP0DatabaseProtections`, limitado a triggers y permisos que el modelo no genera. P1 usa el mismo patrón con `AddP1TeachingModel` generado y `AddP1DatabaseProtections` para `TR_Subject_ProtectCode`. El conjunto de migraciones y el script deben tener paridad de schemas, tablas, columnas, tipos, defaults, collation, constraints, índices, includes, filtros, triggers, seeds y permisos relevantes. Las pruebas comparan metadatos de `sys.*`, no texto generado. El script no será destructivo ni pseudo-idempotente: recreará una base limpia de evaluación.
