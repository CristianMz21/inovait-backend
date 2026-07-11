using System.Data;
using Inovait.Core.Domain.Academics;
using Inovait.Core.Domain.People;
using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Inovait.Infrastructure.Features.Enrollments;

public sealed class EfEnrollmentWorkflow(InovaitDbContext context) : IEnrollmentRepository, IEnrollmentTransaction
{
    private Func<int, CancellationToken, ValueTask<bool>> _beforeAttempt =
        static (_, _) => ValueTask.FromResult(false);
    private Action<int, CancellationToken> _attemptObserved = static (_, _) => { };

    internal EfEnrollmentWorkflow(InovaitDbContext context,
        Func<int, CancellationToken, ValueTask<bool>> beforeAttempt,
        Action<int, CancellationToken> attemptObserved) : this(context) =>
        (_beforeAttempt, _attemptObserved) = (beforeAttempt, attemptObserved);
    public async ValueTask<short?> FindDocumentTypeIdAsync(string code, CancellationToken cancellationToken) =>
        await context.DocumentTypes.AsNoTracking().Where(type => type.Code == code)
            .Select(type => (short?)type.Id).SingleOrDefaultAsync(cancellationToken);
    public async ValueTask<PersonIdentity?> FindPersonAsync(
        short documentTypeId, string documentNumber, CancellationToken cancellationToken) =>
        await context.People.AsNoTracking()
            .Where(person => person.DocumentTypeId == documentTypeId && person.DocumentNumber == documentNumber)
            .Select(person => new PersonIdentity(person.Id, person.FirstNames, person.LastNames, person.BirthDate,
                context.Students.Any(student => student.PersonId == person.Id),
                context.Teachers.Any(teacher => teacher.PersonId == person.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    public async ValueTask<EnrollmentError?> ValidateContextAsync(
        CreateEnrollmentCommand command, CancellationToken cancellationToken)
    {
        if (!await context.Schools.AnyAsync(entity => entity.Id == command.SchoolId, cancellationToken))
            return EnrollmentError.SchoolNotFound;
        if (!await context.AcademicYears.AnyAsync(entity => entity.Id == command.AcademicYearId, cancellationToken))
            return EnrollmentError.AcademicYearNotFound;
        if (!await context.Grades.AnyAsync(entity => entity.Id == command.GradeId, cancellationToken))
            return EnrollmentError.GradeNotFound;
        var group = await context.ClassGroups.AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == command.ClassGroupId, cancellationToken);
        if (group is null)
            return EnrollmentError.ClassGroupNotFound;
        return group.SchoolId == command.SchoolId && group.AcademicYearId == command.AcademicYearId &&
            group.GradeId == command.GradeId ? null : EnrollmentError.AcademicContextMismatch;
    }
    public async ValueTask<bool> EnrollmentExistsAsync(
        int studentId, int academicYearId, CancellationToken cancellationToken) =>
        await context.Enrollments.AnyAsync(
            entity => entity.StudentPersonId == studentId && entity.AcademicYearId == academicYearId,
            cancellationToken);
    public async ValueTask<int> CreatePersonAsync(IdentityResolution identity, CancellationToken cancellationToken)
    {
        var person = new Person(identity.DocumentTypeId, identity.DocumentNumber,
            identity.FirstNames, identity.LastNames, identity.BirthDate);
        context.People.Add(person);
        await context.SaveChangesAsync(cancellationToken);
        return person.Id;
    }
    public async ValueTask<int> CreateEnrollmentAsync(
        int studentId, int classGroupId, int academicYearId, bool createStudentRole,
        CancellationToken cancellationToken)
    {
        if (createStudentRole)
            context.Students.Add(new Student(studentId));
        var enrollment = new Enrollment(studentId, classGroupId, academicYearId);
        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(cancellationToken);
        return enrollment.Id;
    }
    public async ValueTask<EnrollmentResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<EnrollmentResult>> operation, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await using var databaseTransaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                if (await _beforeAttempt(attempt, cancellationToken))
                    throw new InjectedRaceException();
                var result = await operation(cancellationToken);
                if (result.IsSuccess)
                    await databaseTransaction.CommitAsync(cancellationToken);
                else
                    await RollbackAsync(databaseTransaction);
                return result;
            }
            catch (Exception exception) when (IsRace(exception))
            {
                await RollbackAsync(databaseTransaction);
                _attemptObserved(attempt, CancellationToken.None);
                if (attempt == attempts)
                    return EnrollmentResult.Failure(EnrollmentError.ConcurrencyConflict);
            }
            catch
            {
                await RollbackAsync(databaseTransaction);
                throw;
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        }
        return EnrollmentResult.Failure(EnrollmentError.ConcurrencyConflict);
    }
    private async Task RollbackAsync(IDbContextTransaction transaction)
    {
        var cancellationToken = CancellationToken.None;
        _attemptObserved(0, cancellationToken);
        await transaction.RollbackAsync(cancellationToken);
    }
    private static bool IsRace(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is InjectedRaceException or SqlException { Number: 1205 or 2601 or 2627 })
                return true;
        return false;
    }
    private sealed class InjectedRaceException : Exception { }
}
