using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.Staff;
using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

public sealed class ReportReadService(InovaitDbContext context)
{
    public async Task<AgeDistributionQueryResult> GetAgeDistributionAsync(
        int academicYearId, int? schoolId, int? gradeId, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var classGroups = context.ClassGroups.AsNoTracking()
            .Where(group => group.AcademicYearId == academicYearId);
        if (schoolId is int school)
        {
            classGroups = classGroups.Where(group => group.SchoolId == school);
        }

        if (gradeId is int grade)
        {
            classGroups = classGroups.Where(group => group.GradeId == grade);
        }

        var classGroupIds = classGroups.Select(group => group.Id);
        var birthDates = await (
            from enrollment in context.Enrollments.AsNoTracking()
            where enrollment.AcademicYearId == academicYearId && classGroupIds.Contains(enrollment.ClassGroupId)
            join person in context.People.AsNoTracking() on enrollment.StudentPersonId equals person.Id
            select person.BirthDate)
            .ToListAsync(cancellationToken);

        if (birthDates.Any(birthDate => birthDate > asOfDate))
        {
            return AgeDistributionQueryResult.Invalid;
        }

        var ages = birthDates.Select(birthDate => AgeCalculator.Calculate(birthDate, asOfDate)).ToList();
        var response = new AgeDistributionResponse(
            academicYearId, schoolId, gradeId, asOfDate,
            new AgeRangeCount(3, 7, ages.Count(age => age is >= 3 and <= 7)),
            new AgeRangeCount(8, 12, ages.Count(age => age is >= 8 and <= 12)),
            new AgeRangeCount(13, null, ages.Count(age => age >= 13)));
        return AgeDistributionQueryResult.Success(response);
    }

    public async Task<TeacherCountsBySectorResponse> GetDistinctTeacherCountsBySectorAsync(
        DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var teacherSectorPairs = await (
            from contract in context.TeacherContracts.AsNoTracking()
            where contract.Status == TeacherContractStatus.Confirmed
                && contract.StartDate <= periodEnd
                && (contract.EndDate == null || contract.EndDate >= periodStart)
            join school in context.Schools.AsNoTracking() on contract.SchoolId equals school.Id
            select new { contract.TeacherPersonId, school.Sector })
            .Distinct()
            .ToListAsync(cancellationToken);

        var publicCount = teacherSectorPairs.Count(pair => pair.Sector == SchoolSector.Public);
        var privateCount = teacherSectorPairs.Count(pair => pair.Sector == SchoolSector.Private);
        return new TeacherCountsBySectorResponse(periodStart, periodEnd, publicCount, privateCount);
    }

    public async Task<IReadOnlyList<TopSchoolResponse>> GetTopSchoolsByEnrollmentAsync(
        int academicYearId, CancellationToken cancellationToken)
    {
        var counts = await (
            from enrollment in context.Enrollments.AsNoTracking()
            where enrollment.AcademicYearId == academicYearId
            join classGroup in context.ClassGroups.AsNoTracking() on enrollment.ClassGroupId equals classGroup.Id
            group enrollment by classGroup.SchoolId into schoolGroup
            select new { SchoolId = schoolGroup.Key, Count = schoolGroup.Count() })
            .ToListAsync(cancellationToken);

        if (counts.Count == 0)
        {
            return [];
        }

        var max = counts.Max(count => count.Count);
        var countsBySchoolId = counts.Where(count => count.Count == max)
            .ToDictionary(count => count.SchoolId, count => count.Count);
        var topSchoolIds = countsBySchoolId.Keys.ToArray();

        var schools = await context.Schools.AsNoTracking()
            .Where(school => topSchoolIds.Contains(school.Id))
            .OrderBy(school => school.Name).ThenBy(school => school.Id)
            .ToListAsync(cancellationToken);

        return schools
            .Select(school => new TopSchoolResponse(
                new SchoolSummary(school.Id, school.Name, school.Sector.ToString()),
                academicYearId, countsBySchoolId[school.Id]))
            .ToList();
    }
}

public readonly record struct AgeDistributionQueryResult(AgeDistributionResponse? Response, bool AsOfDateInvalid)
{
    public static AgeDistributionQueryResult Success(AgeDistributionResponse response) => new(response, false);

    public static readonly AgeDistributionQueryResult Invalid = new(null, true);
}
