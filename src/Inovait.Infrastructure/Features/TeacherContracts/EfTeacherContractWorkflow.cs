using System.Data;
using Inovait.Core.Domain.Staff;
using Inovait.Core.Features.TeacherContracts;
using Inovait.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inovait.Infrastructure.Features.TeacherContracts;

public sealed class EfTeacherContractWorkflow(InovaitDbContext context)
    : ITeacherContractRepository, ITeacherContractTransaction
{
    private readonly Func<int, CancellationToken, ValueTask> _afterOverlapRead = static (_, _) => ValueTask.CompletedTask;
    private readonly Action<int, CancellationToken> _attemptObserved = static (_, _) => { };
    private int _attempt;

    internal EfTeacherContractWorkflow(InovaitDbContext context,
        Func<int, CancellationToken, ValueTask> afterOverlapRead,
        Action<int, CancellationToken> attemptObserved) : this(context) =>
        (_afterOverlapRead, _attemptObserved) = (afterOverlapRead, attemptObserved);

    public async ValueTask<bool> TeacherExistsAsync(int teacherPersonId, CancellationToken cancellationToken) =>
        await context.Teachers.AnyAsync(teacher => teacher.PersonId == teacherPersonId, cancellationToken);

    public async ValueTask<bool> SchoolExistsAsync(int schoolId, CancellationToken cancellationToken) =>
        await context.Schools.AnyAsync(school => school.Id == schoolId, cancellationToken);

    public async ValueTask<bool> OverlapsAsync(int teacherPersonId, int schoolId,
        DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var lastDate = endDate ?? DateOnly.MaxValue;
        var overlaps = await context.TeacherContracts.AsNoTracking().AnyAsync(contract =>
            contract.TeacherPersonId == teacherPersonId && contract.SchoolId == schoolId &&
            contract.StartDate <= lastDate && (contract.EndDate == null || contract.EndDate >= startDate),
            cancellationToken);
        await _afterOverlapRead(_attempt, cancellationToken);
        return overlaps;
    }

    public async ValueTask<int> CreateAsync(int teacherPersonId, int schoolId,
        DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken)
    {
        var contract = new TeacherContract(teacherPersonId, schoolId, startDate, endDate);
        context.TeacherContracts.Add(contract);
        await context.SaveChangesAsync(cancellationToken);
        return contract.Id;
    }

    public async ValueTask<TeacherContractResult> ExecuteAsync(
        Func<CancellationToken, ValueTask<TeacherContractResult>> operation, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (_attempt = 1; _attempt <= attempts; _attempt++)
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
                _attemptObserved(_attempt, CancellationToken.None);
                if (_attempt == attempts)
                    return TeacherContractResult.Failure(TeacherContractError.ConcurrencyConflict);
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
        return TeacherContractResult.Failure(TeacherContractError.ConcurrencyConflict);
    }

    private async Task RollbackAsync(IDbContextTransaction transaction)
    {
        _attemptObserved(0, CancellationToken.None);
        await transaction.RollbackAsync(CancellationToken.None);
    }

    private static bool IsRace(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException { Number: 1205 or 2601 or 2627 })
                return true;
        return false;
    }
}
