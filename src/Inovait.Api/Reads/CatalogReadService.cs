using Inovait.Api.Contracts;
using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

public sealed class CatalogReadService(InovaitDbContext context)
{
    public async Task<IReadOnlyList<SchoolSummary>> ListSchoolsAsync(CancellationToken cancellationToken)
    {
        var rows = await context.Schools.AsNoTracking()
            .OrderBy(school => school.Name).ThenBy(school => school.Id)
            .Select(school => new { school.Id, school.Name, school.Sector })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new SchoolSummary(row.Id, row.Name, row.Sector.ToString())).ToList();
    }

    public Task<List<GradeSummary>> ListGradesAsync(CancellationToken cancellationToken) =>
        context.Grades.AsNoTracking()
            .OrderBy(grade => grade.SortOrder).ThenBy(grade => grade.Id)
            .Select(grade => new GradeSummary(grade.Id, grade.Name, grade.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AcademicYearSummary>> ListAcademicYearsAsync(CancellationToken cancellationToken)
    {
        var currentYearId = await CurrentAcademicYearIdAsync(cancellationToken);
        var rows = await context.AcademicYears.AsNoTracking()
            .OrderByDescending(year => year.StartDate).ThenBy(year => year.Id)
            .Select(year => new { year.Id, year.Name, year.StartDate, year.EndDate })
            .ToListAsync(cancellationToken);
        return rows.Select(row => new AcademicYearSummary(row.Id, row.Name, row.StartDate, row.EndDate, row.Id == currentYearId))
            .ToList();
    }

    public Task<List<ClassGroupSummary>> ListClassGroupsAsync(
        int? schoolId, int? gradeId, int? academicYearId, CancellationToken cancellationToken)
    {
        var query = context.ClassGroups.AsNoTracking().AsQueryable();
        if (schoolId is int school)
        {
            query = query.Where(group => group.SchoolId == school);
        }

        if (gradeId is int grade)
        {
            query = query.Where(group => group.GradeId == grade);
        }

        if (academicYearId is int academicYear)
        {
            query = query.Where(group => group.AcademicYearId == academicYear);
        }

        return query.OrderBy(group => group.Code).ThenBy(group => group.Id)
            .Select(group => new ClassGroupSummary(group.Id, group.Code, group.SchoolId, group.AcademicYearId, group.GradeId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<TeacherSummary>> ListTeachersAsync(CancellationToken cancellationToken) =>
        (from teacher in context.Teachers.AsNoTracking()
         join person in context.People.AsNoTracking() on teacher.PersonId equals person.Id
         join documentType in context.DocumentTypes.AsNoTracking() on person.DocumentTypeId equals documentType.Id
         orderby person.LastNames, person.FirstNames, documentType.Code, person.DocumentNumber, person.Id
         select new TeacherSummary(person.Id, documentType.Code, person.DocumentNumber, person.FirstNames, person.LastNames))
        .ToListAsync(cancellationToken);

    public Task<List<SubjectSummary>> ListSubjectsAsync(CancellationToken cancellationToken) =>
        context.Subjects.AsNoTracking()
            .OrderBy(subject => subject.Name).ThenBy(subject => subject.Code).ThenBy(subject => subject.Id)
            .Select(subject => new SubjectSummary(subject.Id, subject.Code, subject.Name))
            .ToListAsync(cancellationToken);

    public Task<int> CurrentAcademicYearIdAsync(CancellationToken cancellationToken) =>
        context.AcademicConfigurations.AsNoTracking()
            .Where(configuration => configuration.Id == 1)
            .Select(configuration => configuration.CurrentAcademicYearId)
            .SingleAsync(cancellationToken);
}
