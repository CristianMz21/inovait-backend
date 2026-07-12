using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inovait.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddP1TeachingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subject", x => x.Id);
                    table.CheckConstraint("CK_Subject_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_Subject_Name_NotBlank", "LEN(TRIM([Name])) > 0");
                    table.CheckConstraint("CK_Subject_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "TeachingAssignment",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherContractId = table.Column<int>(type: "int", nullable: false),
                    ClassGroupId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeachingAssignment", x => x.Id);
                    table.CheckConstraint("CK_TeachingAssignment_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_TeachingAssignment_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_TeachingAssignment_ClassGroup",
                        column: x => x.ClassGroupId,
                        principalSchema: "academic",
                        principalTable: "ClassGroup",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeachingAssignment_Subject",
                        column: x => x.SubjectId,
                        principalSchema: "catalog",
                        principalTable: "Subject",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeachingAssignment_TeacherContract",
                        column: x => x.TeacherContractId,
                        principalSchema: "staff",
                        principalTable: "TeacherContract",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClassSchedule",
                schema: "academic",
                columns: table => new
                {
                    TeachingAssignmentId = table.Column<int>(type: "int", nullable: false),
                    Weekday = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSchedule", x => new { x.TeachingAssignmentId, x.Weekday });
                    table.CheckConstraint("CK_ClassSchedule_Weekday", "[Weekday] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_ClassSchedule_TeachingAssignment",
                        column: x => x.TeachingAssignmentId,
                        principalSchema: "academic",
                        principalTable: "TeachingAssignment",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Subject_Code",
                schema: "catalog",
                table: "Subject",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Subject_Name",
                schema: "catalog",
                table: "Subject",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_ClassGroupId_StartDate_EndDate",
                schema: "academic",
                table: "TeachingAssignment",
                columns: new[] { "ClassGroupId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "TeacherContractId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_SubjectId",
                schema: "academic",
                table: "TeachingAssignment",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeachingAssignment_TeacherContractId_StartDate_EndDate",
                schema: "academic",
                table: "TeachingAssignment",
                columns: new[] { "TeacherContractId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "ClassGroupId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "UQ_TeachingAssignment_Contract_Group_Subject",
                schema: "academic",
                table: "TeachingAssignment",
                columns: new[] { "TeacherContractId", "ClassGroupId", "SubjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassSchedule",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "TeachingAssignment",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "catalog");
        }
    }
}
