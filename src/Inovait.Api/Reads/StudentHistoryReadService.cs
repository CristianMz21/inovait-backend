using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.Common;
using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

// REQ-038/REQ-039: locate the student by canonical document identity and return every enrollment with
// the teachers/subjects that served its ClassGroup. REQ-040/REQ-041 (period compatibility) are enforced
// transactionally at TeachingAssignment creation (see CreateTeachingAssignmentHandler); the read side only
// needs to join by ClassGroupId, since every persisted assignment is already period-valid for its group.
public sealed class StudentHistoryReadService(InovaitDbContext context, ITextNormalizer normalizer)
{
    public async Task<StudentHistoryResponse?> GetAsync(
        string documentType, string documentNumber, CancellationToken cancellationToken)
    {
        var typeCode = normalizer.NormalizeRequired(documentType);
        var number = normalizer.NormalizeRequired(documentNumber);

        var student = await (
            from person in context.People.AsNoTracking()
            join docType in context.DocumentTypes.AsNoTracking() on person.DocumentTypeId equals docType.Id
            where docType.Code == typeCode && person.DocumentNumber == number
                && context.Students.Any(candidate => candidate.PersonId == person.Id)
            select new StudentRow(person.Id, docType.Code, person.DocumentNumber, person.FirstNames, person.LastNames, person.BirthDate))
            .SingleOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return null;
        }

        var currentYearId = await CurrentAcademicYearIdAsync(cancellationToken);

        var enrollmentRows = await (
            from enrollment in context.Enrollments.AsNoTracking()
            where enrollment.StudentPersonId == student.PersonId
            join classGroup in context.ClassGroups.AsNoTracking() on enrollment.ClassGroupId equals classGroup.Id
            join school in context.Schools.AsNoTracking() on classGroup.SchoolId equals school.Id
            join grade in context.Grades.AsNoTracking() on classGroup.GradeId equals grade.Id
            join academicYear in context.AcademicYears.AsNoTracking() on enrollment.AcademicYearId equals academicYear.Id
            orderby academicYear.StartDate descending, enrollment.Id
            select new EnrollmentRow(
                enrollment.Id, classGroup.Id, classGroup.Code,
                school.Id, school.Name, school.Sector,
                grade.Id, grade.Name, grade.SortOrder,
                academicYear.Id, academicYear.Name, academicYear.StartDate, academicYear.EndDate))
            .ToListAsync(cancellationToken);

        var classGroupIds = enrollmentRows.Select(row => row.ClassGroupId).Distinct().ToArray();

        var assignmentRows = await (
            from assignment in context.TeachingAssignments.AsNoTracking()
            where classGroupIds.Contains(assignment.ClassGroupId)
            join contract in context.TeacherContracts.AsNoTracking() on assignment.TeacherContractId equals contract.Id
            join teacherPerson in context.People.AsNoTracking() on contract.TeacherPersonId equals teacherPerson.Id
            join teacherDocType in context.DocumentTypes.AsNoTracking() on teacherPerson.DocumentTypeId equals teacherDocType.Id
            join subject in context.Subjects.AsNoTracking() on assignment.SubjectId equals subject.Id
            orderby subject.Name, teacherPerson.LastNames, teacherPerson.FirstNames, assignment.Id
            select new AssignmentRow(
                assignment.Id, assignment.ClassGroupId,
                teacherPerson.Id, teacherDocType.Code, teacherPerson.DocumentNumber,
                teacherPerson.FirstNames, teacherPerson.LastNames,
                subject.Id, subject.Code, subject.Name))
            .ToListAsync(cancellationToken);

        var assignmentIds = assignmentRows.Select(row => row.AssignmentId).ToArray();
        var scheduleRows = await context.ClassSchedules.AsNoTracking()
            .Where(schedule => assignmentIds.Contains(schedule.TeachingAssignmentId))
            .OrderBy(schedule => schedule.Weekday)
            .Select(schedule => new { schedule.TeachingAssignmentId, schedule.Weekday })
            .ToListAsync(cancellationToken);

        var weekdaysByAssignment = new Dictionary<int, List<int>>();
        foreach (var schedule in scheduleRows)
        {
            if (!weekdaysByAssignment.TryGetValue(schedule.TeachingAssignmentId, out var weekdays))
            {
                weekdaysByAssignment[schedule.TeachingAssignmentId] = weekdays = [];
            }

            weekdays.Add(schedule.Weekday);
        }

        var assignmentsByGroup = new Dictionary<int, List<HistoryTeachingAssignment>>();
        foreach (var row in assignmentRows)
        {
            if (!assignmentsByGroup.TryGetValue(row.ClassGroupId, out var list))
            {
                assignmentsByGroup[row.ClassGroupId] = list = [];
            }

            list.Add(new HistoryTeachingAssignment(
                row.AssignmentId,
                new TeacherSummary(row.TeacherId, row.TeacherDocumentType, row.TeacherDocumentNumber,
                    row.TeacherFirstNames, row.TeacherLastNames),
                new SubjectSummary(row.SubjectId, row.SubjectCode, row.SubjectName),
                weekdaysByAssignment.TryGetValue(row.AssignmentId, out var weekdays) ? weekdays : []));
        }

        var enrollments = enrollmentRows.Select(row => new EnrollmentHistoryItem(
            row.EnrollmentId,
            new AcademicYearSummary(row.AcademicYearId, row.AcademicYearName, row.AcademicYearStartDate,
                row.AcademicYearEndDate, row.AcademicYearId == currentYearId),
            new SchoolSummary(row.SchoolId, row.SchoolName, row.Sector.ToString()),
            new GradeSummary(row.GradeId, row.GradeName, row.GradeSortOrder),
            new ClassGroupSummary(row.ClassGroupId, row.ClassGroupCode, row.SchoolId, row.AcademicYearId, row.GradeId),
            assignmentsByGroup.TryGetValue(row.ClassGroupId, out var assignments)
                ? assignments
                : []))
            .ToList();

        return new StudentHistoryResponse(
            student.PersonId, student.DocumentType, student.DocumentNumber, student.FirstNames, student.LastNames,
            student.BirthDate, enrollments);
    }

    private Task<int> CurrentAcademicYearIdAsync(CancellationToken cancellationToken) =>
        context.AcademicConfigurations.AsNoTracking()
            .Where(configuration => configuration.Id == 1)
            .Select(configuration => configuration.CurrentAcademicYearId)
            .SingleAsync(cancellationToken);

    private sealed record StudentRow(
        int PersonId, string DocumentType, string DocumentNumber, string FirstNames, string LastNames, DateOnly BirthDate);

    private sealed record EnrollmentRow(
        int EnrollmentId, int ClassGroupId, string ClassGroupCode,
        int SchoolId, string SchoolName, SchoolSector Sector,
        int GradeId, string GradeName, short GradeSortOrder,
        int AcademicYearId, string AcademicYearName, DateOnly AcademicYearStartDate, DateOnly AcademicYearEndDate);

    private sealed record AssignmentRow(
        int AssignmentId, int ClassGroupId,
        int TeacherId, string TeacherDocumentType, string TeacherDocumentNumber, string TeacherFirstNames, string TeacherLastNames,
        int SubjectId, string SubjectCode, string SubjectName);
}
