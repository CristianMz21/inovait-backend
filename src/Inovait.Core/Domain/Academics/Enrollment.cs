namespace Inovait.Core.Domain.Academics;

public sealed class Enrollment(int studentPersonId, int classGroupId, int academicYearId)
{
    public int Id { get; private set; }
    public int StudentPersonId { get; private set; } = studentPersonId;
    public int ClassGroupId { get; private set; } = classGroupId;
    public int AcademicYearId { get; private set; } = academicYearId;
    public DateTime CreatedAtUtc { get; private set; }
}
