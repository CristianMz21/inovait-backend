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

    public IResult ToProblem() => ProblemFactory.Create(
        StatusCodes.Status400BadRequest,
        "invalid-request",
        "La solicitud no es válida",
        "invalid_request",
        errors: _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
}
