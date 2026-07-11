namespace Inovait.Core.Domain.Common;

public interface ITextNormalizer
{
    string? Normalize(string? value);

    string NormalizeRequired(string? value);
}
