using Inovait.Core.Features.Enrollments;
using Inovait.Infrastructure.Text;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
[Trait("Evidence", "UT-IDENTITY")]
public sealed class IdentityResolverTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ResolveAsync_ReusesEquivalentIdentityWithoutDuplicatingStudentRole(
        bool isStudent, bool createStudentRole)
    {
        var reader = new StubIdentityReader(new(42, "José Luis", "Pérez", new(2010, 4, 2), isStudent, true));
        var resolver = CreateResolver(reader);
        var result = await resolver.ResolveAsync(
            new("  cc ", " ab\t123 ", "JOSE\u0301  LUIS", "péREZ", new(2010, 4, 2)),
            TestContext.Current.CancellationToken);
        Assert.Equal(IdentityResolutionStatus.ReusePerson, result.Status);
        Assert.Equal(42, result.PersonId);
        Assert.Equal(createStudentRole, result.CreateStudentRole);
        Assert.Equal(("cc", (short)1, "ab 123"), reader.Lookup);
    }
    [Theory]
    [InlineData("Jose", "Pérez", 2010)]
    [InlineData("José", "Other", 2010)]
    [InlineData("José", "Pérez", 2011)]
    public async Task ResolveAsync_ReportsConflictWhenIdentityDataDiffers(
        string firstNames, string lastNames, int birthYear)
    {
        var resolver = CreateResolver(new StubIdentityReader(
            new(42, "José", "Pérez", new(2010, 4, 2), true, false)));
        var result = await resolver.ResolveAsync(
            new("CC", "AB 123", firstNames, lastNames, new(birthYear, 4, 2)),
            TestContext.Current.CancellationToken);
        Assert.Equal(IdentityResolutionStatus.Conflict, result.Status);
        Assert.Equal(42, result.PersonId);
        Assert.False(result.CreateStudentRole);
    }
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task ResolveAsync_RejectsOnlyFutureBirthDate(int dayOffset, bool shouldThrow)
    {
        var resolver = CreateResolver(new StubIdentityReader(null));
        var action = () => resolver.ResolveAsync(new("CC", "NEW-01", "Ada", "Lovelace",
            new DateOnly(2026, 7, 11).AddDays(dayOffset)), TestContext.Current.CancellationToken).AsTask();
        if (shouldThrow)
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(action);
        else
            Assert.Equal(IdentityResolutionStatus.NewPerson, (await action()).Status);
    }
    [Fact]
    public async Task ResolveAsync_TreatsSameNumberUnderAnotherDocumentTypeAsNewIdentity()
    {
        var reader = new StubIdentityReader(new(42, "Ada", "Lovelace", new(1815, 12, 10), true, false));
        var result = await CreateResolver(reader).ResolveAsync(
            new("PP", " ab\t123 ", " Ada ", " Lovelace ", new(1815, 12, 10)),
            TestContext.Current.CancellationToken);
        Assert.Equal(IdentityResolutionStatus.NewPerson, result.Status);
        Assert.Null(result.PersonId);
        Assert.Equal(((short)2, "ab 123", "Ada", "Lovelace"),
            (result.DocumentTypeId, result.DocumentNumber, result.FirstNames, result.LastNames));
        Assert.True(result.CreateStudentRole);
        Assert.Equal(("PP", (short)2, "ab 123"), reader.Lookup);
    }
    [Fact]
    public async Task ResolveAsync_RejectsUnknownDocumentType()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => CreateResolver(new StubIdentityReader(null)).ResolveAsync(
            new("XX", "NEW-01", "Ada", "Lovelace", new(1815, 12, 10)),
            TestContext.Current.CancellationToken).AsTask());
    }
    private static IdentityResolver CreateResolver(IIdentityReader reader) =>
        new(new TextNormalizer(), reader, new FixedTimeProvider(new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero)));
    private sealed class StubIdentityReader(PersonIdentity? person) : IIdentityReader
    {
        private string _code = string.Empty;
        public (string Code, short TypeId, string Number) Lookup { get; private set; }

        public ValueTask<short?> FindDocumentTypeIdAsync(string code, CancellationToken cancellationToken)
        {
            _code = code;
            return ValueTask.FromResult<short?>(StringComparer.OrdinalIgnoreCase.Equals(code, "CC") ? (short)1
                : StringComparer.OrdinalIgnoreCase.Equals(code, "PP") ? (short)2 : null);
        }
        public ValueTask<PersonIdentity?> FindPersonAsync(
            short documentTypeId, string documentNumber, CancellationToken cancellationToken)
        {
            Lookup = (_code, documentTypeId, documentNumber);
            return ValueTask.FromResult(documentTypeId == 1 && StringComparer.OrdinalIgnoreCase.Equals(documentNumber, "AB 123")
                ? person : null);
        }
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
