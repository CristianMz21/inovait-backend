namespace Inovait.Core.Domain.Catalogs;

public sealed class DocumentType(string code, string name, bool isActive)
{
    public short Id { get; private set; }
    public string Code { get; private set; } = code;
    public string Name { get; private set; } = name;
    public bool IsActive { get; private set; } = isActive;
}
