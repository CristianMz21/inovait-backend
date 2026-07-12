using Inovait.Api.Errors;
using Inovait.Api.Reads;

namespace Inovait.Api.Endpoints;

public static class StudentHistoryEndpoints
{
    public static IEndpointRouteBuilder MapStudentHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/students/{documentType}/{documentNumber}/history", async (
            string documentType, string documentNumber, StudentHistoryReadService reads, CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator()
                .Require("documentType", documentType.Length is >= 1 and <= 20, "El campo debe tener entre 1 y 20 caracteres.")
                .Require("documentNumber", documentNumber.Length is >= 1 and <= 32, "El campo debe tener entre 1 y 32 caracteres.");
            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            var response = await reads.GetAsync(documentType, documentNumber, cancellationToken);
            return response is null ? StudentHistoryProblems.StudentNotFound() : Results.Ok(response);
        }).WithName("getStudentHistory");

        return app;
    }
}
