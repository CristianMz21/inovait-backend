namespace Inovait.Api.Errors;

internal sealed class RequestValidator
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool HasErrors => _errors.Count > 0;

    public RequestValidator Require(string field, bool condition, string message)
    {
        if (!condition)
        {
            if (!_errors.TryGetValue(field, out var messages))
            {
                _errors[field] = messages = [];
            }

            messages.Add(message);
        }

        return this;
    }

    public RequestValidator RequireOptionalPositiveId(string field, int? value)
    {
        if (value is int id)
        {
            Require(field, id >= 1, "Debe ser mayor o igual a 1.");
        }

        return this;
    }

    public RequestValidator RequirePositiveId(string field, int? value) =>
        Require(field, value is not null, "El campo es obligatorio.")
            .RequireOptionalPositiveId(field, value);

    public IResult ToProblem() => ProblemFactory.Create(
        StatusCodes.Status400BadRequest,
        "invalid-request",
        "La solicitud no es válida",
        "invalid_request",
        errors: _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
}
