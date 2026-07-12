-- Fictitious LOCAL-EVALUATION demo data (strict evaluator dataset). NEVER
-- part of the production seed. Supersedes the old database/demo-data.sql
-- (1 school / 1 group / 1 teacher) with a dataset sized to exercise every
-- endpoint and all five report questions deterministically.
--
-- Single idempotent T-SQL batch (no GO): every block is either guarded by
-- IF NOT EXISTS on a stable natural key (Code / DocumentNumber) or is a
-- set-based INSERT ... SELECT ... WHERE NOT EXISTS anti-join on the same
-- kind of natural key, so re-running this script is always a safe no-op
-- (see IT-SEED-DEMO-IDEMPOTENT). All IDs are resolved by natural key into
-- variables -- no IDENTITY value is ever hardcoded. Pure ASCII; accented
-- Spanish characters are produced with NCHAR() so the file survives every
-- encoding layer (sqlcmd, docker cp, git).
--
-- currentYear is NEVER wall-clock: it is read from catalog.AcademicYear via
-- catalog.AcademicConfiguration(Id=1) (the canonical "current year" pointer
-- P0 already protects). DEMO-AY-<currentYear-1> and DEMO-AY-<currentYear-2>
-- are created (full Jan1-Dec31 calendars) as the two historical years this
-- dataset needs. All student birthdates and contract dates are expressed
-- relative to GETDATE(), so ages/effective-status stay correct no matter
-- which day this script actually runs.
--
-- ============================================================================
-- ENROLLMENT MATRIX (designed on paper before writing SQL -- do not edit the
-- data below without re-deriving this table; the self-verification block at
-- the end of this script THROWs if the invariants stop holding)
-- ============================================================================
-- 24 students, ages split exactly 8/8/8 across the report buckets [3-7],
-- [8-12], [13+] (boundary ages 3, 7, 8, 12 and 13 all present). Every
-- multi-year student stays in a single school across all its years; grade
-- always progresses by exactly +1 level per year (age idx = age-3, clamped
-- to [0,13] over the 14-grade catalog DEMO-PJ..DEMO-G11); currentYear ("y")
-- distribution across the 4 schools is exactly 10/6/5/3
-- (COL-PUB-001/COL-PUB-002/COL-PRI-001/COL-PRI-002) and Quinto (DEMO-G05)
-- @ COL-PUB-001 @ y contains EXACTLY 3 enrollments.
--
-- Doc            Age  School      Years  Grade progression (y-2 -> y-1 -> y)            Group(s)
-- DEMO-EST-001    3   COL-PUB-001  1     -> Prejardin                                    A
-- DEMO-EST-002    5   COL-PUB-002  1     -> Transicion                                    A
-- DEMO-EST-003    6   COL-PRI-001  2     Transicion -> Primero                            A / A
-- DEMO-EST-004    4   COL-PUB-001  1     -> Jardin                                        A
-- DEMO-EST-005    7   COL-PRI-001  1     -> Segundo                                       A
-- DEMO-EST-006   10   COL-PUB-001  3     Tercero -> Cuarto -> Quinto  [FIXED CASE]        A / B / A
-- DEMO-EST-007    4   COL-PRI-002  1     -> Jardin                                        A
-- DEMO-EST-008    6   COL-PUB-002  2     Transicion -> Primero                            A / A
-- DEMO-EST-009    5   COL-PUB-001  1     -> Transicion                                    A
-- DEMO-EST-010    8   COL-PUB-002  3     Primero -> Segundo -> Tercero                    A / A / A
-- DEMO-EST-011    9   COL-PUB-001  1     -> Quinto (repeater, completes the Quinto trio)  A
-- DEMO-EST-012   10   COL-PUB-001  1     -> Quinto                                        A
-- DEMO-EST-013   11   COL-PUB-002  2     Quinto -> Sexto                                  A / A
-- DEMO-EST-014   12   COL-PRI-001  3     Quinto -> Sexto -> Septimo                       A / A / A
-- DEMO-EST-015    9   COL-PUB-001  1     -> Cuarto                                        A
-- DEMO-EST-016   11   COL-PUB-001  1     -> Sexto                                         A
-- DEMO-EST-017   13   COL-PUB-001  1     -> Octavo                                        A
-- DEMO-EST-018   14   COL-PUB-002  3     Septimo -> Octavo -> Noveno                      A / A / A
-- DEMO-EST-019   13   COL-PRI-001  2     Septimo -> Octavo                                A / A
-- DEMO-EST-020   15   COL-PUB-001  1     -> Decimo                                        A
-- DEMO-EST-021   16   COL-PRI-002  3     Noveno -> Decimo -> Undecimo                     A / A / A
-- DEMO-EST-022   14   COL-PUB-002  1     -> Noveno                                        A
-- DEMO-EST-023   17   COL-PRI-001  1     -> Undecimo                                      A
-- DEMO-EST-024   15   COL-PRI-002  3     Octavo -> Noveno -> Decimo                       A / A / A
--
-- Totals: 14 x 1yr + 4 x 2yr + 6 x 3yr = 24 students / 40 enrollments.
-- currentYear ("y") school split: COL-PUB-001=10, COL-PUB-002=6,
-- COL-PRI-001=5, COL-PRI-002=3 (top school = COL-PUB-001, per REQ report).
-- Quinto@COL-PUB-001@y = EST-006 + EST-011 + EST-012 = exactly 3.
--
-- Teachers (DEMO-DOC-001..008), all Confirmed contracts:
-- DOC-001 COL-PUB-001 active since <= y-2 (covers EST-006's y-2 assignment)
-- DOC-002 COL-PUB-002 active
-- DOC-003 COL-PRI-001 active
-- DOC-004 COL-PRI-002 active
-- DOC-005 COL-PUB-001 + COL-PRI-001 simultaneous active since <= y-1 (covers EST-006's y assignment)
-- DOC-006 COL-PUB-001 + COL-PUB-002 simultaneous active since <= y-2 (covers EST-006's y-1 assignment)
-- DOC-007 COL-PUB-002 expired (EndDate < today)
-- DOC-008 COL-PRI-001 future (StartDate > today)
-- => 4 public-active teachers, 3 private-active teachers, 6 distinct active
--    teachers, 8 active contract rows, 10 contract rows total.
--
-- EST-006 teaching assignments (one per historical year, distinct teacher):
-- y-2 Tercero-A  -> DOC-001 / Matematicas
-- y-1 Cuarto-B   -> DOC-006 / Lenguaje
-- y   Quinto-A   -> DOC-005 / Matematicas
-- ============================================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    -- ------------------------------------------------------------------
    -- 1) DocumentTypes DNI/PAS/CE (frontend enrollment form examples)
    -- ------------------------------------------------------------------
    -- NOTE: every varchar/nvarchar/char column below is pinned to
    -- COLLATE Latin1_General_100_CI_AS to match the collation EF Core assigns to
    -- Code/Name/DocumentNumber columns (see the model snapshot); without this a table
    -- variable column defaults to the instance collation and comparisons/joins against
    -- real catalog columns throw "Cannot resolve the collation conflict".
    DECLARE @DocTypes TABLE (Code varchar(20) COLLATE Latin1_General_100_CI_AS, Name nvarchar(80));
    INSERT @DocTypes (Code, Name) VALUES
        ('DNI', N'Documento Nacional de Identidad'),
        ('PAS', N'Pasaporte'),
        ('CE', N'C' + NCHAR(233) + N'dula de Extranjer' + NCHAR(237) + N'a');

    INSERT INTO [catalog].[DocumentType] ([Code],[Name],[IsActive])
        SELECT d.Code, d.Name, 1
        FROM @DocTypes d
        WHERE NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] x WHERE x.[Code] = d.Code);
    PRINT CONCAT('DocumentType: seeded ', @@ROWCOUNT, ' of 3 candidate row(s).');

    DECLARE @DniId smallint = (SELECT [Id] FROM [catalog].[DocumentType] WHERE [Code] = 'DNI');
    IF @DniId IS NULL THROW 51100, 'Demo seed precondition failed: DocumentType DNI could not be resolved.', 1;

    -- ------------------------------------------------------------------
    -- 2) Grades DEMO-PJ..DEMO-G11 (14 rows, SortOrder 100-113)
    -- ------------------------------------------------------------------
    DECLARE @Grades TABLE (Code varchar(20) COLLATE Latin1_General_100_CI_AS, Name nvarchar(80), SortOrder smallint);
    INSERT @Grades (Code, Name, SortOrder) VALUES
        ('DEMO-PJ',  N'Prejard' + NCHAR(237) + N'n', 100),
        ('DEMO-JI',  N'Jard' + NCHAR(237) + N'n', 101),
        ('DEMO-TR',  N'Transici' + NCHAR(243) + N'n', 102),
        ('DEMO-G01', N'Primero', 103),
        ('DEMO-G02', N'Segundo', 104),
        ('DEMO-G03', N'Tercero', 105),
        ('DEMO-G04', N'Cuarto', 106),
        ('DEMO-G05', N'Quinto', 107),
        ('DEMO-G06', N'Sexto', 108),
        ('DEMO-G07', N'S' + NCHAR(233) + N'ptimo', 109),
        ('DEMO-G08', N'Octavo', 110),
        ('DEMO-G09', N'Noveno', 111),
        ('DEMO-G10', N'D' + NCHAR(233) + N'cimo', 112),
        ('DEMO-G11', N'Und' + NCHAR(233) + N'cimo', 113);

    INSERT INTO [catalog].[Grade] ([Code],[Name],[SortOrder])
        SELECT g.Code, g.Name, g.SortOrder
        FROM @Grades g
        WHERE NOT EXISTS (SELECT 1 FROM [catalog].[Grade] x WHERE x.[Code] = g.Code);
    PRINT CONCAT('Grade: seeded ', @@ROWCOUNT, ' of 14 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 3) Subjects DEMO-MAT/LEN/CIE/SOC
    -- ------------------------------------------------------------------
    DECLARE @Subjects TABLE (Code varchar(20) COLLATE Latin1_General_100_CI_AS, Name nvarchar(120));
    INSERT @Subjects (Code, Name) VALUES
        ('DEMO-MAT', N'Matem' + NCHAR(225) + N'ticas'),
        ('DEMO-LEN', N'Lenguaje'),
        ('DEMO-CIE', N'Ciencias'),
        ('DEMO-SOC', N'Sociales');

    INSERT INTO [catalog].[Subject] ([Code],[Name])
        SELECT s.Code, s.Name
        FROM @Subjects s
        WHERE NOT EXISTS (SELECT 1 FROM [catalog].[Subject] x WHERE x.[Code] = s.Code);
    PRINT CONCAT('Subject: seeded ', @@ROWCOUNT, ' of 4 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 4) Schools COL-PUB-001/002, COL-PRI-001/002
    -- ------------------------------------------------------------------
    DECLARE @Schools TABLE (Code varchar(20) COLLATE Latin1_General_100_CI_AS, Name nvarchar(160), Sector varchar(8) COLLATE Latin1_General_100_CI_AS);
    INSERT @Schools (Code, Name, Sector) VALUES
        ('COL-PUB-001', N'Colegio P' + NCHAR(250) + N'blico Central', 'Public'),
        ('COL-PUB-002', N'Colegio P' + NCHAR(250) + N'blico Distrital Norte', 'Public'),
        ('COL-PRI-001', N'Colegio Privado San Gabriel', 'Private'),
        ('COL-PRI-002', N'Colegio Privado Horizonte', 'Private');

    INSERT INTO [catalog].[School] ([Code],[Name],[Sector])
        SELECT s.Code, s.Name, s.Sector
        FROM @Schools s
        WHERE NOT EXISTS (SELECT 1 FROM [catalog].[School] x WHERE x.[Code] = s.Code);
    PRINT CONCAT('School: seeded ', @@ROWCOUNT, ' of 4 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 5) Historical AcademicYears (currentYear resolved dynamically)
    -- ------------------------------------------------------------------
    DECLARE @CurrentAcademicYearId int = (SELECT [CurrentAcademicYearId] FROM [catalog].[AcademicConfiguration] WHERE [Id] = 1);
    IF @CurrentAcademicYearId IS NULL THROW 51101, 'Demo seed precondition failed: catalog.AcademicConfiguration(Id=1) is missing.', 1;

    DECLARE @CurrentYear int = (SELECT YEAR([StartDate]) FROM [catalog].[AcademicYear] WHERE [Id] = @CurrentAcademicYearId);
    IF @CurrentYear IS NULL THROW 51102, 'Demo seed precondition failed: the current catalog.AcademicYear could not be resolved.', 1;

    DECLARE @Year1 int = @CurrentYear - 1;
    DECLARE @Year2 int = @CurrentYear - 2;
    DECLARE @AyCode1 varchar(20) = CONCAT('DEMO-AY-', @Year1);
    DECLARE @AyCode2 varchar(20) = CONCAT('DEMO-AY-', @Year2);

    IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WHERE [Code] = @AyCode1)
    BEGIN
        INSERT INTO [catalog].[AcademicYear] ([Code],[Name],[StartDate],[EndDate])
            VALUES (@AyCode1, CONCAT('Demo Academic Year ', @Year1), DATEFROMPARTS(@Year1, 1, 1), DATEFROMPARTS(@Year1, 12, 31));
        PRINT CONCAT('AcademicYear: seeded ', @AyCode1);
    END
    ELSE PRINT CONCAT('AcademicYear: skipped ', @AyCode1, ' (exists)');

    IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WHERE [Code] = @AyCode2)
    BEGIN
        INSERT INTO [catalog].[AcademicYear] ([Code],[Name],[StartDate],[EndDate])
            VALUES (@AyCode2, CONCAT('Demo Academic Year ', @Year2), DATEFROMPARTS(@Year2, 1, 1), DATEFROMPARTS(@Year2, 12, 31));
        PRINT CONCAT('AcademicYear: seeded ', @AyCode2);
    END
    ELSE PRINT CONCAT('AcademicYear: skipped ', @AyCode2, ' (exists)');

    DECLARE @AyCurrentId int = @CurrentAcademicYearId;
    DECLARE @Ay1Id int = (SELECT [Id] FROM [catalog].[AcademicYear] WHERE [Code] = @AyCode1);
    DECLARE @Ay2Id int = (SELECT [Id] FROM [catalog].[AcademicYear] WHERE [Code] = @AyCode2);

    -- ------------------------------------------------------------------
    -- 6) Students DEMO-EST-001..024 (Person + Student role)
    -- ------------------------------------------------------------------
    DECLARE @Students TABLE (DocNumber nvarchar(32) COLLATE Latin1_General_100_CI_AS, FirstNames nvarchar(120), LastNames nvarchar(120), Age int);
    INSERT @Students (DocNumber, FirstNames, LastNames, Age) VALUES
        ('DEMO-EST-001', N'Mateo', N'Rojas', 3),
        ('DEMO-EST-002', N'Valentina', N'Diaz', 5),
        ('DEMO-EST-003', N'Samuel', N'Torres', 6),
        ('DEMO-EST-004', N'Isabella', N'Vargas', 4),
        ('DEMO-EST-005', N'Santiago', N'Moreno', 7),
        ('DEMO-EST-006', N'Camila', N'Herrera', 10),
        ('DEMO-EST-007', N'Sebastian', N'Castro', 4),
        ('DEMO-EST-008', N'Martina', N'Ortiz', 6),
        ('DEMO-EST-009', N'Nicolas', N'Ramirez', 5),
        ('DEMO-EST-010', N'Emma', N'Gutierrez', 8),
        ('DEMO-EST-011', N'Benjamin', N'Silva', 9),
        ('DEMO-EST-012', N'Sofia', N'Mendoza', 10),
        ('DEMO-EST-013', N'Tomas', N'Aguilar', 11),
        ('DEMO-EST-014', N'Renata', N'Paredes', 12),
        ('DEMO-EST-015', N'Lucas', N'Guzman', 9),
        ('DEMO-EST-016', N'Antonella', N'Reyes', 11),
        ('DEMO-EST-017', N'Joaquin', N'Navarro', 13),
        ('DEMO-EST-018', N'Victoria', N'Cortes', 14),
        ('DEMO-EST-019', N'Gabriel', N'Salazar', 13),
        ('DEMO-EST-020', N'Emilia', N'Cabrera', 15),
        ('DEMO-EST-021', N'Damian', N'Fuentes', 16),
        ('DEMO-EST-022', N'Julieta', N'Campos', 14),
        ('DEMO-EST-023', N'Alejandro', N'Rios', 17),
        ('DEMO-EST-024', N'Catalina', N'Nunez', 15);

    INSERT INTO [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate])
        SELECT @DniId, s.DocNumber, s.FirstNames, s.LastNames,
            DATEADD(DAY, -30, DATEADD(YEAR, -s.Age, CAST(GETDATE() AS date)))
        FROM @Students s
        WHERE NOT EXISTS (SELECT 1 FROM [people].[Person] x WHERE x.[DocumentTypeId] = @DniId AND x.[DocumentNumber] = s.DocNumber);
    PRINT CONCAT('Person (students): seeded ', @@ROWCOUNT, ' of 24 candidate row(s).');

    INSERT INTO [people].[Student] ([PersonId])
        SELECT p.[Id]
        FROM [people].[Person] p
        JOIN @Students s ON p.[DocumentTypeId] = @DniId AND p.[DocumentNumber] = s.DocNumber
        WHERE NOT EXISTS (SELECT 1 FROM [people].[Student] x WHERE x.[PersonId] = p.[Id]);
    PRINT CONCAT('Student role: seeded ', @@ROWCOUNT, ' of 24 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 7) Teachers DEMO-DOC-001..008 (Person + Teacher role)
    -- ------------------------------------------------------------------
    DECLARE @Teachers TABLE (DocNumber nvarchar(32) COLLATE Latin1_General_100_CI_AS, FirstNames nvarchar(120), LastNames nvarchar(120), Age int);
    INSERT @Teachers (DocNumber, FirstNames, LastNames, Age) VALUES
        ('DEMO-DOC-001', N'Andres', N'Molina', 40),
        ('DEMO-DOC-002', N'Paula', N'Restrepo', 35),
        ('DEMO-DOC-003', N'Diego', N'Sanchez', 45),
        ('DEMO-DOC-004', N'Carolina', N'Nino', 32),
        ('DEMO-DOC-005', N'Felipe', N'Cardenas', 38),
        ('DEMO-DOC-006', N'Manuela', N'Rincon', 41),
        ('DEMO-DOC-007', N'Ricardo', N'Beltran', 50),
        ('DEMO-DOC-008', N'Daniela', N'Cuellar', 29);

    INSERT INTO [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate])
        SELECT @DniId, t.DocNumber, t.FirstNames, t.LastNames,
            DATEADD(DAY, -30, DATEADD(YEAR, -t.Age, CAST(GETDATE() AS date)))
        FROM @Teachers t
        WHERE NOT EXISTS (SELECT 1 FROM [people].[Person] x WHERE x.[DocumentTypeId] = @DniId AND x.[DocumentNumber] = t.DocNumber);
    PRINT CONCAT('Person (teachers): seeded ', @@ROWCOUNT, ' of 8 candidate row(s).');

    INSERT INTO [people].[Teacher] ([PersonId])
        SELECT p.[Id]
        FROM [people].[Person] p
        JOIN @Teachers t ON p.[DocumentTypeId] = @DniId AND p.[DocumentNumber] = t.DocNumber
        WHERE NOT EXISTS (SELECT 1 FROM [people].[Teacher] x WHERE x.[PersonId] = p.[Id]);
    PRINT CONCAT('Teacher role: seeded ', @@ROWCOUNT, ' of 8 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 8) Enrollment matrix source (40 rows: student, school, year offset
    --    relative to currentYear, grade, class-group letter)
    -- ------------------------------------------------------------------
    DECLARE @Enrollments TABLE (
        StudentDoc nvarchar(32) COLLATE Latin1_General_100_CI_AS,
        SchoolCode varchar(20) COLLATE Latin1_General_100_CI_AS,
        YearOffset int,
        GradeCode varchar(20) COLLATE Latin1_General_100_CI_AS,
        GroupLetter char(1) COLLATE Latin1_General_100_CI_AS);
    INSERT @Enrollments (StudentDoc, SchoolCode, YearOffset, GradeCode, GroupLetter) VALUES
        -- currentYear (y): 24 rows, split 10/6/5/3 across the 4 schools
        ('DEMO-EST-001', 'COL-PUB-001', 0, 'DEMO-PJ',  'A'),
        ('DEMO-EST-002', 'COL-PUB-002', 0, 'DEMO-TR',  'A'),
        ('DEMO-EST-003', 'COL-PRI-001', 0, 'DEMO-G01', 'A'),
        ('DEMO-EST-004', 'COL-PUB-001', 0, 'DEMO-JI',  'A'),
        ('DEMO-EST-005', 'COL-PRI-001', 0, 'DEMO-G02', 'A'),
        ('DEMO-EST-006', 'COL-PUB-001', 0, 'DEMO-G05', 'A'),
        ('DEMO-EST-007', 'COL-PRI-002', 0, 'DEMO-JI',  'A'),
        ('DEMO-EST-008', 'COL-PUB-002', 0, 'DEMO-G01', 'A'),
        ('DEMO-EST-009', 'COL-PUB-001', 0, 'DEMO-TR',  'A'),
        ('DEMO-EST-010', 'COL-PUB-002', 0, 'DEMO-G03', 'A'),
        ('DEMO-EST-011', 'COL-PUB-001', 0, 'DEMO-G05', 'A'),
        ('DEMO-EST-012', 'COL-PUB-001', 0, 'DEMO-G05', 'A'),
        ('DEMO-EST-013', 'COL-PUB-002', 0, 'DEMO-G06', 'A'),
        ('DEMO-EST-014', 'COL-PRI-001', 0, 'DEMO-G07', 'A'),
        ('DEMO-EST-015', 'COL-PUB-001', 0, 'DEMO-G04', 'A'),
        ('DEMO-EST-016', 'COL-PUB-001', 0, 'DEMO-G06', 'A'),
        ('DEMO-EST-017', 'COL-PUB-001', 0, 'DEMO-G08', 'A'),
        ('DEMO-EST-018', 'COL-PUB-002', 0, 'DEMO-G09', 'A'),
        ('DEMO-EST-019', 'COL-PRI-001', 0, 'DEMO-G08', 'A'),
        ('DEMO-EST-020', 'COL-PUB-001', 0, 'DEMO-G10', 'A'),
        ('DEMO-EST-021', 'COL-PRI-002', 0, 'DEMO-G11', 'A'),
        ('DEMO-EST-022', 'COL-PUB-002', 0, 'DEMO-G09', 'A'),
        ('DEMO-EST-023', 'COL-PRI-001', 0, 'DEMO-G11', 'A'),
        ('DEMO-EST-024', 'COL-PRI-002', 0, 'DEMO-G10', 'A'),
        -- y-1: 10 rows (the 4 two-year students + the 6 three-year students)
        ('DEMO-EST-003', 'COL-PRI-001', -1, 'DEMO-TR',  'A'),
        ('DEMO-EST-006', 'COL-PUB-001', -1, 'DEMO-G04', 'B'),
        ('DEMO-EST-008', 'COL-PUB-002', -1, 'DEMO-TR',  'A'),
        ('DEMO-EST-010', 'COL-PUB-002', -1, 'DEMO-G02', 'A'),
        ('DEMO-EST-013', 'COL-PUB-002', -1, 'DEMO-G05', 'A'),
        ('DEMO-EST-014', 'COL-PRI-001', -1, 'DEMO-G06', 'A'),
        ('DEMO-EST-018', 'COL-PUB-002', -1, 'DEMO-G08', 'A'),
        ('DEMO-EST-019', 'COL-PRI-001', -1, 'DEMO-G07', 'A'),
        ('DEMO-EST-021', 'COL-PRI-002', -1, 'DEMO-G10', 'A'),
        ('DEMO-EST-024', 'COL-PRI-002', -1, 'DEMO-G09', 'A'),
        -- y-2: 6 rows (the 6 three-year students, incl. EST-006's fixed case)
        ('DEMO-EST-006', 'COL-PUB-001', -2, 'DEMO-G03', 'A'),
        ('DEMO-EST-010', 'COL-PUB-002', -2, 'DEMO-G01', 'A'),
        ('DEMO-EST-014', 'COL-PRI-001', -2, 'DEMO-G05', 'A'),
        ('DEMO-EST-018', 'COL-PUB-002', -2, 'DEMO-G07', 'A'),
        ('DEMO-EST-021', 'COL-PRI-002', -2, 'DEMO-G09', 'A'),
        ('DEMO-EST-024', 'COL-PRI-002', -2, 'DEMO-G08', 'A');

    -- ------------------------------------------------------------------
    -- 9) ClassGroups derived from the enrollment matrix. Code scheme:
    --    DEMO-<esc><gg><grp>-<yy> (<=20 chars; esc is one of P1, P2, R1, R2).
    -- ------------------------------------------------------------------
    DECLARE @SchoolMap TABLE (SchoolCode varchar(20) COLLATE Latin1_General_100_CI_AS, Esc char(2) COLLATE Latin1_General_100_CI_AS);
    INSERT @SchoolMap (SchoolCode, Esc) VALUES
        ('COL-PUB-001', 'P1'), ('COL-PUB-002', 'P2'), ('COL-PRI-001', 'R1'), ('COL-PRI-002', 'R2');

    DECLARE @GradeMap TABLE (GradeCode varchar(20) COLLATE Latin1_General_100_CI_AS, Gg char(2) COLLATE Latin1_General_100_CI_AS);
    INSERT @GradeMap (GradeCode, Gg) VALUES
        ('DEMO-PJ', 'PJ'), ('DEMO-JI', 'JI'), ('DEMO-TR', 'TR'),
        ('DEMO-G01', '01'), ('DEMO-G02', '02'), ('DEMO-G03', '03'), ('DEMO-G04', '04'),
        ('DEMO-G05', '05'), ('DEMO-G06', '06'), ('DEMO-G07', '07'), ('DEMO-G08', '08'),
        ('DEMO-G09', '09'), ('DEMO-G10', '10'), ('DEMO-G11', '11');

    ;WITH Distinct_Groups AS (
        SELECT DISTINCT
            e.SchoolCode, e.GradeCode, e.GroupLetter, e.YearOffset,
            CASE e.YearOffset WHEN 0 THEN @AyCurrentId WHEN -1 THEN @Ay1Id ELSE @Ay2Id END AS AcademicYearId,
            CASE e.YearOffset WHEN 0 THEN @CurrentYear WHEN -1 THEN @Year1 ELSE @Year2 END AS TargetYear
        FROM @Enrollments e
    )
    INSERT INTO [academic].[ClassGroup] ([SchoolId],[AcademicYearId],[GradeId],[Code])
        SELECT sc.[Id], dg.AcademicYearId, gr.[Id],
            CONCAT('DEMO-', sm.Esc, gm.Gg, dg.GroupLetter, '-', RIGHT(CAST(dg.TargetYear AS varchar(4)), 2))
        FROM Distinct_Groups dg
        JOIN [catalog].[School] sc ON sc.[Code] = dg.SchoolCode
        JOIN [catalog].[Grade] gr ON gr.[Code] = dg.GradeCode
        JOIN @SchoolMap sm ON sm.SchoolCode = dg.SchoolCode
        JOIN @GradeMap gm ON gm.GradeCode = dg.GradeCode
        WHERE NOT EXISTS (
            SELECT 1 FROM [academic].[ClassGroup] x
            WHERE x.[SchoolId] = sc.[Id] AND x.[AcademicYearId] = dg.AcademicYearId AND x.[GradeId] = gr.[Id]);
    PRINT CONCAT('ClassGroup: seeded ', @@ROWCOUNT, ' of 37 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 10) Enrollments (40 rows)
    -- ------------------------------------------------------------------
    INSERT INTO [academic].[Enrollment] ([StudentPersonId],[ClassGroupId],[AcademicYearId])
        SELECT p.[Id], cg.[Id], cg.[AcademicYearId]
        FROM @Enrollments e
        JOIN [people].[Person] p ON p.[DocumentTypeId] = @DniId AND p.[DocumentNumber] = e.StudentDoc
        JOIN [catalog].[School] sc ON sc.[Code] = e.SchoolCode
        JOIN [catalog].[Grade] gr ON gr.[Code] = e.GradeCode
        JOIN [academic].[ClassGroup] cg ON cg.[SchoolId] = sc.[Id] AND cg.[GradeId] = gr.[Id]
            AND cg.[AcademicYearId] = CASE e.YearOffset WHEN 0 THEN @AyCurrentId WHEN -1 THEN @Ay1Id ELSE @Ay2Id END
        WHERE NOT EXISTS (
            SELECT 1 FROM [academic].[Enrollment] x
            WHERE x.[StudentPersonId] = p.[Id] AND x.[AcademicYearId] = cg.[AcademicYearId]);
    PRINT CONCAT('Enrollment: seeded ', @@ROWCOUNT, ' of 40 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 11) TeacherContracts (10 rows, all Confirmed)
    -- ------------------------------------------------------------------
    DECLARE @Today date = CAST(GETDATE() AS date);
    DECLARE @Contracts TABLE (
        TeacherDoc nvarchar(32) COLLATE Latin1_General_100_CI_AS,
        SchoolCode varchar(20) COLLATE Latin1_General_100_CI_AS,
        StartDate date, EndDate date NULL);
    INSERT @Contracts (TeacherDoc, SchoolCode, StartDate, EndDate) VALUES
        ('DEMO-DOC-001', 'COL-PUB-001', DATEFROMPARTS(@Year2, 1, 1), NULL),
        ('DEMO-DOC-002', 'COL-PUB-002', DATEFROMPARTS(@Year1, 1, 1), NULL),
        ('DEMO-DOC-003', 'COL-PRI-001', DATEFROMPARTS(@Year1, 1, 1), NULL),
        ('DEMO-DOC-004', 'COL-PRI-002', DATEFROMPARTS(@Year1, 1, 1), NULL),
        ('DEMO-DOC-005', 'COL-PUB-001', DATEFROMPARTS(@Year1, 1, 1), NULL),
        ('DEMO-DOC-005', 'COL-PRI-001', DATEFROMPARTS(@Year1, 1, 1), NULL),
        ('DEMO-DOC-006', 'COL-PUB-001', DATEFROMPARTS(@Year2, 1, 1), NULL),
        ('DEMO-DOC-006', 'COL-PUB-002', DATEFROMPARTS(@Year2, 1, 1), NULL),
        ('DEMO-DOC-007', 'COL-PUB-002', DATEFROMPARTS(@Year2, 1, 1), DATEADD(MONTH, -6, @Today)),
        ('DEMO-DOC-008', 'COL-PRI-001', DATEADD(MONTH, 6, @Today), NULL);

    INSERT INTO [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[EndDate],[Status])
        SELECT tp.[Id], sc.[Id], c.StartDate, c.EndDate, 'Confirmed'
        FROM @Contracts c
        JOIN [people].[Person] tp ON tp.[DocumentTypeId] = @DniId AND tp.[DocumentNumber] = c.TeacherDoc
        JOIN [catalog].[School] sc ON sc.[Code] = c.SchoolCode
        WHERE NOT EXISTS (
            SELECT 1 FROM [staff].[TeacherContract] x
            WHERE x.[TeacherPersonId] = tp.[Id] AND x.[SchoolId] = sc.[Id] AND x.[StartDate] = c.StartDate);
    PRINT CONCAT('TeacherContract: seeded ', @@ROWCOUNT, ' of 10 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 12) TeachingAssignments for EST-006's three historical groups
    -- ------------------------------------------------------------------
    DECLARE @Assignments TABLE (
        TeacherDoc nvarchar(32) COLLATE Latin1_General_100_CI_AS,
        SchoolCode varchar(20) COLLATE Latin1_General_100_CI_AS,
        GradeCode varchar(20) COLLATE Latin1_General_100_CI_AS,
        YearOffset int,
        SubjectCode varchar(20) COLLATE Latin1_General_100_CI_AS);
    INSERT @Assignments (TeacherDoc, SchoolCode, GradeCode, YearOffset, SubjectCode) VALUES
        ('DEMO-DOC-001', 'COL-PUB-001', 'DEMO-G03', -2, 'DEMO-MAT'),
        ('DEMO-DOC-006', 'COL-PUB-001', 'DEMO-G04', -1, 'DEMO-LEN'),
        ('DEMO-DOC-005', 'COL-PUB-001', 'DEMO-G05',  0, 'DEMO-MAT');

    INSERT INTO [academic].[TeachingAssignment] ([TeacherContractId],[ClassGroupId],[SubjectId],[StartDate],[EndDate])
        SELECT tc.[Id], cg.[Id], sub.[Id],
            DATEFROMPARTS(CASE a.YearOffset WHEN 0 THEN @CurrentYear WHEN -1 THEN @Year1 ELSE @Year2 END, 1, 1),
            NULL
        FROM @Assignments a
        JOIN [people].[Person] tp ON tp.[DocumentTypeId] = @DniId AND tp.[DocumentNumber] = a.TeacherDoc
        JOIN [catalog].[School] sc ON sc.[Code] = a.SchoolCode
        JOIN [staff].[TeacherContract] tc ON tc.[TeacherPersonId] = tp.[Id] AND tc.[SchoolId] = sc.[Id]
        JOIN [catalog].[Grade] gr ON gr.[Code] = a.GradeCode
        JOIN [academic].[ClassGroup] cg ON cg.[SchoolId] = sc.[Id] AND cg.[GradeId] = gr.[Id]
            AND cg.[AcademicYearId] = CASE a.YearOffset WHEN 0 THEN @AyCurrentId WHEN -1 THEN @Ay1Id ELSE @Ay2Id END
        JOIN [catalog].[Subject] sub ON sub.[Code] = a.SubjectCode
        WHERE NOT EXISTS (
            SELECT 1 FROM [academic].[TeachingAssignment] x
            WHERE x.[TeacherContractId] = tc.[Id] AND x.[ClassGroupId] = cg.[Id] AND x.[SubjectId] = sub.[Id]);
    PRINT CONCAT('TeachingAssignment: seeded ', @@ROWCOUNT, ' of 3 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 13) ClassSchedule (weekdays 1 and 3 for each of the 3 assignments)
    -- ------------------------------------------------------------------
    INSERT INTO [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday])
        SELECT ta.[Id], wd.Weekday
        FROM @Assignments a
        JOIN [people].[Person] tp ON tp.[DocumentTypeId] = @DniId AND tp.[DocumentNumber] = a.TeacherDoc
        JOIN [catalog].[School] sc ON sc.[Code] = a.SchoolCode
        JOIN [staff].[TeacherContract] tc ON tc.[TeacherPersonId] = tp.[Id] AND tc.[SchoolId] = sc.[Id]
        JOIN [catalog].[Grade] gr ON gr.[Code] = a.GradeCode
        JOIN [academic].[ClassGroup] cg ON cg.[SchoolId] = sc.[Id] AND cg.[GradeId] = gr.[Id]
            AND cg.[AcademicYearId] = CASE a.YearOffset WHEN 0 THEN @AyCurrentId WHEN -1 THEN @Ay1Id ELSE @Ay2Id END
        JOIN [catalog].[Subject] sub ON sub.[Code] = a.SubjectCode
        JOIN [academic].[TeachingAssignment] ta ON ta.[TeacherContractId] = tc.[Id] AND ta.[ClassGroupId] = cg.[Id] AND ta.[SubjectId] = sub.[Id]
        CROSS JOIN (VALUES (CAST(1 AS tinyint)), (CAST(3 AS tinyint))) wd(Weekday)
        WHERE NOT EXISTS (SELECT 1 FROM [academic].[ClassSchedule] x WHERE x.[TeachingAssignmentId] = ta.[Id] AND x.[Weekday] = wd.Weekday);
    PRINT CONCAT('ClassSchedule: seeded ', @@ROWCOUNT, ' of 6 candidate row(s).');

    -- ------------------------------------------------------------------
    -- 14) Self-verification (THROW 51100-series on any invariant miss)
    -- ------------------------------------------------------------------
    DECLARE @StudentCount int = (
        SELECT COUNT(*) FROM [people].[Student] st JOIN [people].[Person] p ON p.[Id] = st.[PersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%');
    IF @StudentCount <> 24 THROW 51110, 'Demo self-check failed: expected 24 DEMO students.', 1;

    DECLARE @EnrollmentCount int = (
        SELECT COUNT(*) FROM [academic].[Enrollment] en JOIN [people].[Person] p ON p.[Id] = en.[StudentPersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%');
    IF @EnrollmentCount <> 40 THROW 51111, 'Demo self-check failed: expected 40 DEMO enrollments.', 1;

    DECLARE @CurrentYearEnrollmentCount int = (
        SELECT COUNT(*) FROM [academic].[Enrollment] en JOIN [people].[Person] p ON p.[Id] = en.[StudentPersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%' AND en.[AcademicYearId] = @AyCurrentId);
    IF @CurrentYearEnrollmentCount <> 24 THROW 51112, 'Demo self-check failed: expected 24 current-year DEMO enrollments.', 1;

    DECLARE @Pub1Count int, @Pub2Count int, @Pri1Count int, @Pri2Count int;
    SELECT @Pub1Count = COUNT(*) FROM [academic].[Enrollment] en
        JOIN [academic].[ClassGroup] cg ON cg.[Id] = en.[ClassGroupId]
        JOIN [catalog].[School] sc ON sc.[Id] = cg.[SchoolId]
        WHERE en.[AcademicYearId] = @AyCurrentId AND sc.[Code] = 'COL-PUB-001';
    SELECT @Pub2Count = COUNT(*) FROM [academic].[Enrollment] en
        JOIN [academic].[ClassGroup] cg ON cg.[Id] = en.[ClassGroupId]
        JOIN [catalog].[School] sc ON sc.[Id] = cg.[SchoolId]
        WHERE en.[AcademicYearId] = @AyCurrentId AND sc.[Code] = 'COL-PUB-002';
    SELECT @Pri1Count = COUNT(*) FROM [academic].[Enrollment] en
        JOIN [academic].[ClassGroup] cg ON cg.[Id] = en.[ClassGroupId]
        JOIN [catalog].[School] sc ON sc.[Id] = cg.[SchoolId]
        WHERE en.[AcademicYearId] = @AyCurrentId AND sc.[Code] = 'COL-PRI-001';
    SELECT @Pri2Count = COUNT(*) FROM [academic].[Enrollment] en
        JOIN [academic].[ClassGroup] cg ON cg.[Id] = en.[ClassGroupId]
        JOIN [catalog].[School] sc ON sc.[Id] = cg.[SchoolId]
        WHERE en.[AcademicYearId] = @AyCurrentId AND sc.[Code] = 'COL-PRI-002';
    IF NOT (@Pub1Count = 10 AND @Pub2Count = 6 AND @Pri1Count = 5 AND @Pri2Count = 3)
        THROW 51113, 'Demo self-check failed: current-year school distribution must be exactly 10/6/5/3.', 1;

    DECLARE @Quinto001Count int = (
        SELECT COUNT(*) FROM [academic].[Enrollment] en
        JOIN [academic].[ClassGroup] cg ON cg.[Id] = en.[ClassGroupId]
        JOIN [catalog].[School] sc ON sc.[Id] = cg.[SchoolId]
        JOIN [catalog].[Grade] gr ON gr.[Id] = cg.[GradeId]
        WHERE en.[AcademicYearId] = @AyCurrentId AND sc.[Code] = 'COL-PUB-001' AND gr.[Code] = 'DEMO-G05');
    IF @Quinto001Count <> 3 THROW 51114, 'Demo self-check failed: expected exactly 3 current-year enrollments in Quinto @ COL-PUB-001.', 1;

    DECLARE @TeacherCount int = (
        SELECT COUNT(*) FROM [people].[Teacher] t JOIN [people].[Person] p ON p.[Id] = t.[PersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%');
    IF @TeacherCount <> 8 THROW 51115, 'Demo self-check failed: expected 8 DEMO teachers.', 1;

    DECLARE @ContractCount int = (
        SELECT COUNT(*) FROM [staff].[TeacherContract] tc JOIN [people].[Person] p ON p.[Id] = tc.[TeacherPersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%');
    IF @ContractCount <> 10 THROW 51116, 'Demo self-check failed: expected 10 DEMO teacher contracts.', 1;

    DECLARE @ActiveContractCount int = (
        SELECT COUNT(*) FROM [staff].[TeacherContract] tc JOIN [people].[Person] p ON p.[Id] = tc.[TeacherPersonId]
        WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%' AND tc.[Status] = 'Confirmed'
            AND tc.[StartDate] <= @Today AND (tc.[EndDate] IS NULL OR tc.[EndDate] >= @Today));
    IF @ActiveContractCount <> 8 THROW 51117, 'Demo self-check failed: expected 8 active DEMO teacher contracts as of today.', 1;

    DECLARE @Est006Years int = (
        SELECT COUNT(DISTINCT en.[AcademicYearId]) FROM [academic].[Enrollment] en
        JOIN [people].[Person] p ON p.[Id] = en.[StudentPersonId]
        WHERE p.[DocumentNumber] = 'DEMO-EST-006');
    IF @Est006Years <> 3 THROW 51118, 'Demo self-check failed: DEMO-EST-006 must have exactly 3 enrollment years.', 1;

    DECLARE @Est006Teachers int = (
        SELECT COUNT(DISTINCT tc.[TeacherPersonId])
        FROM [academic].[Enrollment] en
        JOIN [people].[Person] p ON p.[Id] = en.[StudentPersonId]
        JOIN [academic].[TeachingAssignment] ta ON ta.[ClassGroupId] = en.[ClassGroupId]
        JOIN [staff].[TeacherContract] tc ON tc.[Id] = ta.[TeacherContractId]
        WHERE p.[DocumentNumber] = 'DEMO-EST-006');
    IF @Est006Teachers <> 3 THROW 51119, 'Demo self-check failed: DEMO-EST-006 must have 3 distinct teachers across its history.', 1;

    COMMIT TRANSACTION;
    PRINT 'Demo seed batch complete: 24 students / 40 enrollments / 8 teachers / 10 contracts verified.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
