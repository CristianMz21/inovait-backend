# Modelo de datos canónico

## Decisiones transversales

- SQL Server 2022, nombres técnicos en inglés, PK enteras `IDENTITY` salvo `ClassSchedule`.
- Fechas de negocio: SQL `date`, .NET `DateOnly`, OpenAPI `string/date`. No se planifican timestamps técnicos.
- Todas las FK usan `ON DELETE NO ACTION`; no existen endpoints de borrado histórico.
- Strings requeridos se recortan; longitudes y conjuntos cerrados se validan en API y SQL.
- Los reportes son consultas sobre historia normalizada; no existen tablas ni columnas de agregados.
- No existe una tabla de oferta académica adicional. Cualquier School/Grade/AcademicYear existente es un contexto de consulta válido; `ClassGroup` solo representa grupos realmente creados y su ausencia produce resultados vacíos.

## Tablas

### `School`

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | `int` | PK |
| `Name` | `nvarchar(160)` | requerido |
| `Sector` | `varchar(8)` | `Public` o `Private` |

`UQ_School_Name(Name)`; `CK_School_Sector`. `Sector` usa el mismo vocabulario en enum C#, JSON y SQL; una lookup agregaría administración inexistente.

### `AcademicYear`

| Columna | Tipo | Regla |
| --- | --- | --- |
| `Id` | `int` | PK |
| `Name` | `nvarchar(20)` | requerido, por ejemplo `2026` |
| `StartDate` | `date` | requerido |
| `EndDate` | `date` | requerido |
| `IsCurrent` | `bit` | requerido, default 0 |

`UQ_AcademicYear_Name`; `CK_AcademicYear_DateRange(EndDate >= StartDate)`; índice filtrado único `UX_AcademicYear_Current(IsCurrent) WHERE IsCurrent = 1`. Es tabla y no entero libre porque aporta identidad estable, límites temporales, historia y año actual inequívoco.

### `Grade`

`Id int` PK, `Name nvarchar(80)` requerido y `SortOrder smallint` requerido positivo. `UQ_Grade_Name`, `UQ_Grade_SortOrder`, `CK_Grade_SortOrder(SortOrder > 0)`.

### `ClassGroup`

`Id int` PK; `SchoolId`, `AcademicYearId`, `GradeId` FK requeridas; `Code nvarchar(20)` requerido. `UQ_ClassGroup_Context(SchoolId, AcademicYearId, GradeId, Code)`. Se agrega `AK_ClassGroup_Id_AcademicYear(Id, AcademicYearId)` para el FK compuesto de `Enrollment`. Índices `IX_ClassGroup_AcademicYear_Grade_School(AcademicYearId, GradeId, SchoolId)` y FK individuales cuando no queden cubiertas.

### `Student`

`Id int` PK; `DocumentType nvarchar(20)`, `DocumentNumber nvarchar(32)`, `NormalizedDocumentType nvarchar(20)`, `NormalizedDocumentNumber nvarchar(32)`, `FirstNames nvarchar(120)`, `LastNames nvarchar(120)`, `NormalizedFirstNames nvarchar(120)`, `NormalizedLastNames nvarchar(120)` y `BirthDate date`, todos requeridos. `UQ_Student_NormalizedDocument(NormalizedDocumentType, NormalizedDocumentNumber)` e índice `IX_Student_Name(LastNames, FirstNames, Id)`.

La aplicación calcula valores normalizados antes de persistir. Documento: trim, tipo/alfanumérico en mayúsculas y número sin espacios, puntos ni guiones. Nombres: trim, espacios internos colapsados y comparación case-insensitive conservando diacríticos. El SQL único es la defensa concurrente de identidad.

### `Enrollment`

`Id int` PK; `StudentId int` FK; `ClassGroupId int`; `AcademicYearId int`. `UQ_Enrollment_Student_AcademicYear(StudentId, AcademicYearId)`. FK compuesto `FK_Enrollment_ClassGroup_Context(ClassGroupId, AcademicYearId) → ClassGroup(Id, AcademicYearId)`; índice `IX_Enrollment_ClassGroup(ClassGroupId)`.

`AcademicYearId` es una proyección redundante deliberada: permite a SQL Server imponer una inscripción anual aun cuando el año se obtiene del grupo. No se acepta como verdad independiente; el FK compuesto obliga a que coincida con `ClassGroup.AcademicYearId`. No se duplican `SchoolId` ni `GradeId`.

### `Teacher`

Misma estrategia documental que `Student`: `Id`, campos de documento originales/normalizados y nombres originales/normalizados. `UQ_Teacher_NormalizedDocument` e `IX_Teacher_Name`. Los registros se precargan con datos ficticios.

### `TeacherContract`

`Id int` PK; `TeacherId int` y `SchoolId int` FK; `StartDate date`; `EndDate date` nullable; `Status varchar(10)` requerido con `Confirmed|Cancelled`.

- `CK_TeacherContract_DateRange(EndDate IS NULL OR EndDate >= StartDate)`.
- `CK_TeacherContract_Status`.
- `UX_TeacherContract_Exact(TeacherId, SchoolId, StartDate, EndDate)` evita duplicados exactos. Debe configurarse sin filtro `IS NOT NULL` (`HasFilter(null)`), de modo que SQL Server admita solo un fin nulo por combinación.
- `IX_TeacherContract_Overlap(TeacherId, SchoolId, StartDate, EndDate)` soporta validación serializable.
- `IX_TeacherContract_School_Status_Dates(SchoolId, Status, StartDate, EndDate)` soporta listas y reporte.

Un `CHECK` no compara filas. Toda superposición inclusiva, sin excepción por estado, se valida en aplicación dentro de transacción `Serializable`; otra escuela sí puede superponerse. Se rechaza un trigger por lógica oculta, errores menos claros y mayor costo de prueba.

Estado efectivo para fecha `d`: `Cancelled` domina; un `Confirmed` es `Upcoming`, `Effective` o `Expired` según su intervalo. Reportes filtran `Status = Confirmed` y la intersección de fechas; nunca confían en “current” persistido.

### `Subject`

`Id int` PK, `Code nvarchar(20)` y `Name nvarchar(120)` requeridos. `UQ_Subject_Code` y `UQ_Subject_Name`.

### `TeachingAssignment`

`Id int` PK; `TeacherContractId`, `ClassGroupId`, `SubjectId` FK requeridas. `UQ_TeachingAssignment_Contract_Group_Subject`; `IX_TeachingAssignment_ClassGroup_Subject(ClassGroupId, SubjectId, Id)`.

SQL garantiza existencia, pero sin duplicar escuela/fechas no puede garantizar que contrato y grupo compartan escuela ni que sus intervalos sean compatibles. La aplicación valida: `TeacherContract.SchoolId = ClassGroup.SchoolId` y contrato/año tienen al menos un día de intersección inclusiva. Esta regla permite docentes de parte del año sin inventar fechas de asignación.

### `ClassSchedule`

`TeachingAssignmentId int` FK y `Weekday tinyint`; PK compuesta `PK_ClassSchedule(TeachingAssignmentId, Weekday)` y `CK_ClassSchedule_Weekday(Weekday BETWEEN 1 AND 7)`, donde 1=lunes y 7=domingo. No se almacenan horas. La aplicación exige al menos un día antes de confirmar una asignación; SQL no puede exigir una fila hija durante la inserción sin trigger.

## Normalización

| Tabla | Forma | Justificación |
| --- | --- | --- |
| `School`, `AcademicYear`, `Grade`, `Student`, `Teacher`, `Subject` | BCNF | cada determinante declarado es clave candidata; no hay atributos multivaluados |
| `ClassGroup` | BCNF | contexto+code y `Id` determinan los atributos; el AK compuesto se usa para integridad referencial |
| `Enrollment` | 3NF, no BCNF | `ClassGroupId → AcademicYearId`; `ClassGroupId` no es superclave, pero `AcademicYearId` es atributo primo de la clave candidata `(StudentId, AcademicYearId)`. La dependencia viola BCNF, no 3NF, y queda cerrada por FK compuesto |
| `TeacherContract` | BCNF | los datos contractuales dependen del contrato o de la clave candidata exacta; estado no representa tiempo |
| `TeachingAssignment` | BCNF | el triple contrato/grupo/materia es clave candidata; escuela y año no se duplican |
| `ClassSchedule` | BCNF | toda la fila es la PK; un weekday es un valor atómico, no una lista |

## Fronteras de validación

| Frontera | Responsabilidad principal |
| --- | --- |
| API | required, formato, longitudes, arrays no vacíos, pares de fechas y weekdays 1..7 |
| Domain | normalización, identidad equivalente, edad, rangos, intersección, estado efectivo |
| Application | referencias, contexto school/grade/year/group, escuela/tiempo de asignación, request multiescuela completa |
| SQL Server | PK/FK/AK, unique, check, concurrencia de identidad/año/duplicado y borrado restrictivo |

## Seeds ficticios requeridos

P0 usa únicamente School, AcademicYear, Grade, ClassGroup, Student, Enrollment, Teacher y TeacherContract, con varios años, grupos, escuelas de ambos sectores y contratos abiertos/cerrados. P1 condicional agrega Subject, TeachingAssignment, ClassSchedule, edades límite 2/3/7/8/12/13, empate planificado, docente multisector e historia con múltiples docentes/materias. `database/setup.sql` no existe durante planificación.

## Plan exacto para `database/setup.sql`

Antes de la puerta P0, `database/setup.sql` deberá: (1) exigir una base vacía sin crear login ni almacenar credenciales; (2) activar `SET XACT_ABORT ON` y abrir transacción con `TRY/CATCH`; (3) crear las 8 tablas P0 en orden de dependencias; (4) declarar PK, FK `NO ACTION`, AK, UNIQUE, CHECK e índices aplicables; (5) insertar seeds P0 deterministas; y (6) confirmar o revertir y relanzar el error. P1 podrá extender el mismo script después de la puerta con Subject, TeachingAssignment, ClassSchedule y sus seeds.

La validación P0 aplicará el script a SQL Server 2022 vacío, comprobará 8 tablas y metadatos, ejecutará IT-SEED-P0/IT-SQL-SCRIPT y el walkthrough P0, y comparará con la migración P0. La validación P1, si se autoriza, elevará la expectativa a 11 tablas. La reproducibilidad significa recrear una base limpia; no se agrega lógica destructiva o pseudo-idempotente.
