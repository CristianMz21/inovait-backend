using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.People;

public sealed class Teacher(int personId) : AuditableEntity
{
    public int PersonId { get; private set; } = personId;
}
