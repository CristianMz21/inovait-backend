using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

public sealed class ReferenceLookupService(InovaitDbContext context)
{
    public Task<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken) =>
        context.Schools.AsNoTracking().AnyAsync(school => school.Id == schoolId, cancellationToken);

    public Task<bool> GradeExistsAsync(int gradeId, CancellationToken cancellationToken) =>
        context.Grades.AsNoTracking().AnyAsync(grade => grade.Id == gradeId, cancellationToken);

    public Task<bool> AcademicYearExistsAsync(int academicYearId, CancellationToken cancellationToken) =>
        context.AcademicYears.AsNoTracking().AnyAsync(year => year.Id == academicYearId, cancellationToken);

    public Task<bool> TeacherExistsAsync(int teacherPersonId, CancellationToken cancellationToken) =>
        context.Teachers.AsNoTracking().AnyAsync(teacher => teacher.PersonId == teacherPersonId, cancellationToken);
}
