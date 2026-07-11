using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Catalogs;

public enum SchoolSector { Public, Private }

public sealed class School(string code, string name, SchoolSector sector) : AuditableEntity
{
    public int Id { get; private set; }
    public string Code { get; private set; } = code;
    public string Name { get; set; } = name;
    public SchoolSector Sector { get; private set; } = sector;
}
