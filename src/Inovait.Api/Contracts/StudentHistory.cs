namespace Inovait.Api.Contracts;

public sealed record HistoryTeachingAssignment(
    int AssignmentId, TeacherSummary Teacher, SubjectSummary Subject, IReadOnlyList<int> Weekdays);

public sealed record EnrollmentHistoryItem(
    int EnrollmentId, AcademicYearSummary AcademicYear, SchoolSummary School, GradeSummary Grade,
    ClassGroupSummary ClassGroup, IReadOnlyList<HistoryTeachingAssignment> TeachingAssignments);

public sealed record StudentHistoryResponse(
    int StudentId, string DocumentType, string DocumentNumber, string FirstNames, string LastNames,
    DateOnly BirthDate, IReadOnlyList<EnrollmentHistoryItem> Enrollments);
