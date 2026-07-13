using Inovait.Core.Features.TeacherContracts;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
public sealed class CreateTeacherContractsHandlerTests
{
    [Fact]
    public async Task HandleAsync_CreatesOneContractPerSchool()
    {
        var workflow = new StubWorkflow();

        var result = await new CreateTeacherContractsHandler(workflow, workflow)
            .HandleAsync(Command([1, 2]), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([101, 102], result.ContractIds);
        Assert.Equal([1, 2], workflow.CreatedSchools);
    }

    public static TheoryData<TeacherContractError> Errors => [.. Enum.GetValues<TeacherContractError>()];

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task HandleAsync_ReturnsEveryCanonicalFailureWithoutWriting(TeacherContractError expected)
    {
        var workflow = new StubWorkflow();
        var command = Command([1]);
        switch (expected)
        {
            case TeacherContractError.InvalidDateRange:
                command = command with { EndDate = new(2026, 2, 28) }; break;
            case TeacherContractError.NoSchoolsSelected:
                command = command with { SchoolIds = [] }; break;
            case TeacherContractError.DuplicateSchool:
                command = command with { SchoolIds = [1, 1] }; break;
            case TeacherContractError.TeacherNotFound:
                workflow.TeacherExists = false; break;
            case TeacherContractError.SchoolNotFound:
                command = command with { SchoolIds = [2, 1] };
                workflow.MissingSchoolId = 1; break;
            case TeacherContractError.OverlapConflict:
                command = command with { SchoolIds = [2, 1] };
                workflow.OverlapSchoolId = 1; break;
            case TeacherContractError.ConcurrencyConflict:
                workflow.TransactionError = expected; break;
        }

        var result = await new CreateTeacherContractsHandler(workflow, workflow)
            .HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal((expected, false), (result.Error, result.IsSuccess));
        Assert.Empty(result.ContractIds);
        Assert.Empty(workflow.CreatedSchools);
    }

    private static CreateTeacherContractsCommand Command(IReadOnlyList<int> schools) =>
        new(7, schools, new(2026, 3, 1), new(2026, 11, 30));

    private sealed class StubWorkflow : ITeacherContractRepository, ITeacherContractTransaction
    {
        public bool TeacherExists { get; set; } = true;
        public int? MissingSchoolId { get; set; }
        public int? OverlapSchoolId { get; set; }
        public TeacherContractError? TransactionError { get; set; }
        public List<int> CreatedSchools { get; } = [];
        public ValueTask<TeacherContractResult> ExecuteAsync(
            Func<CancellationToken, ValueTask<TeacherContractResult>> operation, CancellationToken cancellationToken) =>
            TransactionError is { } error ? ValueTask.FromResult(TeacherContractResult.Failure(error)) : operation(cancellationToken);
        public ValueTask<bool> TeacherExistsAsync(int teacherPersonId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(TeacherExists);
        public ValueTask<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(MissingSchoolId != schoolId);
        public ValueTask<bool> OverlapsAsync(int teacherPersonId, int schoolId, DateOnly startDate,
            DateOnly? endDate, CancellationToken cancellationToken) =>
            ValueTask.FromResult(OverlapSchoolId == schoolId);
        public ValueTask<int> CreateAsync(int teacherPersonId, int schoolId, DateOnly startDate,
            DateOnly? endDate, CancellationToken cancellationToken)
        {
            CreatedSchools.Add(schoolId);
            return ValueTask.FromResult(100 + schoolId);
        }
    }
}
