namespace Inovait.Api.Contracts;

public sealed record CreateTeacherContractsRequest(IReadOnlyList<int> SchoolIds, DateOnly? StartDate, DateOnly? EndDate);

public sealed record TeacherContractResponse(
    int Id, int TeacherId, SchoolSummary School, DateOnly StartDate, DateOnly? EndDate,
    string PersistedStatus, string EffectiveStatus, DateOnly EvaluatedAt);
