namespace Inovait.Core.Domain.Catalogs;

public sealed class AcademicConfiguration(byte id, int currentAcademicYearId)
{
    public byte Id { get; private set; } = id;
    public int CurrentAcademicYearId { get; set; } = currentAcademicYearId;
}
