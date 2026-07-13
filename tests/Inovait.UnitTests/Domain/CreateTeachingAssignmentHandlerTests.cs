using Inovait.Core.Features.TeachingAssignments;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P1")]
public sealed class CreateTeachingAssignmentHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesAssignmentWithScheduleWhenValid()
    {
        var workflow = new StubWorkflow();

        var result = await new CreateTeachingAssignmentHandler(workflow, workflow)
            .HandleAsync(Command([1, 3]), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(501, result.AssignmentId);
        Assert.Equal([1, 3], workflow.CreatedWeekdays);
    }

    public static TheoryData<TeachingAssignmentError> Errors => [.. Enum.GetValues<TeachingAssignmentError>()];

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task HandleAsync_ReturnsEveryCanonicalFailureWithoutWriting(TeachingAssignmentError expected)
    {
        var workflow = new StubWorkflow();
        var command = Command([1]);
        switch (expected)
        {
            case TeachingAssignmentError.InvalidDateRange:
                command = command with { EndDate = new(2026, 2, 28) }; break;
            case TeachingAssignmentError.NoWeekdaysSelected:
                command = command with { Weekdays = [] }; break;
            case TeachingAssignmentError.InvalidWeekday:
                command = command with { Weekdays = [8] }; break;
            case TeachingAssignmentError.DuplicateWeekday:
                command = command with { Weekdays = [1, 1] }; break;
            case TeachingAssignmentError.TeacherContractNotFound:
                workflow.Contract = null; break;
            case TeachingAssignmentError.ClassGroupNotFound:
                workflow.Group = null; break;
            case TeachingAssignmentError.SubjectNotFound:
                workflow.SubjectExists = false; break;
            case TeachingAssignmentError.SchoolMismatch:
                workflow.Group = new ClassGroupSnapshot(2, new(2026, 1, 1), new(2026, 12, 31)); break;
            case TeachingAssignmentError.PeriodNotContained:
                command = command with { StartDate = new(2025, 1, 1), EndDate = new(2025, 2, 1) }; break;
            case TeachingAssignmentError.ConcurrencyConflict:
                workflow.TransactionError = expected; break;
        }

        var result = await new CreateTeachingAssignmentHandler(workflow, workflow)
            .HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal((expected, false), (result.Error, result.IsSuccess));
        Assert.Empty(workflow.CreatedWeekdays);
    }

    private static CreateTeachingAssignmentCommand Command(IReadOnlyList<byte> weekdays) =>
        new(7, 9, 11, new(2026, 3, 1), new(2026, 11, 30), weekdays);

    private sealed class StubWorkflow : ITeachingAssignmentRepository, ITeachingAssignmentTransaction
    {
        public TeacherContractSnapshot? Contract { get; set; } = new(1, new(2026, 1, 1), new(2026, 12, 31), null);
        public ClassGroupSnapshot? Group { get; set; } = new(1, new(2026, 1, 1), new(2026, 12, 31));
        public bool SubjectExists { get; set; } = true;
        public TeachingAssignmentError? TransactionError { get; set; }
        public List<byte> CreatedWeekdays { get; } = [];

        public ValueTask<TeachingAssignmentResult> ExecuteAsync(
            Func<CancellationToken, ValueTask<TeachingAssignmentResult>> operation, CancellationToken cancellationToken) =>
            TransactionError is { } error
                ? ValueTask.FromResult(TeachingAssignmentResult.Failure(error))
                : operation(cancellationToken);
        public ValueTask<TeacherContractSnapshot?> FindTeacherContractAsync(int teacherContractId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Contract);
        public ValueTask<ClassGroupSnapshot?> FindClassGroupAsync(int classGroupId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Group);
        public ValueTask<bool> SubjectExistsAsync(int subjectId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(SubjectExists);
        public ValueTask<int> CreateAsync(int teacherContractId, int classGroupId, int subjectId,
            DateOnly startDate, DateOnly? endDate, IReadOnlyList<byte> weekdays, CancellationToken cancellationToken)
        {
            CreatedWeekdays.AddRange(weekdays);
            return ValueTask.FromResult(501);
        }
    }
}
