using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inovait.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddP1DatabaseProtections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                EXEC(N'CREATE TRIGGER [catalog].[TR_Subject_ProtectCode] ON [catalog].[Subject] AFTER UPDATE AS
                BEGIN SET NOCOUNT ON;
                    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.[Id]=d.[Id] WHERE
                        CONVERT(varbinary(8000),i.[Code])<>CONVERT(varbinary(8000),d.[Code]))
                        THROW 51007, ''Subject Code is immutable.'', 1;
                END')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS [catalog].[TR_Subject_ProtectCode];
                """);
        }
    }
}
