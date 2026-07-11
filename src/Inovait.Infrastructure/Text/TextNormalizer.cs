using System.Text;
using Inovait.Core.Domain.Common;

namespace Inovait.Infrastructure.Text;

public sealed class TextNormalizer : ITextNormalizer
{
    public string? Normalize(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var canonical = value.Normalize(NormalizationForm.FormC);
        var result = new StringBuilder(canonical.Length);
        var hasPendingWhitespace = false;

        foreach (var character in canonical)
        {
            if (char.IsWhiteSpace(character))
            {
                hasPendingWhitespace = result.Length > 0;
                continue;
            }

            if (hasPendingWhitespace)
            {
                result.Append(' ');
                hasPendingWhitespace = false;
            }

            result.Append(character);
        }

        return result.ToString();
    }

    public string NormalizeRequired(string? value)
    {
        var normalized = Normalize(value);

        if (string.IsNullOrEmpty(normalized))
        {
            throw new ArgumentException("Required text must contain a non-whitespace character.", nameof(value));
        }

        return normalized;
    }
}
