using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Catalogs;

public sealed class Grade(string code, string name, short sortOrder) : AuditableEntity
{
    public int Id { get; private set; }
    public string Code { get; private set; } = code;
    public string Name { get; set; } = name;
    public short SortOrder { get; set; } = sortOrder;
}
