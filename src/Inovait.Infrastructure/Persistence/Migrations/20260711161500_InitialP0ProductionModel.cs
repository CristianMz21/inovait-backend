using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inovait.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialP0ProductionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "academic");

            migrationBuilder.EnsureSchema(
                name: "people");

            migrationBuilder.EnsureSchema(
                name: "staff");

            migrationBuilder.CreateTable(
                name: "AcademicYear",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYear", x => x.Id);
                    table.CheckConstraint("CK_AcademicYear_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_AcademicYear_DateRange", "[EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_AcademicYear_Name_NotBlank", "LEN(TRIM([Name])) > 0");
                    table.CheckConstraint("CK_AcademicYear_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                    table.CheckConstraint("CK_DocumentType_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_DocumentType_Name_NotBlank", "LEN(TRIM([Name])) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Grade",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    SortOrder = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grade", x => x.Id);
                    table.CheckConstraint("CK_Grade_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_Grade_Name_NotBlank", "LEN(TRIM([Name])) > 0");
                    table.CheckConstraint("CK_Grade_SortOrder", "[SortOrder] > 0");
                    table.CheckConstraint("CK_Grade_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "School",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    Sector = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_School", x => x.Id);
                    table.CheckConstraint("CK_School_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_School_Name_NotBlank", "LEN(TRIM([Name])) > 0");
                    table.CheckConstraint("CK_School_Sector", "[Sector] IN ('Public','Private')");
                    table.CheckConstraint("CK_School_Sector_NotBlank", "LEN(TRIM([Sector])) > 0");
                    table.CheckConstraint("CK_School_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                });

            migrationBuilder.CreateTable(
                name: "AcademicConfiguration",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrentAcademicYearId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicConfiguration", x => x.Id);
                    table.CheckConstraint("CK_AcademicConfiguration_Singleton", "[Id] = 1");
                    table.ForeignKey(
                        name: "FK_AcademicConfiguration_AcademicYear",
                        column: x => x.CurrentAcademicYearId,
                        principalSchema: "catalog",
                        principalTable: "AcademicYear",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Person",
                schema: "people",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeId = table.Column<short>(type: "smallint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    FirstNames = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    LastNames = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.Id);
                    table.CheckConstraint("CK_Person_DocumentNumber_NotBlank", "LEN(TRIM([DocumentNumber])) > 0");
                    table.CheckConstraint("CK_Person_FirstNames_NotBlank", "LEN(TRIM([FirstNames])) > 0");
                    table.CheckConstraint("CK_Person_LastNames_NotBlank", "LEN(TRIM([LastNames])) > 0");
                    table.CheckConstraint("CK_Person_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_Person_DocumentType",
                        column: x => x.DocumentTypeId,
                        principalSchema: "catalog",
                        principalTable: "DocumentType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClassGroup",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    GradeId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroup", x => x.Id);
                    table.UniqueConstraint("UQ_ClassGroup_Id_AcademicYear_ForEnrollment", x => new { x.Id, x.AcademicYearId });
                    table.CheckConstraint("CK_ClassGroup_Code_NotBlank", "LEN(TRIM([Code])) > 0");
                    table.CheckConstraint("CK_ClassGroup_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ClassGroup_AcademicYear",
                        column: x => x.AcademicYearId,
                        principalSchema: "catalog",
                        principalTable: "AcademicYear",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassGroup_Grade",
                        column: x => x.GradeId,
                        principalSchema: "catalog",
                        principalTable: "Grade",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClassGroup_School",
                        column: x => x.SchoolId,
                        principalSchema: "catalog",
                        principalTable: "School",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Student",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Student_Person",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Person",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Teacher",
                schema: "people",
                columns: table => new
                {
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teacher", x => x.PersonId);
                    table.CheckConstraint("CK_Teacher_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_Teacher_Person",
                        column: x => x.PersonId,
                        principalSchema: "people",
                        principalTable: "Person",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Enrollment",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentPersonId = table.Column<int>(type: "int", nullable: false),
                    ClassGroupId = table.Column<int>(type: "int", nullable: false),
                    AcademicYearId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollment_ClassGroupId_AcademicYearId",
                        columns: x => new { x.ClassGroupId, x.AcademicYearId },
                        principalSchema: "academic",
                        principalTable: "ClassGroup",
                        principalColumns: new[] { "Id", "AcademicYearId" });
                    table.ForeignKey(
                        name: "FK_Enrollment_Student",
                        column: x => x.StudentPersonId,
                        principalSchema: "people",
                        principalTable: "Student",
                        principalColumn: "PersonId");
                });

            migrationBuilder.CreateTable(
                name: "TeacherContract",
                schema: "staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeacherPersonId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true, collation: "Latin1_General_100_CI_AS"),
                    CancellationEffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherContract", x => x.Id);
                    table.CheckConstraint("CK_TeacherContract_CancellationEffectiveDate", "[CancellationEffectiveDate] IS NULL OR ([CancellationEffectiveDate] >= [StartDate] AND ([EndDate] IS NULL OR [CancellationEffectiveDate] <= [EndDate]))");
                    table.CheckConstraint("CK_TeacherContract_CancellationReason_NotBlank", "[CancellationReason] IS NULL OR LEN(TRIM([CancellationReason])) > 0");
                    table.CheckConstraint("CK_TeacherContract_DateRange", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.CheckConstraint("CK_TeacherContract_Status", "[Status] IN ('Confirmed','Cancelled')");
                    table.CheckConstraint("CK_TeacherContract_Status_NotBlank", "LEN(TRIM([Status])) > 0");
                    table.CheckConstraint("CK_TeacherContract_StatusCancellation", "([Status]='Confirmed' AND [CancelledAtUtc] IS NULL AND [CancellationReason] IS NULL AND [CancellationEffectiveDate] IS NULL) OR ([Status]='Cancelled' AND [CancelledAtUtc] IS NOT NULL AND [CancellationReason] IS NOT NULL AND [CancellationEffectiveDate] IS NOT NULL)");
                    table.CheckConstraint("CK_TeacherContract_UpdatedAtUtc", "[UpdatedAtUtc] >= [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_TeacherContract_School",
                        column: x => x.SchoolId,
                        principalSchema: "catalog",
                        principalTable: "School",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TeacherContract_Teacher",
                        column: x => x.TeacherPersonId,
                        principalSchema: "people",
                        principalTable: "Teacher",
                        principalColumn: "PersonId");
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "AcademicYear",
                columns: new[] { "Id", "Code", "EndDate", "Name", "StartDate" },
                values: new object[] { 1, "AY-2026", new DateOnly(2026, 12, 31), "Academic Year 2026", new DateOnly(2026, 1, 1) });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "DocumentType",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[] { (short)1, "CC", true, "Citizenship Card" });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "Grade",
                columns: new[] { "Id", "Code", "Name", "SortOrder" },
                values: new object[] { 1, "G01", "First Grade", (short)1 });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "School",
                columns: new[] { "Id", "Code", "Name", "Sector" },
                values: new object[] { 1, "SCH-001", "North Learning Center", "Public" });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "AcademicConfiguration",
                columns: new[] { "Id", "CurrentAcademicYearId" },
                values: new object[] { (byte)1, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicConfiguration_CurrentAcademicYearId",
                schema: "catalog",
                table: "AcademicConfiguration",
                column: "CurrentAcademicYearId");

            migrationBuilder.CreateIndex(
                name: "UQ_AcademicYear_Code",
                schema: "catalog",
                table: "AcademicYear",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AcademicYear_Name",
                schema: "catalog",
                table: "AcademicYear",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroup_AcademicYearId_GradeId_SchoolId",
                schema: "academic",
                table: "ClassGroup",
                columns: new[] { "AcademicYearId", "GradeId", "SchoolId" })
                .Annotation("SqlServer:Include", new[] { "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroup_GradeId",
                schema: "academic",
                table: "ClassGroup",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "UQ_ClassGroup_Context",
                schema: "academic",
                table: "ClassGroup",
                columns: new[] { "SchoolId", "AcademicYearId", "GradeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DocumentType_Code",
                schema: "catalog",
                table: "DocumentType",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_ClassGroupId_AcademicYearId",
                schema: "academic",
                table: "Enrollment",
                columns: new[] { "ClassGroupId", "AcademicYearId" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollment_ClassGroupId_StudentPersonId",
                schema: "academic",
                table: "Enrollment",
                columns: new[] { "ClassGroupId", "StudentPersonId" })
                .Annotation("SqlServer:Include", new[] { "AcademicYearId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UQ_Enrollment_StudentPersonId_AcademicYearId",
                schema: "academic",
                table: "Enrollment",
                columns: new[] { "StudentPersonId", "AcademicYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Grade_Code",
                schema: "catalog",
                table: "Grade",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Grade_Name",
                schema: "catalog",
                table: "Grade",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Grade_SortOrder",
                schema: "catalog",
                table: "Grade",
                column: "SortOrder",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Person_LastNames_FirstNames_Id",
                schema: "people",
                table: "Person",
                columns: new[] { "LastNames", "FirstNames", "Id" })
                .Annotation("SqlServer:Include", new[] { "DocumentTypeId", "DocumentNumber", "BirthDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_Person_DocumentTypeId_DocumentNumber",
                schema: "people",
                table: "Person",
                columns: new[] { "DocumentTypeId", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_School_Code",
                schema: "catalog",
                table: "School",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_School_Name",
                schema: "catalog",
                table: "School",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContract_SchoolId_StartDate_EndDate",
                schema: "staff",
                table: "TeacherContract",
                columns: new[] { "SchoolId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "TeacherPersonId", "Status", "CancellationEffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherContract_TeacherPersonId_StartDate_EndDate",
                schema: "staff",
                table: "TeacherContract",
                columns: new[] { "TeacherPersonId", "StartDate", "EndDate" })
                .Annotation("SqlServer:Include", new[] { "SchoolId", "Status", "CancelledAtUtc", "CancellationReason", "CancellationEffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "UQ_TeacherContract_Exact",
                schema: "staff",
                table: "TeacherContract",
                columns: new[] { "TeacherPersonId", "SchoolId", "StartDate", "EndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicConfiguration",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Enrollment",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "TeacherContract",
                schema: "staff");

            migrationBuilder.DropTable(
                name: "ClassGroup",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "Student",
                schema: "people");

            migrationBuilder.DropTable(
                name: "Teacher",
                schema: "people");

            migrationBuilder.DropTable(
                name: "AcademicYear",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Grade",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "School",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Person",
                schema: "people");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "catalog");
        }
    }
}
