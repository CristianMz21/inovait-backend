using Inovait.Core.Domain.Common;

namespace Inovait.Core.Features.Enrollments;

public sealed record IdentityRequest(
    string DocumentTypeCode, string DocumentNumber, string FirstNames, string LastNames, DateOnly BirthDate);

public sealed record PersonIdentity(
    int PersonId, string FirstNames, string LastNames, DateOnly BirthDate, bool IsStudent, bool IsTeacher);

public enum IdentityResolutionStatus { NewPerson, ReusePerson, Conflict }

public sealed record IdentityResolution(
    IdentityResolutionStatus Status, int? PersonId, short DocumentTypeId, string DocumentNumber,
    string FirstNames, string LastNames, DateOnly BirthDate, bool CreateStudentRole);

public interface IIdentityReader
{
    ValueTask<short?> FindDocumentTypeIdAsync(string code, CancellationToken cancellationToken);
    ValueTask<PersonIdentity?> FindPersonAsync(
        short documentTypeId, string documentNumber, CancellationToken cancellationToken);
}

public sealed class IdentityResolver(
    ITextNormalizer normalizer, IIdentityReader reader, TimeProvider timeProvider)
{
    public async ValueTask<IdentityResolution> ResolveAsync(
        IdentityRequest request, CancellationToken cancellationToken = default)
    {
        var code = normalizer.NormalizeRequired(request.DocumentTypeCode);
        var number = normalizer.NormalizeRequired(request.DocumentNumber);
        var firstNames = normalizer.NormalizeRequired(request.FirstNames);
        var lastNames = normalizer.NormalizeRequired(request.LastNames);
        if (request.BirthDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new ArgumentOutOfRangeException(nameof(request.BirthDate), "Birth date cannot be in the future.");
        }

        var documentTypeId = await reader.FindDocumentTypeIdAsync(code, cancellationToken)
            ?? throw new KeyNotFoundException($"Document type '{code}' was not found.");
        var person = await reader.FindPersonAsync(documentTypeId, number, cancellationToken);
        if (person is null)
        {
            return new(IdentityResolutionStatus.NewPerson, null, documentTypeId, number,
                firstNames, lastNames, request.BirthDate, true);
        }

        var matches = StringComparer.OrdinalIgnoreCase.Equals(person.FirstNames, firstNames)
            && StringComparer.OrdinalIgnoreCase.Equals(person.LastNames, lastNames)
            && person.BirthDate == request.BirthDate;
        return new(matches ? IdentityResolutionStatus.ReusePerson : IdentityResolutionStatus.Conflict,
            person.PersonId, documentTypeId, number, firstNames, lastNames, request.BirthDate,
            matches && !person.IsStudent);
    }
}
