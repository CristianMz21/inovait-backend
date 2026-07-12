-- Safe cleanup for the fictitious LOCAL-EVALUATION demo dataset seeded by
-- database/seed-demo.sql. Deletes children before parents, scoped strictly
-- to the demo namespace (Person.DocumentNumber LIKE 'DEMO-EST-%' /
-- 'DEMO-DOC-%', ClassGroup/Subject/Grade.Code LIKE 'DEMO-%',
-- AcademicYear.Code LIKE 'DEMO-AY-%', School.Code LIKE 'COL-%'). Never
-- touches canonical rows (SCH-001 / AY-2026 / G01 / CC) or DocumentTypes
-- (DNI/PAS/CE are kept -- the frontend enrollment form still needs them
-- after a reset). No DROP/EnsureDeleted anywhere.
--
-- Enrollment is deleted by EITHER a DEMO student OR a DEMO class group
-- match (not just the student side): if an evaluator used
-- requests/evaluator.http to POST a brand-new non-DEMO student into a demo
-- ClassGroup, that leftover enrollment would otherwise block the ClassGroup
-- DELETE below with a foreign-key violation. The extra non-DEMO
-- Person/Student row is intentionally left behind (harmless, out of the
-- demo namespace) -- only its dangling enrollment into demo infrastructure
-- is cleaned up.
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    DELETE x
    FROM [academic].[ClassSchedule] x
    JOIN [academic].[TeachingAssignment] ta ON ta.[Id] = x.[TeachingAssignmentId]
    JOIN [academic].[ClassGroup] cg ON cg.[Id] = ta.[ClassGroupId]
    JOIN [staff].[TeacherContract] tc ON tc.[Id] = ta.[TeacherContractId]
    JOIN [people].[Person] p ON p.[Id] = tc.[TeacherPersonId]
    WHERE cg.[Code] LIKE 'DEMO-%' OR p.[DocumentNumber] LIKE 'DEMO-DOC-%';
    PRINT CONCAT('ClassSchedule: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE x
    FROM [academic].[TeachingAssignment] x
    JOIN [academic].[ClassGroup] cg ON cg.[Id] = x.[ClassGroupId]
    JOIN [staff].[TeacherContract] tc ON tc.[Id] = x.[TeacherContractId]
    JOIN [people].[Person] p ON p.[Id] = tc.[TeacherPersonId]
    JOIN [catalog].[Subject] sub ON sub.[Id] = x.[SubjectId]
    WHERE cg.[Code] LIKE 'DEMO-%' OR p.[DocumentNumber] LIKE 'DEMO-DOC-%' OR sub.[Code] LIKE 'DEMO-%';
    PRINT CONCAT('TeachingAssignment: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE x
    FROM [staff].[TeacherContract] x
    JOIN [people].[Person] p ON p.[Id] = x.[TeacherPersonId]
    JOIN [catalog].[School] sc ON sc.[Id] = x.[SchoolId]
    WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%' OR sc.[Code] LIKE 'COL-%';
    PRINT CONCAT('TeacherContract: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE x
    FROM [academic].[Enrollment] x
    LEFT JOIN [academic].[ClassGroup] cg ON cg.[Id] = x.[ClassGroupId]
    LEFT JOIN [people].[Person] p ON p.[Id] = x.[StudentPersonId]
    WHERE (cg.[Code] LIKE 'DEMO-%') OR (p.[DocumentNumber] LIKE 'DEMO-EST-%');
    PRINT CONCAT('Enrollment: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE x
    FROM [people].[Student] x
    JOIN [people].[Person] p ON p.[Id] = x.[PersonId]
    WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%';
    PRINT CONCAT('Student: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE x
    FROM [people].[Teacher] x
    JOIN [people].[Person] p ON p.[Id] = x.[PersonId]
    WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%';
    PRINT CONCAT('Teacher: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [people].[Person]
    WHERE [DocumentNumber] LIKE 'DEMO-EST-%' OR [DocumentNumber] LIKE 'DEMO-DOC-%';
    PRINT CONCAT('Person: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [academic].[ClassGroup] WHERE [Code] LIKE 'DEMO-%';
    PRINT CONCAT('ClassGroup: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [catalog].[Subject] WHERE [Code] LIKE 'DEMO-%';
    PRINT CONCAT('Subject: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [catalog].[Grade] WHERE [Code] LIKE 'DEMO-%';
    PRINT CONCAT('Grade: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [catalog].[AcademicYear] WHERE [Code] LIKE 'DEMO-AY-%';
    PRINT CONCAT('AcademicYear: deleted ', @@ROWCOUNT, ' row(s).');

    DELETE FROM [catalog].[School] WHERE [Code] LIKE 'COL-%';
    PRINT CONCAT('School: deleted ', @@ROWCOUNT, ' row(s).');

    COMMIT TRANSACTION;
    PRINT 'Demo reset complete: DEMO-%/COL-% namespace cleared; canonical seed and DocumentTypes untouched.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
