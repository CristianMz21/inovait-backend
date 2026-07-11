using System.Linq.Expressions;
using Inovait.Api.Contracts;
using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

public sealed class EnrollmentReadService(InovaitDbContext context, TimeProvider timeProvider)
{
    public async Task<CreateEnrollmentResponse> GetCreatedAsync(
        int enrollmentId, bool studentReused, CancellationToken cancellationToken)
    {
        var today = Today();
        var currentYearId = await CurrentAcademicYearIdAsync(cancellationToken);
        var row = await Query(enrollment => enrollment.Id == enrollmentId).SingleAsync(cancellationToken);
        return new CreateEnrollmentResponse(
            row.EnrollmentId, row.StudentId, studentReused, row.DocumentType, row.DocumentNumber,
            row.FirstNames, row.LastNames, row.BirthDate, AgeCalculator.Calculate(row.BirthDate, today),
            new SchoolSummary(row.SchoolId, row.SchoolName, row.Sector.ToString()),
            new AcademicYearSummary(row.AcademicYearId, row.AcademicYearName, row.AcademicYearStartDate,
                row.AcademicYearEndDate, row.AcademicYearId == currentYearId),
            new GradeSummary(row.GradeId, row.GradeName, row.GradeSortOrder),
            new ClassGroupSummary(row.ClassGroupId, row.ClassGroupCode, row.SchoolId, row.AcademicYearId, row.GradeId));
    }

    public async Task<IReadOnlyList<EnrollmentListItem>> ListAsync(
        int schoolId, int gradeId, int academicYearId, DateOnly? asOfDate, CancellationToken cancellationToken)
    {
        var today = asOfDate ?? Today();
        var currentYearId = await CurrentAcademicYearIdAsync(cancellationToken);
        var classGroupIds = context.ClassGroups.AsNoTracking()
            .Where(group => group.SchoolId == schoolId && group.GradeId == gradeId && group.AcademicYearId == academicYearId)
            .Select(group => group.Id);
        var rows = await Query(enrollment =>
                enrollment.AcademicYearId == academicYearId && classGroupIds.Contains(enrollment.ClassGroupId))
            .ToListAsync(cancellationToken);
        return rows.Select(row => new EnrollmentListItem(
            row.EnrollmentId, row.StudentId, row.DocumentType, row.DocumentNumber, row.FirstNames, row.LastNames,
            row.BirthDate, AgeCalculator.Calculate(row.BirthDate, today),
            new SchoolSummary(row.SchoolId, row.SchoolName, row.Sector.ToString()),
            new AcademicYearSummary(row.AcademicYearId, row.AcademicYearName, row.AcademicYearStartDate,
                row.AcademicYearEndDate, row.AcademicYearId == currentYearId),
            new GradeSummary(row.GradeId, row.GradeName, row.GradeSortOrder),
            new ClassGroupSummary(row.ClassGroupId, row.ClassGroupCode, row.SchoolId, row.AcademicYearId, row.GradeId)))
            .ToList();
    }

    private IQueryable<EnrollmentRow> Query(Expression<Func<Enrollment, bool>> predicate) =>
        (from enrollment in context.Enrollments.AsNoTracking().Where(predicate)
         join person in context.People.AsNoTracking() on enrollment.StudentPersonId equals person.Id
         join documentType in context.DocumentTypes.AsNoTracking() on person.DocumentTypeId equals documentType.Id
         join classGroup in context.ClassGroups.AsNoTracking() on enrollment.ClassGroupId equals classGroup.Id
         join school in context.Schools.AsNoTracking() on classGroup.SchoolId equals school.Id
         join grade in context.Grades.AsNoTracking() on classGroup.GradeId equals grade.Id
         join academicYear in context.AcademicYears.AsNoTracking() on enrollment.AcademicYearId equals academicYear.Id
         orderby person.LastNames, person.FirstNames, documentType.Code, person.DocumentNumber, enrollment.Id
         select new EnrollmentRow(
             enrollment.Id, person.Id, documentType.Code, person.DocumentNumber, person.FirstNames, person.LastNames,
             person.BirthDate, school.Id, school.Name, school.Sector, academicYear.Id, academicYear.Name,
             academicYear.StartDate, academicYear.EndDate, grade.Id, grade.Name, grade.SortOrder,
             classGroup.Id, classGroup.Code));

    private Task<int> CurrentAcademicYearIdAsync(CancellationToken cancellationToken) =>
        context.AcademicConfigurations.AsNoTracking()
            .Where(configuration => configuration.Id == 1)
            .Select(configuration => configuration.CurrentAcademicYearId)
            .SingleAsync(cancellationToken);

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private sealed record EnrollmentRow(
        int EnrollmentId, int StudentId, string DocumentType, string DocumentNumber, string FirstNames,
        string LastNames, DateOnly BirthDate, int SchoolId, string SchoolName, SchoolSector Sector,
        int AcademicYearId, string AcademicYearName, DateOnly AcademicYearStartDate, DateOnly AcademicYearEndDate,
        int GradeId, string GradeName, short GradeSortOrder, int ClassGroupId, string ClassGroupCode);
}
