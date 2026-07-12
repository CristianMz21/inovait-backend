using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Academics;

public sealed class TeachingAssignment(
    int teacherContractId, int classGroupId, int subjectId, DateOnly startDate, DateOnly? endDate) : AuditableEntity
{
    public int Id { get; private set; }
    public int TeacherContractId { get; private set; } = teacherContractId;
    public int ClassGroupId { get; private set; } = classGroupId;
    public int SubjectId { get; private set; } = subjectId;
    public DateOnly StartDate { get; private set; } = startDate;
    public DateOnly? EndDate { get; private set; } = ValidateEndDate(startDate, endDate);

    private static DateOnly? ValidateEndDate(DateOnly startDate, DateOnly? endDate) =>
        endDate < startDate ? throw new ArgumentOutOfRangeException(nameof(endDate)) : endDate;
}
