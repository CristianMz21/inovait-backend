using System.Diagnostics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.Staff;
using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Persistence;
using Inovait.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Inovait.IntegrationTests.Persistence;

/// <summary>
/// Exercises <c>database/seed-demo.sql</c> and <c>database/reset-demo.sql</c> end to end against a
/// dedicated migrated database: applies the seed twice (idempotency), asserts every invariant of the
/// strict evaluator dataset from the seeding plan (24 students / 40 enrollments / school split
/// 10/6/5/3 / exactly 3 current-year Quinto enrollments @ COL-PUB-001 / 8 teachers / 8 active
/// contracts across 6 distinct teachers split 4 public / 3 private / one expired / one future / the
/// DEMO-EST-006 three-year, three-teacher history), then resets and re-seeds to prove the cycle is
/// repeatable. Uses its own database (via <see cref="SqlServerFixture"/>) so it never interferes with
/// the P0/P1 gate suites. Carries no <c>Priority</c> trait on purpose (see plan section on gate
/// totals) -- only <c>Category=DemoSeed</c>, so <c>run-p0-tests.sh</c>/<c>run-p1-tests.sh</c> never
/// pick these tests up.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "DemoSeed")]
public sealed class DemoDataSeedTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private InovaitDbContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"InovaitDemoSeed_{Guid.NewGuid():N}",
        }.ConnectionString;
        _context = new InovaitDbContext(new DbContextOptionsBuilder<InovaitDbContext>()
            .UseSqlServer(connectionString).Options);
        await _context.Database.MigrateAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    [Trait("Evidence", "IT-SEED-DEMO-DATASET")]
    public async Task SeedDemoScript_ProducesTheStrictEvaluatorDatasetAndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seedScript = await LoadRepoFileAsync("database", "seed-demo.sql", cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(seedScript, cancellationToken);
        var firstRunCounts = await CaptureCountsAsync(cancellationToken);
        await AssertDatasetInvariantsAsync(cancellationToken);

        // Idempotency: a second run against the already-seeded database must not throw (no unique
        // key/THROW conflicts) and must not duplicate a single row.
        await _context.Database.ExecuteSqlRawAsync(seedScript, cancellationToken);
        var secondRunCounts = await CaptureCountsAsync(cancellationToken);
        Assert.Equal(firstRunCounts, secondRunCounts);
        await AssertDatasetInvariantsAsync(cancellationToken);
    }

    [Fact]
    [Trait("Evidence", "IT-SEED-DEMO-RESET")]
    public async Task ResetDemoScript_ClearsTheDemoNamespaceAndPreservesCanonicalRows_ThenReseedIsGreenAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seedScript = await LoadRepoFileAsync("database", "seed-demo.sql", cancellationToken);
        var resetScript = await LoadRepoFileAsync("database", "reset-demo.sql", cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(seedScript, cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(resetScript, cancellationToken);

        Assert.Equal(0, await CountAsync(
            "SELECT COUNT(*) AS [Value] FROM [people].[Person] WHERE [DocumentNumber] LIKE 'DEMO-EST-%' OR [DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken));
        Assert.Equal(0, await CountAsync(
            "SELECT COUNT(*) AS [Value] FROM [academic].[Enrollment] en JOIN [academic].[ClassGroup] cg ON cg.[Id]=en.[ClassGroupId] WHERE cg.[Code] LIKE 'DEMO-%'", cancellationToken));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) AS [Value] FROM [academic].[ClassGroup] WHERE [Code] LIKE 'DEMO-%'", cancellationToken));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[Subject] WHERE [Code] LIKE 'DEMO-%'", cancellationToken));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[Grade] WHERE [Code] LIKE 'DEMO-%'", cancellationToken));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[AcademicYear] WHERE [Code] LIKE 'DEMO-AY-%'", cancellationToken));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[School] WHERE [Code] LIKE 'COL-%'", cancellationToken));
        Assert.Equal(0, await CountAsync(
            "SELECT COUNT(*) AS [Value] FROM [staff].[TeacherContract] tc JOIN [people].[Person] p ON p.[Id]=tc.[TeacherPersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken));

        // Canonical rows (P0 seed) are untouched.
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[School] WHERE [Id]=1 AND [Code]='SCH-001'", cancellationToken));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[AcademicYear] WHERE [Id]=1 AND [Code]='AY-2026'", cancellationToken));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[Grade] WHERE [Id]=1 AND [Code]='G01'", cancellationToken));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[AcademicConfiguration] WHERE [Id]=1 AND [CurrentAcademicYearId]=1", cancellationToken));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[DocumentType] WHERE [Id]=1 AND [Code]='CC'", cancellationToken));

        // DNI/PAS/CE (frontend enrollment form) survive the reset -- only the DEMO-%/COL-% namespace
        // is cleared, and these DocumentTypes are still required after a reset.
        Assert.Equal(3, await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[DocumentType] WHERE [Code] IN ('DNI','PAS','CE')", cancellationToken));

        // Re-seed after reset must be green again, from a clean demo namespace.
        await _context.Database.ExecuteSqlRawAsync(seedScript, cancellationToken);
        await AssertDatasetInvariantsAsync(cancellationToken);
    }

    private async Task AssertDatasetInvariantsAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var students = await (
            from student in _context.Students.AsNoTracking()
            join person in _context.People.AsNoTracking() on student.PersonId equals person.Id
            where person.DocumentNumber.StartsWith("DEMO-EST-")
            select new { person.Id, person.DocumentNumber, person.BirthDate })
            .ToListAsync(cancellationToken);
        Assert.Equal(24, students.Count);

        var ages = students.Select(student => AgeCalculator.Calculate(student.BirthDate, today)).ToList();
        Assert.Equal(8, ages.Count(age => age is >= 3 and <= 7));
        Assert.Equal(8, ages.Count(age => age is >= 8 and <= 12));
        Assert.Equal(8, ages.Count(age => age >= 13));
        Assert.Contains(3, ages);
        Assert.Contains(7, ages);
        Assert.Contains(8, ages);
        Assert.Contains(12, ages);
        Assert.Contains(13, ages);

        var studentPersonIds = students.Select(student => student.Id).ToArray();
        var enrollments = await _context.Enrollments.AsNoTracking()
            .Where(enrollment => studentPersonIds.Contains(enrollment.StudentPersonId))
            .ToListAsync(cancellationToken);
        Assert.Equal(40, enrollments.Count);

        var currentAcademicYearId = await _context.AcademicConfigurations.AsNoTracking()
            .Where(configuration => configuration.Id == 1)
            .Select(configuration => configuration.CurrentAcademicYearId)
            .SingleAsync(cancellationToken);
        var currentYearEnrollments = enrollments.Where(enrollment => enrollment.AcademicYearId == currentAcademicYearId).ToList();
        Assert.Equal(24, currentYearEnrollments.Count);

        var classGroups = await _context.ClassGroups.AsNoTracking()
            .ToDictionaryAsync(classGroup => classGroup.Id, cancellationToken);
        var schools = await _context.Schools.AsNoTracking()
            .ToDictionaryAsync(school => school.Id, cancellationToken);
        var grades = await _context.Grades.AsNoTracking()
            .ToDictionaryAsync(grade => grade.Id, cancellationToken);

        var currentYearBySchoolCode = currentYearEnrollments
            .GroupBy(enrollment => schools[classGroups[enrollment.ClassGroupId].SchoolId].Code)
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(10, currentYearBySchoolCode["COL-PUB-001"]);
        Assert.Equal(6, currentYearBySchoolCode["COL-PUB-002"]);
        Assert.Equal(5, currentYearBySchoolCode["COL-PRI-001"]);
        Assert.Equal(3, currentYearBySchoolCode["COL-PRI-002"]);

        var quintoPub001Count = currentYearEnrollments.Count(enrollment =>
            schools[classGroups[enrollment.ClassGroupId].SchoolId].Code == "COL-PUB-001"
            && grades[classGroups[enrollment.ClassGroupId].GradeId].Code == "DEMO-G05");
        Assert.Equal(3, quintoPub001Count);

        var teacherPersonIds = await (
            from teacher in _context.Teachers.AsNoTracking()
            join person in _context.People.AsNoTracking() on teacher.PersonId equals person.Id
            where person.DocumentNumber.StartsWith("DEMO-DOC-")
            select person.Id)
            .ToListAsync(cancellationToken);
        Assert.Equal(8, teacherPersonIds.Count);

        var contracts = await _context.TeacherContracts.AsNoTracking()
            .Where(contract => teacherPersonIds.Contains(contract.TeacherPersonId))
            .ToListAsync(cancellationToken);
        Assert.Equal(10, contracts.Count);

        var activeContracts = contracts.Where(contract => contract.GetEffectiveStatus(today) == EffectiveContractStatus.Effective).ToList();
        Assert.Equal(8, activeContracts.Count);
        Assert.Equal(6, activeContracts.Select(contract => contract.TeacherPersonId).Distinct().Count());

        var activePublicTeachers = activeContracts
            .Where(contract => schools[contract.SchoolId].Sector == SchoolSector.Public)
            .Select(contract => contract.TeacherPersonId).Distinct().Count();
        var activePrivateTeachers = activeContracts
            .Where(contract => schools[contract.SchoolId].Sector == SchoolSector.Private)
            .Select(contract => contract.TeacherPersonId).Distinct().Count();
        Assert.Equal(4, activePublicTeachers);
        Assert.Equal(3, activePrivateTeachers);

        Assert.Contains(contracts, contract => contract.GetEffectiveStatus(today) == EffectiveContractStatus.Expired);
        Assert.Contains(contracts, contract => contract.GetEffectiveStatus(today) == EffectiveContractStatus.Upcoming);

        var est006PersonId = students.Single(student => student.DocumentNumber == "DEMO-EST-006").Id;
        var est006History = await (
            from enrollment in _context.Enrollments.AsNoTracking()
            where enrollment.StudentPersonId == est006PersonId
            join classGroup in _context.ClassGroups.AsNoTracking() on enrollment.ClassGroupId equals classGroup.Id
            join grade in _context.Grades.AsNoTracking() on classGroup.GradeId equals grade.Id
            join academicYear in _context.AcademicYears.AsNoTracking() on enrollment.AcademicYearId equals academicYear.Id
            orderby academicYear.StartDate
            select new { classGroup.Id, classGroup.Code, GradeCode = grade.Code })
            .ToListAsync(cancellationToken);
        Assert.Equal(3, est006History.Count);
        Assert.Equal(["DEMO-G03", "DEMO-G04", "DEMO-G05"], est006History.Select(row => row.GradeCode).ToArray());
        // Code scheme DEMO-<esc(2)><gg(2)><grp(1)>-<yy(2)>: the group letter is always at index 9.
        Assert.Equal(["A", "B", "A"], est006History.Select(row => row.Code.Substring(9, 1)).ToArray());

        var est006ClassGroupIds = est006History.Select(row => row.Id).ToArray();
        var est006TeacherPersonIds = await (
            from assignment in _context.TeachingAssignments.AsNoTracking()
            where est006ClassGroupIds.Contains(assignment.ClassGroupId)
            join contract in _context.TeacherContracts.AsNoTracking() on assignment.TeacherContractId equals contract.Id
            select contract.TeacherPersonId)
            .Distinct()
            .ToListAsync(cancellationToken);
        Assert.Equal(3, est006TeacherPersonIds.Count);
    }

    private async Task<int[]> CaptureCountsAsync(CancellationToken cancellationToken) =>
    [
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [people].[Person] WHERE [DocumentNumber] LIKE 'DEMO-EST-%' OR [DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [people].[Student] st JOIN [people].[Person] p ON p.[Id]=st.[PersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [people].[Teacher] t JOIN [people].[Person] p ON p.[Id]=t.[PersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[Grade] WHERE [Code] LIKE 'DEMO-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[Subject] WHERE [Code] LIKE 'DEMO-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[School] WHERE [Code] LIKE 'COL-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[AcademicYear] WHERE [Code] LIKE 'DEMO-AY-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [academic].[ClassGroup] WHERE [Code] LIKE 'DEMO-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [academic].[Enrollment] en JOIN [people].[Person] p ON p.[Id]=en.[StudentPersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-EST-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [staff].[TeacherContract] tc JOIN [people].[Person] p ON p.[Id]=tc.[TeacherPersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [academic].[TeachingAssignment] ta JOIN [staff].[TeacherContract] tc ON tc.[Id]=ta.[TeacherContractId] JOIN [people].[Person] p ON p.[Id]=tc.[TeacherPersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [academic].[ClassSchedule] cs JOIN [academic].[TeachingAssignment] ta ON ta.[Id]=cs.[TeachingAssignmentId] JOIN [staff].[TeacherContract] tc ON tc.[Id]=ta.[TeacherContractId] JOIN [people].[Person] p ON p.[Id]=tc.[TeacherPersonId] WHERE p.[DocumentNumber] LIKE 'DEMO-DOC-%'", cancellationToken),
        await CountAsync("SELECT COUNT(*) AS [Value] FROM [catalog].[DocumentType] WHERE [Code] IN ('DNI','PAS','CE')", cancellationToken),
    ];

    private async Task<int> CountAsync(string sql, CancellationToken cancellationToken) =>
        await _context.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);

    private static async Task<string> LoadRepoFileAsync(string subdirectory, string fileName, CancellationToken cancellationToken)
    {
        var repositoryRoot = (await RunGitAsync(AppContext.BaseDirectory, "rev-parse --show-toplevel", cancellationToken)).Trim();
        return await File.ReadAllTextAsync(Path.Combine(repositoryRoot, subdirectory, fileName), cancellationToken);
    }

    private static async Task<string> RunGitAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }
}
