namespace Inovait.Core.Features.Enrollments;

public sealed record CreateEnrollmentCommand(IdentityRequest Student, int SchoolId, int AcademicYearId, int GradeId, int ClassGroupId);
public enum EnrollmentError
{
    InvalidBirthDate, DocumentTypeNotFound, IdentityConflict,
    SchoolNotFound, AcademicYearNotFound, GradeNotFound, ClassGroupNotFound,
    AcademicContextMismatch, AnnualEnrollmentConflict, ConcurrencyConflict,
}
public sealed record EnrollmentResult
{
    private EnrollmentResult(EnrollmentError? error, int? enrollmentId, int? studentId, bool studentReused) => (Error, EnrollmentId, StudentId, StudentReused) = (error, enrollmentId, studentId, studentReused);
    public EnrollmentError? Error { get; }
    public int? EnrollmentId { get; }
    public int? StudentId { get; }
    public bool StudentReused { get; }
    public bool IsSuccess => Error is null;
    public static EnrollmentResult Created(int enrollmentId, int studentId, bool studentReused) => new(null, enrollmentId, studentId, studentReused);
    public static EnrollmentResult Failure(EnrollmentError error) => new(error, null, null, false);
}
public interface IEnrollmentRepository : IIdentityReader
{
    ValueTask<EnrollmentError?> ValidateContextAsync(CreateEnrollmentCommand command, CancellationToken cancellationToken);
    ValueTask<bool> EnrollmentExistsAsync(int studentId, int academicYearId, CancellationToken cancellationToken);
    ValueTask<int> CreatePersonAsync(IdentityResolution identity, CancellationToken cancellationToken);
    ValueTask<int> CreateEnrollmentAsync(
        int studentId, int classGroupId, int academicYearId, bool createStudentRole,
        CancellationToken cancellationToken);
}
public interface IEnrollmentTransaction
{
    ValueTask<EnrollmentResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<EnrollmentResult>> operation, CancellationToken cancellationToken);
}
public sealed class CreateEnrollmentHandler(
    IdentityResolver identityResolver, IEnrollmentRepository repository, IEnrollmentTransaction transaction)
{
    public ValueTask<EnrollmentResult> HandleAsync(
        CreateEnrollmentCommand command, CancellationToken cancellationToken = default) =>
        transaction.ExecuteAsync(token => ExecuteAsync(command, token), cancellationToken);
    private async ValueTask<EnrollmentResult> ExecuteAsync(
        CreateEnrollmentCommand command, CancellationToken cancellationToken)
    {
        var contextError = await repository.ValidateContextAsync(command, cancellationToken);
        if (contextError is not null)
            return EnrollmentResult.Failure(contextError.Value);
        IdentityResolution identity;
        try
        {
            identity = await identityResolver.ResolveAsync(command.Student, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            return EnrollmentResult.Failure(EnrollmentError.InvalidBirthDate);
        }
        catch (KeyNotFoundException)
        {
            return EnrollmentResult.Failure(EnrollmentError.DocumentTypeNotFound);
        }
        if (identity.Status == IdentityResolutionStatus.Conflict)
            return EnrollmentResult.Failure(EnrollmentError.IdentityConflict);
        if (identity.PersonId is int existingId &&
            await repository.EnrollmentExistsAsync(existingId, command.AcademicYearId, cancellationToken))
            return EnrollmentResult.Failure(EnrollmentError.AnnualEnrollmentConflict);
        var studentId = identity.PersonId
            ?? await repository.CreatePersonAsync(identity, cancellationToken);
        var enrollmentId = await repository.CreateEnrollmentAsync(studentId, command.ClassGroupId,
            command.AcademicYearId, identity.CreateStudentRole, cancellationToken);
        return EnrollmentResult.Created(enrollmentId, studentId,
            identity.Status == IdentityResolutionStatus.ReusePerson);
    }
}
