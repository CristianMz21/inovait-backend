namespace Inovait.Api.Contracts;

public sealed record SchoolSummary(int Id, string Name, string Sector);

public sealed record GradeSummary(int Id, string Name, int SortOrder);

public sealed record AcademicYearSummary(int Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsCurrent);

public sealed record ClassGroupSummary(int Id, string Code, int SchoolId, int AcademicYearId, int GradeId);

public sealed record TeacherSummary(int Id, string DocumentType, string DocumentNumber, string FirstNames, string LastNames);

public sealed record SubjectSummary(int Id, string Code, string Name);

public sealed record SchoolTeacherSummary(
    TeacherSummary Teacher, int ContractId, string PersistedStatus, string EffectiveStatus,
    DateOnly EvaluatedAt, DateOnly StartDate, DateOnly? EndDate);
