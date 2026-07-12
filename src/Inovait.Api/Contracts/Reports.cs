namespace Inovait.Api.Contracts;

public sealed record AgeRangeCount(int MinimumAge, int? MaximumAge, int Count);

public sealed record AgeDistributionResponse(
    int AcademicYearId, int? SchoolId, int? GradeId, DateOnly AsOfDate,
    AgeRangeCount Age3To7, AgeRangeCount Age8To12, AgeRangeCount AgeOver12);

public sealed record TeacherCountsBySectorResponse(
    DateOnly PeriodStart, DateOnly PeriodEnd, int PublicDistinctTeacherCount, int PrivateDistinctTeacherCount);

public sealed record TopSchoolResponse(SchoolSummary School, int AcademicYearId, int EnrollmentCount);
