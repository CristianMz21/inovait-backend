using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inovait.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddP0DatabaseProtections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @CanonicalSeedTimestamp datetime2(3)='2026-01-01T00:00:00.000';
                UPDATE [catalog].[School] SET [CreatedAtUtc]=@CanonicalSeedTimestamp,[UpdatedAtUtc]=@CanonicalSeedTimestamp WHERE [Id]=1;
                UPDATE [catalog].[AcademicYear] SET [CreatedAtUtc]=@CanonicalSeedTimestamp,[UpdatedAtUtc]=@CanonicalSeedTimestamp WHERE [Id]=1;
                UPDATE [catalog].[Grade] SET [CreatedAtUtc]=@CanonicalSeedTimestamp,[UpdatedAtUtc]=@CanonicalSeedTimestamp WHERE [Id]=1;
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [catalog].[TR_School_ProtectStableValues] ON [catalog].[School] AFTER UPDATE AS
                BEGIN SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                        CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]) OR
                        CONVERT(varbinary(8000),i.[Sector])<>CONVERT(varbinary(8000),d.[Sector]))
                        THROW 51001, ''School Code and Sector are immutable.'', 1;
                END')
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [catalog].[TR_AcademicYear_ProtectCode] ON [catalog].[AcademicYear] AFTER UPDATE AS
                BEGIN SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                        CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
                        THROW 51002, ''AcademicYear Code is immutable.'', 1;
                END')
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [catalog].[TR_Grade_ProtectCode] ON [catalog].[Grade] AFTER UPDATE AS
                BEGIN SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                        CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
                        THROW 51003, ''Grade Code is immutable.'', 1;
                END')
                """);
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [catalog].[TR_AcademicConfiguration_PreventDelete]
                ON [catalog].[AcademicConfiguration] AFTER DELETE AS
                BEGIN SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM deleted)
                        THROW 51004, ''AcademicConfiguration cannot be deleted.'', 1;
                END')
                """);
            migrationBuilder.Sql("""
                DECLARE @RoleOwnerValue nvarchar(128)=N'20260711161518_AddP0DatabaseProtections';
                DECLARE @RoleId int=DATABASE_PRINCIPAL_ID(N'inovait_runtime');
                IF @RoleId IS NULL BEGIN
                    CREATE ROLE [inovait_runtime];
                    EXEC sys.sp_addextendedproperty @name=N'InovaitMigrationOwner',@value=@RoleOwnerValue,@level0type=N'USER',@level0name=N'inovait_runtime';
                    SET @RoleId=DATABASE_PRINCIPAL_ID(N'inovait_runtime');
                END
                ELSE IF NOT EXISTS (SELECT 1 FROM sys.database_principals p JOIN sys.extended_properties ep ON ep.[class]=4 AND ep.[major_id]=p.[principal_id] WHERE p.[principal_id]=@RoleId AND p.[type]='R' AND ep.[name]=N'InovaitMigrationOwner' AND CONVERT(nvarchar(128),ep.[value])=@RoleOwnerValue)
                    THROW 51005, 'Principal inovait_runtime is not owned by this migration.', 1;
                GRANT SELECT ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
                DENY INSERT, UPDATE, DELETE ON OBJECT::[catalog].[DocumentType] TO [inovait_runtime];
                GRANT SELECT, UPDATE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];
                DENY INSERT, DELETE ON OBJECT::[catalog].[AcademicConfiguration] TO [inovait_runtime];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS [catalog].[TR_School_ProtectStableValues];
                DROP TRIGGER IF EXISTS [catalog].[TR_AcademicYear_ProtectCode];
                DROP TRIGGER IF EXISTS [catalog].[TR_Grade_ProtectCode];
                DROP TRIGGER IF EXISTS [catalog].[TR_AcademicConfiguration_PreventDelete];
                DECLARE @RoleOwnerValue nvarchar(128)=N'20260711161518_AddP0DatabaseProtections';
                DECLARE @RoleId int=DATABASE_PRINCIPAL_ID(N'inovait_runtime');
                IF @RoleId IS NOT NULL BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.database_principals p JOIN sys.extended_properties ep ON ep.[class]=4 AND ep.[major_id]=p.[principal_id] WHERE p.[principal_id]=@RoleId AND p.[type]='R' AND ep.[name]=N'InovaitMigrationOwner' AND CONVERT(nvarchar(128),ep.[value])=@RoleOwnerValue)
                        THROW 51006, 'Principal inovait_runtime is not owned by this migration.', 1;
                    REVOKE SELECT,INSERT,UPDATE,DELETE ON OBJECT::[catalog].[DocumentType] FROM [inovait_runtime];
                    REVOKE SELECT,INSERT,UPDATE,DELETE ON OBJECT::[catalog].[AcademicConfiguration] FROM [inovait_runtime];
                    DECLARE @DropMembers nvarchar(max)=(SELECT STRING_AGG(CONVERT(nvarchar(max),N'ALTER ROLE [inovait_runtime] DROP MEMBER '+QUOTENAME(member.[name])),N';') FROM sys.database_role_members membership JOIN sys.database_principals member ON member.[principal_id]=membership.[member_principal_id] WHERE membership.[role_principal_id]=@RoleId);
                    IF @DropMembers IS NOT NULL EXEC sys.sp_executesql @DropMembers;
                    DROP ROLE [inovait_runtime];
                END
                """);
        }
    }
}
