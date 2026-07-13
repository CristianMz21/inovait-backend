namespace Inovait.Core.Features.TeachingAssignments;

public sealed record CreateTeachingAssignmentCommand(
    int TeacherContractId, int SubjectId, int ClassGroupId,
    DateOnly StartDate, DateOnly? EndDate, IReadOnlyList<byte> Weekdays);

public enum TeachingAssignmentError
{
    InvalidDateRange,
    NoWeekdaysSelected,
    InvalidWeekday,
    DuplicateWeekday,
    TeacherContractNotFound,
    ClassGroupNotFound,
    SubjectNotFound,
    SchoolMismatch,
    PeriodNotContained,
    ConcurrencyConflict,
}

public sealed record TeachingAssignmentResult
{
    private TeachingAssignmentResult(TeachingAssignmentError? error, int assignmentId) =>
        (Error, AssignmentId) = (error, assignmentId);
    public TeachingAssignmentError? Error { get; }
    public int AssignmentId { get; }
    public bool IsSuccess => Error is null;
    public static TeachingAssignmentResult Created(int assignmentId) => new(null, assignmentId);
    public static TeachingAssignmentResult Failure(TeachingAssignmentError error) => new(error, 0);
}

// Snapshot of the school/period bounds a TeacherContract imposes on its assignments.
public sealed record TeacherContractSnapshot(
    int SchoolId, DateOnly StartDate, DateOnly? EndDate, DateOnly? CancellationEffectiveDate);

// Snapshot of the school and academic-year bounds a ClassGroup imposes on its assignments.
public sealed record ClassGroupSnapshot(int SchoolId, DateOnly AcademicYearStartDate, DateOnly AcademicYearEndDate);

// REQ-040/REQ-041/REQ-060: contract and group must share a school, and the assignment period must be
// contained within the contract's period (open contract has no upper bound, a cancelled contract is
// bounded by CancellationEffectiveDate) and the group's academic year.
public static class TeachingAssignmentPeriodPolicy
{
    public static bool SchoolsMatch(TeacherContractSnapshot contract, ClassGroupSnapshot group) =>
        contract.SchoolId == group.SchoolId;

    public static bool IsPeriodContained(
        DateOnly startDate, DateOnly? endDate, TeacherContractSnapshot contract, ClassGroupSnapshot group)
    {
        var lowerBound = contract.StartDate > group.AcademicYearStartDate
            ? contract.StartDate
            : group.AcademicYearStartDate;
        var upperBound = Min(contract.EndDate ?? DateOnly.MaxValue,
            contract.CancellationEffectiveDate ?? DateOnly.MaxValue, group.AcademicYearEndDate);
        var effectiveEnd = endDate ?? group.AcademicYearEndDate;
        return startDate >= lowerBound && effectiveEnd <= upperBound;
    }

    private static DateOnly Min(DateOnly first, DateOnly second, DateOnly third)
    {
        var firstPairMinimum = first < second ? first : second;
        return firstPairMinimum < third ? firstPairMinimum : third;
    }
}

public interface ITeachingAssignmentRepository
{
    ValueTask<TeacherContractSnapshot?> FindTeacherContractAsync(int teacherContractId, CancellationToken cancellationToken);
    ValueTask<ClassGroupSnapshot?> FindClassGroupAsync(int classGroupId, CancellationToken cancellationToken);
    ValueTask<bool> SubjectExistsAsync(int subjectId, CancellationToken cancellationToken);
    ValueTask<int> CreateAsync(int teacherContractId, int classGroupId, int subjectId,
        DateOnly startDate, DateOnly? endDate, IReadOnlyList<byte> weekdays, CancellationToken cancellationToken);
}

public interface ITeachingAssignmentTransaction
{
    ValueTask<TeachingAssignmentResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TeachingAssignmentResult>> operation, CancellationToken cancellationToken);
}

public sealed class CreateTeachingAssignmentHandler(
    ITeachingAssignmentRepository repository, ITeachingAssignmentTransaction transaction)
{
    public ValueTask<TeachingAssignmentResult> HandleAsync(
        CreateTeachingAssignmentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EndDate < command.StartDate)
            return ValueTask.FromResult(TeachingAssignmentResult.Failure(TeachingAssignmentError.InvalidDateRange));
        if (command.Weekdays.Count == 0)
            return ValueTask.FromResult(TeachingAssignmentResult.Failure(TeachingAssignmentError.NoWeekdaysSelected));
        if (command.Weekdays.Any(weekday => weekday is < 1 or > 7))
            return ValueTask.FromResult(TeachingAssignmentResult.Failure(TeachingAssignmentError.InvalidWeekday));
        if (command.Weekdays.Distinct().Count() != command.Weekdays.Count)
            return ValueTask.FromResult(TeachingAssignmentResult.Failure(TeachingAssignmentError.DuplicateWeekday));
        return transaction.ExecuteAsync(token => ExecuteAsync(command, token), cancellationToken);
    }

    private async ValueTask<TeachingAssignmentResult> ExecuteAsync(
        CreateTeachingAssignmentCommand command, CancellationToken cancellationToken)
    {
        var contract = await repository.FindTeacherContractAsync(command.TeacherContractId, cancellationToken);
        if (contract is null)
            return TeachingAssignmentResult.Failure(TeachingAssignmentError.TeacherContractNotFound);
        var group = await repository.FindClassGroupAsync(command.ClassGroupId, cancellationToken);
        if (group is null)
            return TeachingAssignmentResult.Failure(TeachingAssignmentError.ClassGroupNotFound);
        if (!await repository.SubjectExistsAsync(command.SubjectId, cancellationToken))
            return TeachingAssignmentResult.Failure(TeachingAssignmentError.SubjectNotFound);
        if (!TeachingAssignmentPeriodPolicy.SchoolsMatch(contract, group))
            return TeachingAssignmentResult.Failure(TeachingAssignmentError.SchoolMismatch);
        if (!TeachingAssignmentPeriodPolicy.IsPeriodContained(command.StartDate, command.EndDate, contract, group))
            return TeachingAssignmentResult.Failure(TeachingAssignmentError.PeriodNotContained);

        var id = await repository.CreateAsync(command.TeacherContractId, command.ClassGroupId, command.SubjectId,
            command.StartDate, command.EndDate, command.Weekdays, cancellationToken);
        return TeachingAssignmentResult.Created(id);
    }
}
