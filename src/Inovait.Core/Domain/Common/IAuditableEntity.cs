namespace Inovait.Core.Domain.Common;

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }

    DateTime UpdatedAtUtc { get; }
}
