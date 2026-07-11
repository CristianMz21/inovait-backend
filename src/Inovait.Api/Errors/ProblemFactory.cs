namespace Inovait.Api.Errors;

internal static class ProblemFactory
{
    public static IResult Create(
        int status, string typeSlug, string title, string code, string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        return Results.Problem(
            detail: detail,
            statusCode: status,
            title: title,
            type: $"https://inovait.local/problems/{typeSlug}",
            extensions: extensions);
    }
}
