namespace Inovait.Core.Domain.Academics;

public sealed class ClassSchedule(int teachingAssignmentId, byte weekday)
{
    public int TeachingAssignmentId { get; private set; } = teachingAssignmentId;
    public byte Weekday { get; private set; } = weekday;
    public DateTime CreatedAtUtc { get; private set; }
}
