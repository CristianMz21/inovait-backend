-- Inovait P0+P1 production model — from-scratch, transactional, idempotent setup script.
--
-- Reproduces, on an empty SQL Server 2022 database, exactly what the EF Core migration
-- chain (20260711161500_InitialP0ProductionModel + 20260711161518_AddP0DatabaseProtections +
-- 20260712001412_AddP1TeachingModel + 20260712010000_AddP1DatabaseProtections) produces:
-- 4 schemas, 14 tables (11 P0 + catalog.Subject/academic.TeachingAssignment/academic.ClassSchedule)
-- with their columns/collations/defaults/checks/PKs/FKs/indexes, 5 protective triggers (4 P0 +
-- TR_Subject_ProtectCode), the locked-down `inovait_runtime` database role with its exact
-- GRANT/DENY set, and the 5 canonical seed rows (School, AcademicYear, Grade, DocumentType,
-- AcademicConfiguration singleton). No fictitious rows are seeded for Subject/TeachingAssignment/
-- ClassSchedule: data-model.md's setup.sql responsibilities section only prescribes the fifth
-- trigger for the P1 extension, and the business-scenario dataset (age boundaries, tied schools,
-- multisector teachers, multi-assignment history) is built per-test by the S14–S17 report/history
-- suites, not by production seed.
--
-- This script does NOT create a database or a login and stores no credentials. It is meant to
-- run as a single batch (no `GO` separators) against an already-selected, empty database — e.g.
-- via EF's `ExecuteSqlRawAsync`. Statements that SQL Server requires to be first-in-batch
-- (CREATE SCHEMA, CREATE TRIGGER) are dispatched through `EXEC(N'...')` so the whole script stays
-- one batch. Running this script twice against the same database is a safe no-op: every schema,
-- table (with its indexes) and seed row is guarded, triggers are declared with
-- `CREATE OR ALTER`, and GRANT/DENY are naturally idempotent in SQL Server.
--
-- It intentionally does NOT create `__EFMigrationsHistory` and does NOT create the
-- `InovaitMigrationOwner` extended property — both are EF-migration-only bookkeeping with no
-- relational meaning for an evaluator/from-scratch database.

SET XACT_ABORT ON;
BEGIN TRY
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;

    ----------------------------------------------------------------------
    -- Schemas
    ----------------------------------------------------------------------
    IF SCHEMA_ID(N'catalog') IS NULL EXEC(N'CREATE SCHEMA [catalog]');
    IF SCHEMA_ID(N'people') IS NULL EXEC(N'CREATE SCHEMA [people]');
    IF SCHEMA_ID(N'academic') IS NULL EXEC(N'CREATE SCHEMA [academic]');
    IF SCHEMA_ID(N'staff') IS NULL EXEC(N'CREATE SCHEMA [staff]');

    ----------------------------------------------------------------------
    -- Tables (FK-safe order), each guarded so a second run is a no-op.
    ----------------------------------------------------------------------

    -- 1) catalog.DocumentType
    IF OBJECT_ID(N'[catalog].[DocumentType]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[DocumentType]
        (
            [Id] smallint NOT NULL IDENTITY(1,1),
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Name] nvarchar(80) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [IsActive] bit NOT NULL,
            CONSTRAINT [PK_DocumentType] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_DocumentType_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_DocumentType_Name_NotBlank] CHECK (LEN(TRIM([Name])) > 0)
        );
        CREATE UNIQUE INDEX [UQ_DocumentType_Code] ON [catalog].[DocumentType]([Code]);
    END;

    -- 2) catalog.School
    IF OBJECT_ID(N'[catalog].[School]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[School]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Name] nvarchar(160) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Sector] varchar(8) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_School] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_School_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_School_Name_NotBlank] CHECK (LEN(TRIM([Name])) > 0),
            CONSTRAINT [CK_School_Sector] CHECK ([Sector] IN ('Public','Private')),
            CONSTRAINT [CK_School_Sector_NotBlank] CHECK (LEN(TRIM([Sector])) > 0),
            CONSTRAINT [CK_School_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
        );
        CREATE UNIQUE INDEX [UQ_School_Code] ON [catalog].[School]([Code]);
        CREATE UNIQUE INDEX [UQ_School_Name] ON [catalog].[School]([Name]);
    END;

    -- 3) catalog.AcademicYear
    IF OBJECT_ID(N'[catalog].[AcademicYear]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[AcademicYear]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Name] nvarchar(80) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [StartDate] date NOT NULL,
            [EndDate] date NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_AcademicYear] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_AcademicYear_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_AcademicYear_DateRange] CHECK ([EndDate] >= [StartDate]),
            CONSTRAINT [CK_AcademicYear_Name_NotBlank] CHECK (LEN(TRIM([Name])) > 0),
            CONSTRAINT [CK_AcademicYear_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
        );
        CREATE UNIQUE INDEX [UQ_AcademicYear_Code] ON [catalog].[AcademicYear]([Code]);
        CREATE UNIQUE INDEX [UQ_AcademicYear_Name] ON [catalog].[AcademicYear]([Name]);
    END;

    -- 4) catalog.AcademicConfiguration (singleton)
    IF OBJECT_ID(N'[catalog].[AcademicConfiguration]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[AcademicConfiguration]
        (
            [Id] tinyint NOT NULL,
            [CurrentAcademicYearId] int NOT NULL,
            CONSTRAINT [PK_AcademicConfiguration] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_AcademicConfiguration_Singleton] CHECK ([Id] = 1),
            CONSTRAINT [FK_AcademicConfiguration_AcademicYear] FOREIGN KEY ([CurrentAcademicYearId])
                REFERENCES [catalog].[AcademicYear]([Id]) ON DELETE NO ACTION
        );
        CREATE INDEX [IX_AcademicConfiguration_CurrentAcademicYearId]
            ON [catalog].[AcademicConfiguration]([CurrentAcademicYearId]);
    END;

    -- 5) catalog.Grade
    IF OBJECT_ID(N'[catalog].[Grade]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[Grade]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Name] nvarchar(80) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [SortOrder] smallint NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_Grade] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_Grade_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_Grade_Name_NotBlank] CHECK (LEN(TRIM([Name])) > 0),
            CONSTRAINT [CK_Grade_SortOrder] CHECK ([SortOrder] > 0),
            CONSTRAINT [CK_Grade_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
        );
        CREATE UNIQUE INDEX [UQ_Grade_Code] ON [catalog].[Grade]([Code]);
        CREATE UNIQUE INDEX [UQ_Grade_Name] ON [catalog].[Grade]([Name]);
        CREATE UNIQUE INDEX [UQ_Grade_SortOrder] ON [catalog].[Grade]([SortOrder]);
    END;

    -- 6) catalog.Subject (P1)
    IF OBJECT_ID(N'[catalog].[Subject]', N'U') IS NULL
    BEGIN
        CREATE TABLE [catalog].[Subject]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [Name] nvarchar(120) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_Subject] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_Subject_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_Subject_Name_NotBlank] CHECK (LEN(TRIM([Name])) > 0),
            CONSTRAINT [CK_Subject_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc])
        );
        CREATE UNIQUE INDEX [UQ_Subject_Code] ON [catalog].[Subject]([Code]);
        CREATE UNIQUE INDEX [UQ_Subject_Name] ON [catalog].[Subject]([Name]);
    END;

    -- 7) people.Person
    IF OBJECT_ID(N'[people].[Person]', N'U') IS NULL
    BEGIN
        CREATE TABLE [people].[Person]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [DocumentTypeId] smallint NOT NULL,
            [DocumentNumber] nvarchar(32) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [FirstNames] nvarchar(120) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [LastNames] nvarchar(120) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [BirthDate] date NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_Person] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_Person_DocumentNumber_NotBlank] CHECK (LEN(TRIM([DocumentNumber])) > 0),
            CONSTRAINT [CK_Person_FirstNames_NotBlank] CHECK (LEN(TRIM([FirstNames])) > 0),
            CONSTRAINT [CK_Person_LastNames_NotBlank] CHECK (LEN(TRIM([LastNames])) > 0),
            CONSTRAINT [CK_Person_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc]),
            CONSTRAINT [FK_Person_DocumentType] FOREIGN KEY ([DocumentTypeId])
                REFERENCES [catalog].[DocumentType]([Id]) ON DELETE NO ACTION
        );
        CREATE UNIQUE INDEX [UQ_Person_DocumentTypeId_DocumentNumber]
            ON [people].[Person]([DocumentTypeId],[DocumentNumber]);
        CREATE INDEX [IX_Person_LastNames_FirstNames_Id]
            ON [people].[Person]([LastNames],[FirstNames],[Id])
            INCLUDE ([DocumentTypeId],[DocumentNumber],[BirthDate]);
    END;

    -- 8) people.Student (role table: PK == FK)
    IF OBJECT_ID(N'[people].[Student]', N'U') IS NULL
    BEGIN
        CREATE TABLE [people].[Student]
        (
            [PersonId] int NOT NULL,
            CONSTRAINT [PK_Student] PRIMARY KEY CLUSTERED ([PersonId]),
            CONSTRAINT [FK_Student_Person] FOREIGN KEY ([PersonId])
                REFERENCES [people].[Person]([Id]) ON DELETE NO ACTION
        );
    END;

    -- 9) people.Teacher (role table: PK == FK, audited)
    IF OBJECT_ID(N'[people].[Teacher]', N'U') IS NULL
    BEGIN
        CREATE TABLE [people].[Teacher]
        (
            [PersonId] int NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_Teacher] PRIMARY KEY CLUSTERED ([PersonId]),
            CONSTRAINT [CK_Teacher_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc]),
            CONSTRAINT [FK_Teacher_Person] FOREIGN KEY ([PersonId])
                REFERENCES [people].[Person]([Id]) ON DELETE NO ACTION
        );
    END;

    -- 10) academic.ClassGroup
    IF OBJECT_ID(N'[academic].[ClassGroup]', N'U') IS NULL
    BEGIN
        CREATE TABLE [academic].[ClassGroup]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [SchoolId] int NOT NULL,
            [AcademicYearId] int NOT NULL,
            [GradeId] int NOT NULL,
            [Code] varchar(20) COLLATE Latin1_General_100_CI_AS NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_ClassGroup] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [UQ_ClassGroup_Id_AcademicYear_ForEnrollment] UNIQUE ([Id],[AcademicYearId]),
            CONSTRAINT [CK_ClassGroup_Code_NotBlank] CHECK (LEN(TRIM([Code])) > 0),
            CONSTRAINT [CK_ClassGroup_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc]),
            CONSTRAINT [FK_ClassGroup_AcademicYear] FOREIGN KEY ([AcademicYearId])
                REFERENCES [catalog].[AcademicYear]([Id]) ON DELETE NO ACTION,
            CONSTRAINT [FK_ClassGroup_Grade] FOREIGN KEY ([GradeId])
                REFERENCES [catalog].[Grade]([Id]) ON DELETE NO ACTION,
            CONSTRAINT [FK_ClassGroup_School] FOREIGN KEY ([SchoolId])
                REFERENCES [catalog].[School]([Id]) ON DELETE NO ACTION
        );
        CREATE INDEX [IX_ClassGroup_GradeId] ON [academic].[ClassGroup]([GradeId]);
        CREATE INDEX [IX_ClassGroup_AcademicYearId_GradeId_SchoolId]
            ON [academic].[ClassGroup]([AcademicYearId],[GradeId],[SchoolId])
            INCLUDE ([Code]);
        CREATE UNIQUE INDEX [UQ_ClassGroup_Context]
            ON [academic].[ClassGroup]([SchoolId],[AcademicYearId],[GradeId],[Code]);
    END;

    -- 11) academic.Enrollment
    IF OBJECT_ID(N'[academic].[Enrollment]', N'U') IS NULL
    BEGIN
        CREATE TABLE [academic].[Enrollment]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [StudentPersonId] int NOT NULL,
            [ClassGroupId] int NOT NULL,
            [AcademicYearId] int NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [PK_Enrollment] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [FK_Enrollment_ClassGroupId_AcademicYearId] FOREIGN KEY ([ClassGroupId],[AcademicYearId])
                REFERENCES [academic].[ClassGroup]([Id],[AcademicYearId]) ON DELETE NO ACTION,
            CONSTRAINT [FK_Enrollment_Student] FOREIGN KEY ([StudentPersonId])
                REFERENCES [people].[Student]([PersonId]) ON DELETE NO ACTION
        );
        CREATE INDEX [IX_Enrollment_ClassGroupId_AcademicYearId]
            ON [academic].[Enrollment]([ClassGroupId],[AcademicYearId]);
        CREATE INDEX [IX_Enrollment_ClassGroupId_StudentPersonId]
            ON [academic].[Enrollment]([ClassGroupId],[StudentPersonId])
            INCLUDE ([AcademicYearId],[CreatedAtUtc]);
        CREATE UNIQUE INDEX [UQ_Enrollment_StudentPersonId_AcademicYearId]
            ON [academic].[Enrollment]([StudentPersonId],[AcademicYearId]);
    END;

    -- 12) staff.TeacherContract
    IF OBJECT_ID(N'[staff].[TeacherContract]', N'U') IS NULL
    BEGIN
        CREATE TABLE [staff].[TeacherContract]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [TeacherPersonId] int NOT NULL,
            [SchoolId] int NOT NULL,
            [StartDate] date NOT NULL,
            [EndDate] date NULL,
            [Status] varchar(10) NOT NULL,
            [CancelledAtUtc] datetime2(3) NULL,
            [CancellationReason] nvarchar(300) COLLATE Latin1_General_100_CI_AS NULL,
            [CancellationEffectiveDate] date NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_TeacherContract] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_TeacherContract_CancellationEffectiveDate] CHECK ([CancellationEffectiveDate] IS NULL OR ([CancellationEffectiveDate] >= [StartDate] AND ([EndDate] IS NULL OR [CancellationEffectiveDate] <= [EndDate]))),
            CONSTRAINT [CK_TeacherContract_CancellationReason_NotBlank] CHECK ([CancellationReason] IS NULL OR LEN(TRIM([CancellationReason])) > 0),
            CONSTRAINT [CK_TeacherContract_DateRange] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate]),
            CONSTRAINT [CK_TeacherContract_Status] CHECK ([Status] IN ('Confirmed','Cancelled')),
            CONSTRAINT [CK_TeacherContract_Status_NotBlank] CHECK (LEN(TRIM([Status])) > 0),
            CONSTRAINT [CK_TeacherContract_StatusCancellation] CHECK (([Status]='Confirmed' AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL AND [CancellationEffectiveDate] IS NULL) OR ([Status]='Cancelled' AND [CancelledAtUtc] IS NOT NULL AND [CancellationReason] IS NOT NULL AND [CancellationEffectiveDate] IS NOT NULL)),
            CONSTRAINT [CK_TeacherContract_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc]),
            CONSTRAINT [FK_TeacherContract_School] FOREIGN KEY ([SchoolId])
                REFERENCES [catalog].[School]([Id]) ON DELETE NO ACTION,
            CONSTRAINT [FK_TeacherContract_Teacher] FOREIGN KEY ([TeacherPersonId])
                REFERENCES [people].[Teacher]([PersonId]) ON DELETE NO ACTION
        );
        CREATE INDEX [IX_TeacherContract_SchoolId_StartDate_EndDate]
            ON [staff].[TeacherContract]([SchoolId],[StartDate],[EndDate])
            INCLUDE ([TeacherPersonId],[Status],[CancellationEffectiveDate]);
        CREATE INDEX [IX_TeacherContract_TeacherPersonId_StartDate_EndDate]
            ON [staff].[TeacherContract]([TeacherPersonId],[StartDate],[EndDate])
            INCLUDE ([SchoolId],[Status],[CancelledAtUtc],[CancellationReason],[CancellationEffectiveDate]);
        CREATE UNIQUE INDEX [UQ_TeacherContract_Exact]
            ON [staff].[TeacherContract]([TeacherPersonId],[SchoolId],[StartDate],[EndDate]);
    END;

    -- 13) academic.TeachingAssignment (P1)
    IF OBJECT_ID(N'[academic].[TeachingAssignment]', N'U') IS NULL
    BEGIN
        CREATE TABLE [academic].[TeachingAssignment]
        (
            [Id] int NOT NULL IDENTITY(1,1),
            [TeacherContractId] int NOT NULL,
            [ClassGroupId] int NOT NULL,
            [SubjectId] int NOT NULL,
            [StartDate] date NOT NULL,
            [EndDate] date NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [UpdatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            [RowVersion] rowversion NOT NULL,
            CONSTRAINT [PK_TeachingAssignment] PRIMARY KEY CLUSTERED ([Id]),
            CONSTRAINT [CK_TeachingAssignment_DateRange] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate]),
            CONSTRAINT [CK_TeachingAssignment_UpdatedAtUtc] CHECK ([UpdatedAtUtc] >= [CreatedAtUtc]),
            CONSTRAINT [FK_TeachingAssignment_ClassGroup] FOREIGN KEY ([ClassGroupId])
                REFERENCES [academic].[ClassGroup]([Id]) ON DELETE NO ACTION,
            CONSTRAINT [FK_TeachingAssignment_Subject] FOREIGN KEY ([SubjectId])
                REFERENCES [catalog].[Subject]([Id]) ON DELETE NO ACTION,
            CONSTRAINT [FK_TeachingAssignment_TeacherContract] FOREIGN KEY ([TeacherContractId])
                REFERENCES [staff].[TeacherContract]([Id]) ON DELETE NO ACTION
        );
        CREATE INDEX [IX_TeachingAssignment_ClassGroupId_StartDate_EndDate]
            ON [academic].[TeachingAssignment]([ClassGroupId],[StartDate],[EndDate])
            INCLUDE ([TeacherContractId],[SubjectId]);
        CREATE INDEX [IX_TeachingAssignment_SubjectId] ON [academic].[TeachingAssignment]([SubjectId]);
        CREATE INDEX [IX_TeachingAssignment_TeacherContractId_StartDate_EndDate]
            ON [academic].[TeachingAssignment]([TeacherContractId],[StartDate],[EndDate])
            INCLUDE ([ClassGroupId],[SubjectId]);
        CREATE UNIQUE INDEX [UQ_TeachingAssignment_Contract_Group_Subject]
            ON [academic].[TeachingAssignment]([TeacherContractId],[ClassGroupId],[SubjectId]);
    END;

    -- 14) academic.ClassSchedule (P1)
    IF OBJECT_ID(N'[academic].[ClassSchedule]', N'U') IS NULL
    BEGIN
        CREATE TABLE [academic].[ClassSchedule]
        (
            [TeachingAssignmentId] int NOT NULL,
            [Weekday] tinyint NOT NULL,
            [CreatedAtUtc] datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT [PK_ClassSchedule] PRIMARY KEY CLUSTERED ([TeachingAssignmentId],[Weekday]),
            CONSTRAINT [CK_ClassSchedule_Weekday] CHECK ([Weekday] BETWEEN 1 AND 7),
            CONSTRAINT [FK_ClassSchedule_TeachingAssignment] FOREIGN KEY ([TeachingAssignmentId])
                REFERENCES [academic].[TeachingAssignment]([Id]) ON DELETE NO ACTION
        );
    END;

    ----------------------------------------------------------------------
    -- Triggers (narrow). CREATE OR ALTER is itself idempotent; each is
    -- dispatched through EXEC(N'...') because CREATE TRIGGER must be the
    -- first statement of its batch and this whole script is one batch.
    -- Four are P0; TR_Subject_ProtectCode is the P1 extension.
    ----------------------------------------------------------------------
    EXEC(N'CREATE OR ALTER TRIGGER [catalog].[TR_School_ProtectStableValues] ON [catalog].[School] AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
            CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]) OR
            CONVERT(varbinary(8000),i.[Sector])<>CONVERT(varbinary(8000),d.[Sector]))
            THROW 51001, ''School Code and Sector are immutable.'', 1;
    END');

    EXEC(N'CREATE OR ALTER TRIGGER [catalog].[TR_AcademicYear_ProtectCode] ON [catalog].[AcademicYear] AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
            CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
            THROW 51002, ''AcademicYear Code is immutable.'', 1;
    END');

    EXEC(N'CREATE OR ALTER TRIGGER [catalog].[TR_Grade_ProtectCode] ON [catalog].[Grade] AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
            CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
            THROW 51003, ''Grade Code is immutable.'', 1;
    END');

    EXEC(N'CREATE OR ALTER TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete]
    ON [catalog].[AcademicConfiguration] AFTER DELETE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM deleted)
            THROW 51004, ''AcademicConfiguration cannot be deleted.'', 1;
    END');

    EXEC(N'CREATE OR ALTER TRIGGER [catalog].[TR_Subject_ProtectCode] ON [catalog].[Subject] AFTER UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
            CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
            THROW 51007, ''Subject Code is immutable.'', 1;
    END');

    ----------------------------------------------------------------------
    -- Runtime role: no login, minimal permissions. Reuses the same
    -- "must be a database role" guard as CatalogDatabaseProtections.cs.
    -- No InovaitMigrationOwner extended property here — that bookkeeping
    -- is migration-only and has no relational meaning for this script.
    ----------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM sys.database_principals WHERE [name]=N'inovait_runtime' AND [type]<>'R')
        THROW 51005, 'Principal inovait_runtime must be a database role.', 1;
    IF DATABASE_PRINCIPAL_ID(N'inovait_runtime') IS NULL CREATE ROLE [inovait_runtime];
    GRANT SELECT ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
    DENY INSERT, UPDATE, DELETE ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
    GRANT SELECT, UPDATE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];
    DENY INSERT, DELETE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];

    ----------------------------------------------------------------------
    -- Canonical fictitious seed — mirrors ProductionCatalogSeed.ApplyAsync
    -- exactly: UPDLOCK/HOLDLOCK identity-conflict guard (THROW 51010),
    -- then one guarded IDENTITY_INSERT ON/OFF INSERT per identity table,
    -- then the AcademicConfiguration singleton row. Canonical timestamps
    -- are the fixed '2026-01-01' instant, inserted directly (no follow-up
    -- UPDATE is needed on this path, unlike the migration chain).
    ----------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM [catalog].[School] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND (CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'SCH-001') OR CONVERT(varbinary(8),[Sector])<>CONVERT(varbinary(8),'Public'))) OR ([Id]<>1 AND [Code]='SCH-001')) OR EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'AY-2026')) OR ([Id]<>1 AND [Code]='AY-2026')) OR EXISTS (SELECT 1 FROM [catalog].[Grade] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'G01')) OR ([Id]<>1 AND [Code]='G01')) OR EXISTS (SELECT 1 FROM [catalog].[DocumentType] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'CC')) OR ([Id]<>1 AND [Code]='CC')) THROW 51010,'Canonical catalog seed identity conflict.',1;

    IF NOT EXISTS (SELECT 1 FROM [catalog].[School] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1)
    BEGIN
        SET IDENTITY_INSERT [catalog].[School] ON;
        BEGIN TRY
            INSERT [catalog].[School] ([Id],[Code],[Name],[Sector],[CreatedAtUtc],[UpdatedAtUtc])
                VALUES (1,'SCH-001',N'North Learning Center','Public','2026-01-01','2026-01-01');
            SET IDENTITY_INSERT [catalog].[School] OFF;
        END TRY
        BEGIN CATCH
            SET IDENTITY_INSERT [catalog].[School] OFF;
            THROW;
        END CATCH
    END;

    IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1)
    BEGIN
        SET IDENTITY_INSERT [catalog].[AcademicYear] ON;
        BEGIN TRY
            INSERT [catalog].[AcademicYear] ([Id],[Code],[Name],[StartDate],[EndDate],[CreatedAtUtc],[UpdatedAtUtc])
                VALUES (1,'AY-2026',N'Academic Year 2026','2026-01-01','2026-12-31','2026-01-01','2026-01-01');
            SET IDENTITY_INSERT [catalog].[AcademicYear] OFF;
        END TRY
        BEGIN CATCH
            SET IDENTITY_INSERT [catalog].[AcademicYear] OFF;
            THROW;
        END CATCH
    END;

    IF NOT EXISTS (SELECT 1 FROM [catalog].[Grade] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1)
    BEGIN
        SET IDENTITY_INSERT [catalog].[Grade] ON;
        BEGIN TRY
            INSERT [catalog].[Grade] ([Id],[Code],[Name],[SortOrder],[CreatedAtUtc],[UpdatedAtUtc])
                VALUES (1,'G01',N'First Grade',1,'2026-01-01','2026-01-01');
            SET IDENTITY_INSERT [catalog].[Grade] OFF;
        END TRY
        BEGIN CATCH
            SET IDENTITY_INSERT [catalog].[Grade] OFF;
            THROW;
        END CATCH
    END;

    IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1)
    BEGIN
        SET IDENTITY_INSERT [catalog].[DocumentType] ON;
        BEGIN TRY
            INSERT [catalog].[DocumentType] ([Id],[Code],[Name],[IsActive])
                VALUES (1,'CC',N'Citizenship Card',1);
            SET IDENTITY_INSERT [catalog].[DocumentType] OFF;
        END TRY
        BEGIN CATCH
            SET IDENTITY_INSERT [catalog].[DocumentType] OFF;
            THROW;
        END CATCH
    END;

    IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicConfiguration] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1)
        INSERT [catalog].[AcademicConfiguration] ([Id],[CurrentAcademicYearId]) VALUES (1,1);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
