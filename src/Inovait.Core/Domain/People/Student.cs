namespace Inovait.Core.Domain.People;

public sealed class Student(int personId)
{
    public int PersonId { get; private set; } = personId;
}
