using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Catalogs;

public sealed class AcademicYear(
    string code, string name, DateOnly startDate, DateOnly endDate) : AuditableEntity
{
    public int Id { get; private set; }
    public string Code { get; private set; } = code;
    public string Name { get; set; } = name;
    public DateOnly StartDate { get; set; } = startDate;
    public DateOnly EndDate { get; set; } = endDate;
}
