-- Fictitious LOCAL-EVALUATION demo data. NEVER part of the production seed.
-- Single idempotent batch: every INSERT is guarded by IF NOT EXISTS on its
-- natural key, so re-running is a safe no-op.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    -- Document types the frontend enrollment form offers (DNI/PAS/CE).
    -- IsActive is NOT NULL with no default, so it is set explicitly.
    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'DNI')
    BEGIN
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('DNI', N'Documento Nacional de Identidad', 1);
        PRINT 'seeded : DocumentType DNI';
    END
    ELSE PRINT 'skipped: DocumentType DNI (exists)';

    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'PAS')
    BEGIN
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('PAS', N'Pasaporte', 1);
        PRINT 'seeded : DocumentType PAS';
    END
    ELSE PRINT 'skipped: DocumentType PAS (exists)';

    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WHERE [Code] = 'CE')
    BEGIN
        -- NCHAR(233)=e-acute, NCHAR(237)=i-acute: "Cedula de Extranjeria"
        -- with accents, kept ASCII-safe in this file.
        INSERT [catalog].[DocumentType] ([Code],[Name],[IsActive])
            VALUES ('CE', N'C' + NCHAR(233) + N'dula de Extranjer' + NCHAR(237) + N'a', 1);
        PRINT 'seeded : DocumentType CE';
    END
    ELSE PRINT 'skipped: DocumentType CE (exists)';

    -- Second school (natural key: Code).
    IF NOT EXISTS (SELECT 1 FROM [catalog].[School] WHERE [Code] = 'SCH-002')
    BEGIN
        INSERT [catalog].[School] ([Code],[Name],[Sector])
            VALUES ('SCH-002', N'South Learning Center', 'Public');
        PRINT 'seeded : School SCH-002';
    END
    ELSE PRINT 'skipped: School SCH-002 (exists)';

    -- Class group in the canonical school/year/grade (natural key: context).
    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassGroup]
                   WHERE [SchoolId] = 1 AND [AcademicYearId] = 1 AND [GradeId] = 1 AND [Code] = 'CG-01')
    BEGIN
        INSERT [academic].[ClassGroup] ([SchoolId],[AcademicYearId],[GradeId],[Code])
            VALUES (1, 1, 1, 'CG-01');
        PRINT 'seeded : ClassGroup CG-01';
    END
    ELSE PRINT 'skipped: ClassGroup CG-01 (exists)';

    DECLARE @classGroupId int = (SELECT [Id] FROM [academic].[ClassGroup]
        WHERE [SchoolId] = 1 AND [AcademicYearId] = 1 AND [GradeId] = 1 AND [Code] = 'CG-01');

    -- Demo teacher person (natural key: DocumentTypeId + DocumentNumber).
    IF NOT EXISTS (SELECT 1 FROM [people].[Person]
                   WHERE [DocumentTypeId] = 1 AND [DocumentNumber] = N'TCH-0001')
    BEGIN
        INSERT [people].[Person] ([DocumentTypeId],[DocumentNumber],[FirstNames],[LastNames],[BirthDate])
            VALUES (1, N'TCH-0001', N'Ana', N'Gomez', '1985-04-12');
        PRINT 'seeded : Person TCH-0001 (Ana Gomez)';
    END
    ELSE PRINT 'skipped: Person TCH-0001 (exists)';

    DECLARE @teacherPersonId int = (SELECT [Id] FROM [people].[Person]
        WHERE [DocumentTypeId] = 1 AND [DocumentNumber] = N'TCH-0001');

    IF NOT EXISTS (SELECT 1 FROM [people].[Teacher] WHERE [PersonId] = @teacherPersonId)
    BEGIN
        INSERT [people].[Teacher] ([PersonId]) VALUES (@teacherPersonId);
        PRINT 'seeded : Teacher role for TCH-0001';
    END
    ELSE PRINT 'skipped: Teacher role for TCH-0001 (exists)';

    -- Subject (natural key: Code).
    IF NOT EXISTS (SELECT 1 FROM [catalog].[Subject] WHERE [Code] = 'MATH')
    BEGIN
        INSERT [catalog].[Subject] ([Code],[Name]) VALUES ('MATH', N'Mathematics');
        PRINT 'seeded : Subject MATH';
    END
    ELSE PRINT 'skipped: Subject MATH (exists)';

    DECLARE @subjectId int = (SELECT [Id] FROM [catalog].[Subject] WHERE [Code] = 'MATH');

    -- Confirmed contract for the demo teacher at the canonical school
    -- (natural key: teacher + school + start date).
    IF NOT EXISTS (SELECT 1 FROM [staff].[TeacherContract]
                   WHERE [TeacherPersonId] = @teacherPersonId AND [SchoolId] = 1 AND [StartDate] = '2026-01-15')
    BEGIN
        INSERT [staff].[TeacherContract] ([TeacherPersonId],[SchoolId],[StartDate],[EndDate],[Status])
            VALUES (@teacherPersonId, 1, '2026-01-15', NULL, 'Confirmed');
        PRINT 'seeded : TeacherContract (TCH-0001 @ SchoolId 1, 2026-01-15)';
    END
    ELSE PRINT 'skipped: TeacherContract (exists)';

    DECLARE @contractId int = (SELECT [Id] FROM [staff].[TeacherContract]
        WHERE [TeacherPersonId] = @teacherPersonId AND [SchoolId] = 1 AND [StartDate] = '2026-01-15');

    -- Teaching assignment (natural key: contract + class group + subject).
    IF NOT EXISTS (SELECT 1 FROM [academic].[TeachingAssignment]
                   WHERE [TeacherContractId] = @contractId AND [ClassGroupId] = @classGroupId AND [SubjectId] = @subjectId)
    BEGIN
        INSERT [academic].[TeachingAssignment] ([TeacherContractId],[ClassGroupId],[SubjectId],[StartDate],[EndDate])
            VALUES (@contractId, @classGroupId, @subjectId, '2026-02-01', NULL);
        PRINT 'seeded : TeachingAssignment (MATH @ CG-01)';
    END
    ELSE PRINT 'skipped: TeachingAssignment (exists)';

    DECLARE @assignmentId int = (SELECT [Id] FROM [academic].[TeachingAssignment]
        WHERE [TeacherContractId] = @contractId AND [ClassGroupId] = @classGroupId AND [SubjectId] = @subjectId);

    -- Class schedule on weekdays 1 (Monday) and 3 (Wednesday).
    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassSchedule]
                   WHERE [TeachingAssignmentId] = @assignmentId AND [Weekday] = 1)
    BEGIN
        INSERT [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday]) VALUES (@assignmentId, 1);
        PRINT 'seeded : ClassSchedule weekday 1';
    END
    ELSE PRINT 'skipped: ClassSchedule weekday 1 (exists)';

    IF NOT EXISTS (SELECT 1 FROM [academic].[ClassSchedule]
                   WHERE [TeachingAssignmentId] = @assignmentId AND [Weekday] = 3)
    BEGIN
        INSERT [academic].[ClassSchedule] ([TeachingAssignmentId],[Weekday]) VALUES (@assignmentId, 3);
        PRINT 'seeded : ClassSchedule weekday 3';
    END
    ELSE PRINT 'skipped: ClassSchedule weekday 3 (exists)';

    COMMIT TRANSACTION;
    PRINT 'Demo data batch complete.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
