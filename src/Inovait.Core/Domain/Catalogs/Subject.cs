using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Catalogs;

public sealed class Subject(string code, string name) : AuditableEntity
{
    public int Id { get; private set; }
    public string Code { get; private set; } = code;
    public string Name { get; set; } = name;
}
