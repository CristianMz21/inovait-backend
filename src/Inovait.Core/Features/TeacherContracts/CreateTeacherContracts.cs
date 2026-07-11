namespace Inovait.Core.Features.TeacherContracts;

public sealed record CreateTeacherContractsCommand(
    int TeacherPersonId, IReadOnlyList<int> SchoolIds, DateOnly StartDate, DateOnly? EndDate);

public enum TeacherContractError
{
    InvalidDateRange,
    NoSchoolsSelected,
    DuplicateSchool,
    TeacherNotFound,
    SchoolNotFound,
    OverlapConflict,
    ConcurrencyConflict,
}

public sealed record TeacherContractResult
{
    private TeacherContractResult(TeacherContractError? error, IReadOnlyList<int> contractIds) =>
        (Error, ContractIds) = (error, contractIds);
    public TeacherContractError? Error { get; }
    public IReadOnlyList<int> ContractIds { get; }
    public bool IsSuccess => Error is null;
    public static TeacherContractResult Created(IReadOnlyList<int> contractIds) => new(null, contractIds);
    public static TeacherContractResult Failure(TeacherContractError error) => new(error, []);
}

public interface ITeacherContractRepository
{
    ValueTask<bool> TeacherExistsAsync(int teacherPersonId, CancellationToken cancellationToken);
    ValueTask<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken);
    ValueTask<bool> OverlapsAsync(int teacherPersonId, int schoolId, DateOnly startDate,
        DateOnly? endDate, CancellationToken cancellationToken);
    ValueTask<int> CreateAsync(int teacherPersonId, int schoolId, DateOnly startDate,
        DateOnly? endDate, CancellationToken cancellationToken);
}

public interface ITeacherContractTransaction
{
    ValueTask<TeacherContractResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TeacherContractResult>> operation, CancellationToken cancellationToken);
}

public sealed class CreateTeacherContractsHandler(
    ITeacherContractRepository repository, ITeacherContractTransaction transaction)
{
    public ValueTask<TeacherContractResult> HandleAsync(
        CreateTeacherContractsCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EndDate < command.StartDate)
            return ValueTask.FromResult(TeacherContractResult.Failure(TeacherContractError.InvalidDateRange));
        if (command.SchoolIds.Count == 0)
            return ValueTask.FromResult(TeacherContractResult.Failure(TeacherContractError.NoSchoolsSelected));
        if (command.SchoolIds.Distinct().Count() != command.SchoolIds.Count)
            return ValueTask.FromResult(TeacherContractResult.Failure(TeacherContractError.DuplicateSchool));
        return transaction.ExecuteAsync(token => ExecuteAsync(command, token), cancellationToken);
    }

    private async ValueTask<TeacherContractResult> ExecuteAsync(
        CreateTeacherContractsCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.TeacherExistsAsync(command.TeacherPersonId, cancellationToken))
            return TeacherContractResult.Failure(TeacherContractError.TeacherNotFound);
        foreach (var schoolId in command.SchoolIds)
        {
            if (!await repository.SchoolExistsAsync(schoolId, cancellationToken))
                return TeacherContractResult.Failure(TeacherContractError.SchoolNotFound);
            if (await repository.OverlapsAsync(command.TeacherPersonId, schoolId,
                command.StartDate, command.EndDate, cancellationToken))
                return TeacherContractResult.Failure(TeacherContractError.OverlapConflict);
        }
        var ids = new List<int>(command.SchoolIds.Count);
        foreach (var schoolId in command.SchoolIds)
            ids.Add(await repository.CreateAsync(command.TeacherPersonId, schoolId,
                command.StartDate, command.EndDate, cancellationToken));
        return TeacherContractResult.Created(ids);
    }
}
