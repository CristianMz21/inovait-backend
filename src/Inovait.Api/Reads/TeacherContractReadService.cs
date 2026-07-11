using Inovait.Api.Contracts;
using Inovait.Core.Domain.Catalogs;
using Inovait.Core.Domain.Staff;
using Inovait.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inovait.Api.Reads;

public sealed class TeacherContractReadService(InovaitDbContext context)
{
    public async Task<IReadOnlyList<TeacherContractResponse>> GetCreatedAsync(
        IReadOnlyList<int> contractIds, DateOnly evaluatedAt, CancellationToken cancellationToken)
    {
        var contracts = await context.TeacherContracts.AsNoTracking()
            .Where(contract => contractIds.Contains(contract.Id))
            .ToListAsync(cancellationToken);
        var schools = await LoadSchoolsAsync(contracts.Select(contract => contract.SchoolId), cancellationToken);
        return contracts
            .OrderBy(contract => schools[contract.SchoolId].Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => contract.Id)
            .Select(contract => Map(contract, schools[contract.SchoolId], evaluatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<TeacherContractResponse>> ListByTeacherAsync(
        int teacherPersonId, DateOnly evaluatedAt, CancellationToken cancellationToken)
    {
        var contracts = await context.TeacherContracts.AsNoTracking()
            .Where(contract => contract.TeacherPersonId == teacherPersonId)
            .ToListAsync(cancellationToken);
        var schools = await LoadSchoolsAsync(contracts.Select(contract => contract.SchoolId), cancellationToken);
        return contracts
            .OrderBy(contract => contract.StartDate)
            .ThenBy(contract => schools[contract.SchoolId].Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => contract.Id)
            .Select(contract => Map(contract, schools[contract.SchoolId], evaluatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<SchoolTeacherSummary>> ListBySchoolAsync(
        int schoolId, DateOnly evaluatedAt, CancellationToken cancellationToken)
    {
        var contracts = await context.TeacherContracts.AsNoTracking()
            .Where(contract => contract.SchoolId == schoolId)
            .ToListAsync(cancellationToken);
        var teacherIds = contracts.Select(contract => contract.TeacherPersonId).Distinct().ToArray();
        var teachers = await (
            from person in context.People.AsNoTracking()
            join documentType in context.DocumentTypes.AsNoTracking() on person.DocumentTypeId equals documentType.Id
            where teacherIds.Contains(person.Id)
            select new TeacherSummary(person.Id, documentType.Code, person.DocumentNumber, person.FirstNames, person.LastNames))
            .ToDictionaryAsync(teacher => teacher.Id, cancellationToken);
        return contracts
            .OrderBy(contract => teachers[contract.TeacherPersonId].LastNames, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => teachers[contract.TeacherPersonId].FirstNames, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => teachers[contract.TeacherPersonId].DocumentType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => teachers[contract.TeacherPersonId].DocumentNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(contract => contract.Id)
            .Select(contract => new SchoolTeacherSummary(
                teachers[contract.TeacherPersonId], contract.Id, contract.Status.ToString(),
                contract.GetEffectiveStatus(evaluatedAt).ToString(), evaluatedAt, contract.StartDate, contract.EndDate))
            .ToList();
    }

    private async Task<Dictionary<int, School>> LoadSchoolsAsync(IEnumerable<int> schoolIds, CancellationToken cancellationToken)
    {
        var ids = schoolIds.Distinct().ToArray();
        return await context.Schools.AsNoTracking()
            .Where(school => ids.Contains(school.Id))
            .ToDictionaryAsync(school => school.Id, cancellationToken);
    }

    private static TeacherContractResponse Map(TeacherContract contract, School school, DateOnly evaluatedAt) => new(
        contract.Id, contract.TeacherPersonId, new SchoolSummary(school.Id, school.Name, school.Sector.ToString()),
        contract.StartDate, contract.EndDate, contract.Status.ToString(),
        contract.GetEffectiveStatus(evaluatedAt).ToString(), evaluatedAt);
}
