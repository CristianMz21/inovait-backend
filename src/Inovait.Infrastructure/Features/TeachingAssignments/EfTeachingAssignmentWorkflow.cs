using System.Data;
using Inovait.Core.Domain.Academics;
using Inovait.Core.Features.TeachingAssignments;
using Inovait.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inovait.Infrastructure.Features.TeachingAssignments;

public sealed class EfTeachingAssignmentWorkflow(InovaitDbContext context)
    : ITeachingAssignmentRepository, ITeachingAssignmentTransaction
{
    public async ValueTask<TeacherContractSnapshot?> FindTeacherContractAsync(
        int teacherContractId, CancellationToken cancellationToken) =>
        await context.TeacherContracts.AsNoTracking()
            .Where(contract => contract.Id == teacherContractId)
            .Select(contract => new TeacherContractSnapshot(
                contract.SchoolId, contract.StartDate, contract.EndDate, contract.CancellationEffectiveDate))
            .SingleOrDefaultAsync(cancellationToken);

    public async ValueTask<ClassGroupSnapshot?> FindClassGroupAsync(
        int classGroupId, CancellationToken cancellationToken) =>
        await (from classGroup in context.ClassGroups.AsNoTracking()
               join year in context.AcademicYears.AsNoTracking() on classGroup.AcademicYearId equals year.Id
               where classGroup.Id == classGroupId
               select new ClassGroupSnapshot(classGroup.SchoolId, year.StartDate, year.EndDate))
            .SingleOrDefaultAsync(cancellationToken);

    public async ValueTask<bool> SubjectExistsAsync(int subjectId, CancellationToken cancellationToken) =>
        await context.Subjects.AnyAsync(subject => subject.Id == subjectId, cancellationToken);

    public async ValueTask<int> CreateAsync(int teacherContractId, int classGroupId, int subjectId,
        DateOnly startDate, DateOnly? endDate, IReadOnlyList<byte> weekdays, CancellationToken cancellationToken)
    {
        var assignment = new TeachingAssignment(teacherContractId, classGroupId, subjectId, startDate, endDate);
        context.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var weekday in weekdays)
            context.Add(new ClassSchedule(assignment.Id, weekday));
        await context.SaveChangesAsync(cancellationToken);
        return assignment.Id;
    }

    public async ValueTask<TeachingAssignmentResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TeachingAssignmentResult>> operation, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                if (result.IsSuccess)
                    await transaction.CommitAsync(cancellationToken);
                else
                    await RollbackAsync(transaction);
                return result;
            }
            catch (Exception exception) when (IsRace(exception))
            {
                await RollbackAsync(transaction);
                if (attempt == attempts)
                    return TeachingAssignmentResult.Failure(TeachingAssignmentError.ConcurrencyConflict);
            }
            catch
            {
                await RollbackAsync(transaction);
                throw;
            }
            finally
            {
                context.ChangeTracker.Clear();
            }
        }
        return TeachingAssignmentResult.Failure(TeachingAssignmentError.ConcurrencyConflict);
    }

    private static async Task RollbackAsync(IDbContextTransaction transaction) =>
        await transaction.RollbackAsync(CancellationToken.None);

    private static bool IsRace(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 1205 or 2601 or 2627 })
                return true;
        return false;
    }
}
