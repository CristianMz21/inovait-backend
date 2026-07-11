using Inovait.Core.Domain.Common;
using Inovait.Infrastructure.Text;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
[Trait("Evidence", "UT-TEXT-NORMALIZATION")]
public sealed class TextNormalizerTests
{
    private readonly ITextNormalizer _normalizer = new TextNormalizer();

    [Fact]
    public void Normalize_CanonicalizesTextToNfc()
    {
        var decomposed = "Jose\u0301";

        var normalized = _normalizer.Normalize(decomposed);

        Assert.Equal("José", normalized);
    }

    [Theory]
    [InlineData("\u2003María\tJosé \n Pérez\u00A0", "María José Pérez")]
    [InlineData("\r\nAlpha\u2028\u2009Beta\tGamma\r\n", "Alpha Beta Gamma")]
    public void Normalize_TrimsAndCollapsesUnicodeWhitespace(string value, string expected)
    {
        Assert.Equal(expected, _normalizer.Normalize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n\u2003\u00A0")]
    public void NormalizeRequired_RejectsMissingOrWhitespaceOnlyText(string? value)
    {
        Assert.Throws<ArgumentException>(() => _normalizer.NormalizeRequired(value));
    }

    [Fact]
    public void Normalize_PreservesNullAndEmptyOptionalText()
    {
        Assert.Null(_normalizer.Normalize(null));
        Assert.Equal(string.Empty, _normalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_PreservesDiacriticsAndPunctuation()
    {
        const string value = "¡Árbol, pingüino & niño!";

        Assert.Equal(value, _normalizer.Normalize(value));
    }

    [Fact]
    public void Normalize_IsIdempotent()
    {
        const string value = "\u2003Jose\u0301\t\nPérez\u00A0";

        var normalized = _normalizer.NormalizeRequired(value);

        Assert.Equal(normalized, _normalizer.NormalizeRequired(normalized));
    }
}
