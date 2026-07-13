using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Text;
namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
public sealed class CreateEnrollmentHandlerTests
{
    [Theory]
    [InlineData(null, false, true, false)]
    [InlineData(7, false, true, true)]
    [InlineData(7, true, false, true)]
    public async Task HandleAsync_CreatesOrReusesIdentityAndOnlyAddsAMissingStudentRole(
        int? personId, bool isStudent, bool createsRole, bool reused)
    {
        var workflow = new StubWorkflow(personId is null
            ? null : new(personId.Value, "Ada", "Lovelace", new(2012, 1, 1), isStudent, false));
        var result = await CreateHandler(workflow).HandleAsync(Command(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal((11, 7, reused), (result.EnrollmentId, result.StudentId, result.StudentReused));
        Assert.Equal(createsRole, workflow.CreatedStudentRole);
    }
    public static TheoryData<EnrollmentError> Errors => [.. Enum.GetValues<EnrollmentError>()];
    [Theory]
    [MemberData(nameof(Errors))]
    public async Task HandleAsync_MapsEveryCanonicalFailureWithoutWriting(EnrollmentError expected)
    {
        var workflow = new StubWorkflow(null);
        var command = Command();
        switch (expected)
        {
            case EnrollmentError.InvalidBirthDate:
                command = command with { Student = command.Student with { BirthDate = new(9999, 1, 1) } }; break;
            case EnrollmentError.DocumentTypeNotFound:
                workflow.DocumentTypeId = null; break;
            case EnrollmentError.IdentityConflict:
                workflow.Person = new(7, "Different", "Identity", new(2012, 1, 1), true, false); break;
            case EnrollmentError.AnnualEnrollmentConflict:
                workflow.Person = new(7, "Ada", "Lovelace", new(2012, 1, 1), true, false);
                workflow.AnnualEnrollmentExists = true; break;
            case EnrollmentError.ConcurrencyConflict:
                workflow.TransactionError = expected; break;
            default:
                workflow.ContextError = expected; break;
        }
        var result = await CreateHandler(workflow).HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal((expected, null, null, false, false),
            (result.Error, result.EnrollmentId, result.StudentId, result.StudentReused, result.IsSuccess));
        Assert.Equal(0, workflow.WriteCount);
    }
    private static CreateEnrollmentHandler CreateHandler(StubWorkflow workflow) =>
        new(new(new TextNormalizer(), workflow, TimeProvider.System), workflow, workflow);
    private static CreateEnrollmentCommand Command() =>
        new(new("DNI", "DOC-1", "Ada", "Lovelace", new(2012, 1, 1)), 1, 1, 1, 1);
    private sealed class StubWorkflow(PersonIdentity? person) : IEnrollmentRepository, IEnrollmentTransaction
    {
        public PersonIdentity? Person { get; set; } = person;
        public short? DocumentTypeId { get; set; } = 1;
        public EnrollmentError? ContextError { get; set; }
        public EnrollmentError? TransactionError { get; set; }
        public bool AnnualEnrollmentExists { get; set; }
        public bool CreatedStudentRole { get; private set; }
        public int WriteCount { get; private set; }
        public ValueTask<EnrollmentResult> ExecuteAsync(
            Func<CancellationToken, ValueTask<EnrollmentResult>> operation, CancellationToken cancellationToken) =>
            TransactionError is { } error ? ValueTask.FromResult(EnrollmentResult.Failure(error)) : operation(cancellationToken);
        public ValueTask<EnrollmentError?> ValidateContextAsync(
            CreateEnrollmentCommand command, CancellationToken cancellationToken) => ValueTask.FromResult(ContextError);
        public ValueTask<bool> EnrollmentExistsAsync(int studentId, int academicYearId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AnnualEnrollmentExists);
        public ValueTask<int> CreatePersonAsync(IdentityResolution identity, CancellationToken cancellationToken) =>
            ValueTask.FromResult(++WriteCount + 6);
        public ValueTask<int> CreateEnrollmentAsync(
            int studentId, int classGroupId, int academicYearId, bool createStudentRole, CancellationToken cancellationToken)
        {
            WriteCount++;
            CreatedStudentRole = createStudentRole;
            return ValueTask.FromResult(11);
        }
        public ValueTask<short?> FindDocumentTypeIdAsync(string code, CancellationToken cancellationToken) =>
            ValueTask.FromResult(DocumentTypeId);
        public ValueTask<PersonIdentity?> FindPersonAsync(
            short documentTypeId, string documentNumber, CancellationToken cancellationToken) => ValueTask.FromResult(Person);
    }
}
