using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.People;

public sealed class Person(
    short documentTypeId, string documentNumber, string firstNames, string lastNames, DateOnly birthDate)
    : AuditableEntity
{
    public int Id { get; private set; }
    public short DocumentTypeId { get; private set; } = documentTypeId;
    public string DocumentNumber { get; private set; } = documentNumber;
    public string FirstNames { get; private set; } = firstNames;
    public string LastNames { get; private set; } = lastNames;
    public DateOnly BirthDate { get; private set; } = birthDate;
}
