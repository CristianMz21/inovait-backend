using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Academics;

public sealed class ClassGroup(int schoolId, int academicYearId, int gradeId, string code) : AuditableEntity
{
    public int Id { get; private set; }
    public int SchoolId { get; private set; } = schoolId;
    public int AcademicYearId { get; private set; } = academicYearId;
    public int GradeId { get; private set; } = gradeId;
    public string Code { get; private set; } = code;
}
