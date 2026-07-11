using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Inovait.Infrastructure.Persistence;

public static class CatalogDatabaseProtections
{
    public static IReadOnlyList<string> Commands { get; } =
    [
        """
        CREATE OR ALTER TRIGGER [catalog].[TR_School_ProtectStableValues] ON [catalog].[School] AFTER UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]) OR
                CONVERT(varbinary(8000),i.[Sector])<>CONVERT(varbinary(8000),d.[Sector]))
                THROW 51001, 'School Code and Sector are immutable.', 1;
        END
        """,
        """
        CREATE OR ALTER TRIGGER [catalog].[TR_AcademicYear_ProtectCode] ON [catalog].[AcademicYear] AFTER UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
                THROW 51002, 'AcademicYear Code is immutable.', 1;
        END
        """,
        """
        CREATE OR ALTER TRIGGER [catalog].[TR_Grade_ProtectCode] ON [catalog].[Grade] AFTER UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
                THROW 51003, 'Grade Code is immutable.', 1;
        END
        """,
        """
        CREATE OR ALTER TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete]
        ON [catalog].[AcademicConfiguration] AFTER DELETE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM deleted)
                THROW 51004, 'AcademicConfiguration cannot be deleted.', 1;
        END
        """,
        """
        IF EXISTS (SELECT 1 FROM sys.database_principals WHERE [name]=N'inovait_runtime' AND [type]<>'R')
            THROW 51005, 'Principal inovait_runtime must be a database role.', 1;
        IF DATABASE_PRINCIPAL_ID(N'inovait_runtime') IS NULL CREATE ROLE [inovait_runtime];
        GRANT SELECT ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
        DENY INSERT, UPDATE, DELETE ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
        GRANT SELECT, UPDATE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];
        DENY INSERT, DELETE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];
        """,
    ];

    public static async Task InstallAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        foreach (var command in Commands)
        {
            await database.ExecuteSqlRawAsync(command, cancellationToken);
        }
    }
}
