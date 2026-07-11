namespace Inovait.Core.Domain.Common;

public abstract class AuditableEntity : IAuditableEntity
{
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
