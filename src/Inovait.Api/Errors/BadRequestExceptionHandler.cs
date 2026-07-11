using Microsoft.AspNetCore.Diagnostics;

namespace Inovait.Api.Errors;

internal sealed class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException)
        {
            return false;
        }

        var problem = ProblemFactory.Create(
            StatusCodes.Status400BadRequest,
            "invalid-request",
            "La solicitud no es válida",
            "invalid_request",
            "La solicitud no pudo interpretarse por ausencia, tipo o formato inválido.");
        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
