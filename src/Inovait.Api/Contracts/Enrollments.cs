namespace Inovait.Api.Contracts;

public sealed record StudentIdentityInput(
    string DocumentType, string DocumentNumber, string FirstNames, string LastNames, DateOnly? BirthDate);

public sealed record CreateEnrollmentRequest(
    StudentIdentityInput Student, int SchoolId, int AcademicYearId, int GradeId, int ClassGroupId);

public sealed record CreateEnrollmentResponse(
    int EnrollmentId, int StudentId, bool StudentReused, string DocumentType, string DocumentNumber,
    string FirstNames, string LastNames, DateOnly BirthDate, int Age,
    SchoolSummary School, AcademicYearSummary AcademicYear, GradeSummary Grade, ClassGroupSummary ClassGroup);

public sealed record EnrollmentListItem(
    int EnrollmentId, int StudentId, string DocumentType, string DocumentNumber,
    string FirstNames, string LastNames, DateOnly BirthDate, int Age,
    SchoolSummary School, AcademicYearSummary AcademicYear, GradeSummary Grade, ClassGroupSummary ClassGroup);
