using Inovait.Api.Contracts;
using Inovait.Api.Errors;
using Inovait.Api.Reads;
using Inovait.Core.Features.TeacherContracts;

namespace Inovait.Api.Endpoints;

public static class TeacherContractEndpoints
{
    public static IEndpointRouteBuilder MapTeacherContractEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/teachers/{teacherId}/contracts");

        group.MapPost("", async (
            int teacherId, CreateTeacherContractsRequest request, CreateTeacherContractsHandler handler,
            TeacherContractReadService reads, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            var validation = RequestValidation.ValidateCreateTeacherContracts(request);
            if (validation is not null)
            {
                return validation;
            }

            var command = new CreateTeacherContractsCommand(teacherId, request.SchoolIds, request.StartDate!.Value, request.EndDate);
            var result = await handler.HandleAsync(command, cancellationToken);
            if (!result.IsSuccess)
            {
                return TeacherContractProblems.Map(result.Error!.Value);
            }

            var evaluatedAt = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var response = await reads.GetCreatedAsync(result.ContractIds, evaluatedAt, cancellationToken);
            return Results.Created($"/api/teachers/{teacherId}/contracts", response);
        }).WithName("createTeacherContracts");

        group.MapGet("", async (
            int teacherId, DateOnly? asOfDate, TeacherContractReadService reads, ReferenceLookupService lookups,
            TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            if (!await lookups.TeacherExistsAsync(teacherId, cancellationToken))
            {
                return CatalogProblems.TeacherNotFound();
            }

            var evaluatedAt = asOfDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            return Results.Ok(await reads.ListByTeacherAsync(teacherId, evaluatedAt, cancellationToken));
        }).WithName("listTeacherContracts");

        return app;
    }
}
