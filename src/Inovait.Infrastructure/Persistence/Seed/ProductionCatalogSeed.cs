using Inovait.Core.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Infrastructure.Persistence.Seed;

public static class ProductionCatalogSeed
{
    private static readonly DateTime AuditTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<School>().HasData(new
        {
            Id = 1,
            Code = "SCH-001",
            Name = "North Learning Center",
            Sector = SchoolSector.Public,
            CreatedAtUtc = AuditTimestamp,
            UpdatedAtUtc = AuditTimestamp,
        });
        modelBuilder.Entity<AcademicYear>().HasData(new
        {
            Id = 1,
            Code = "AY-2026",
            Name = "Academic Year 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            CreatedAtUtc = AuditTimestamp,
            UpdatedAtUtc = AuditTimestamp,
        });
        modelBuilder.Entity<Grade>().HasData(new
        {
            Id = 1,
            Code = "G01",
            Name = "First Grade",
            SortOrder = (short)1,
            CreatedAtUtc = AuditTimestamp,
            UpdatedAtUtc = AuditTimestamp,
        });
        modelBuilder.Entity<DocumentType>().HasData(new
        {
            Id = (short)1,
            Code = "CC",
            Name = "Citizenship Card",
            IsActive = true,
        });
        modelBuilder.Entity<AcademicConfiguration>().HasData(new
        {
            Id = (byte)1,
            CurrentAcademicYearId = 1,
        });
    }

    public static Task ApplyAsync(InovaitDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Database.ExecuteSqlRawAsync("""
            SET XACT_ABORT ON;
            BEGIN TRY
                SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRANSACTION;
                IF EXISTS (SELECT 1 FROM [catalog].[School] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND (CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'SCH-001') OR CONVERT(varbinary(8),[Sector])<>CONVERT(varbinary(8),'Public'))) OR ([Id]<>1 AND [Code]='SCH-001')) OR EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'AY-2026')) OR ([Id]<>1 AND [Code]='AY-2026')) OR EXISTS (SELECT 1 FROM [catalog].[Grade] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'G01')) OR ([Id]<>1 AND [Code]='G01')) OR EXISTS (SELECT 1 FROM [catalog].[DocumentType] WITH (UPDLOCK,HOLDLOCK) WHERE ([Id]=1 AND CONVERT(varbinary(20),[Code])<>CONVERT(varbinary(20),'CC')) OR ([Id]<>1 AND [Code]='CC')) THROW 51010,'Canonical catalog seed identity conflict.',1;
                IF NOT EXISTS (SELECT 1 FROM [catalog].[School] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1) BEGIN SET IDENTITY_INSERT [catalog].[School] ON; BEGIN TRY INSERT [catalog].[School] ([Id],[Code],[Name],[Sector],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (1,'SCH-001',N'North Learning Center','Public','2026-01-01','2026-01-01'); SET IDENTITY_INSERT [catalog].[School] OFF; END TRY BEGIN CATCH SET IDENTITY_INSERT [catalog].[School] OFF; THROW; END CATCH END;
                IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicYear] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1) BEGIN SET IDENTITY_INSERT [catalog].[AcademicYear] ON; BEGIN TRY INSERT [catalog].[AcademicYear] ([Id],[Code],[Name],[StartDate],[EndDate],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (1,'AY-2026',N'Academic Year 2026','2026-01-01','2026-12-31','2026-01-01','2026-01-01'); SET IDENTITY_INSERT [catalog].[AcademicYear] OFF; END TRY BEGIN CATCH SET IDENTITY_INSERT [catalog].[AcademicYear] OFF; THROW; END CATCH END;
                IF NOT EXISTS (SELECT 1 FROM [catalog].[Grade] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1) BEGIN SET IDENTITY_INSERT [catalog].[Grade] ON; BEGIN TRY INSERT [catalog].[Grade] ([Id],[Code],[Name],[SortOrder],[CreatedAtUtc],[UpdatedAtUtc]) VALUES (1,'G01',N'First Grade',1,'2026-01-01','2026-01-01'); SET IDENTITY_INSERT [catalog].[Grade] OFF; END TRY BEGIN CATCH SET IDENTITY_INSERT [catalog].[Grade] OFF; THROW; END CATCH END;
                IF NOT EXISTS (SELECT 1 FROM [catalog].[DocumentType] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1) BEGIN SET IDENTITY_INSERT [catalog].[DocumentType] ON; BEGIN TRY INSERT [catalog].[DocumentType] ([Id],[Code],[Name],[IsActive]) VALUES (1,'CC',N'Citizenship Card',1); SET IDENTITY_INSERT [catalog].[DocumentType] OFF; END TRY BEGIN CATCH SET IDENTITY_INSERT [catalog].[DocumentType] OFF; THROW; END CATCH END;
                IF NOT EXISTS (SELECT 1 FROM [catalog].[AcademicConfiguration] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]=1) INSERT [catalog].[AcademicConfiguration] ([Id],[CurrentAcademicYearId]) VALUES (1,1);
                COMMIT TRANSACTION;
            END TRY
            BEGIN CATCH
                IF XACT_STATE()<>0 ROLLBACK TRANSACTION; THROW;
            END CATCH
            """, cancellationToken);
    }
}
