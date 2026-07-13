using Inovait.Api.Contracts;
using Inovait.Api.Errors;
using Inovait.Api.Reads;
using Inovait.Core.Features.Enrollments;

namespace Inovait.Api.Endpoints;

public static class EnrollmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrollmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/enrollments");

        group.MapPost("", CreateEnrollmentAsync)
            .WithName("createEnrollment");
        group.MapGet("", ListEnrollmentsAsync)
            .WithName("listEnrollments");

        return app;
    }

    private static async Task<IResult> CreateEnrollmentAsync(
        CreateEnrollmentRequest request,
        CreateEnrollmentHandler handler,
        EnrollmentReadService reads,
        CancellationToken cancellationToken)
    {
        var validation = RequestValidation.ValidateCreateEnrollment(request);
        if (validation is not null)
        {
            return validation;
        }

        var command = new CreateEnrollmentCommand(
            new IdentityRequest(request.Student.DocumentType, request.Student.DocumentNumber,
                request.Student.FirstNames, request.Student.LastNames, request.Student.BirthDate!.Value),
            request.SchoolId, request.AcademicYearId, request.GradeId, request.ClassGroupId);

        var result = await handler.HandleAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return EnrollmentProblems.Map(result.Error!.Value);
        }

        var response = await reads.GetCreatedAsync(result.EnrollmentId!.Value, result.StudentReused, cancellationToken);
        return Results.Created($"/api/enrollments/{result.EnrollmentId}", response);
    }

    private static async Task<IResult> ListEnrollmentsAsync(
        [AsParameters] EnrollmentQuery query,
        EnrollmentReadService reads,
        ReferenceLookupService lookups,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator()
            .RequirePositiveId("schoolId", query.SchoolId)
            .RequirePositiveId("gradeId", query.GradeId)
            .RequirePositiveId("academicYearId", query.AcademicYearId);
        if (validator.HasErrors)
        {
            return validator.ToProblem();
        }

        if (!await lookups.SchoolExistsAsync(query.SchoolId!.Value, cancellationToken))
        {
            return CatalogProblems.SchoolNotFound();
        }

        if (!await lookups.GradeExistsAsync(query.GradeId!.Value, cancellationToken))
        {
            return CatalogProblems.GradeNotFound();
        }

        if (!await lookups.AcademicYearExistsAsync(query.AcademicYearId!.Value, cancellationToken))
        {
            return CatalogProblems.AcademicYearNotFound();
        }

        var items = await reads.ListAsync(
            query.SchoolId.Value,
            query.GradeId.Value,
            query.AcademicYearId.Value,
            query.AsOfDate,
            cancellationToken);
        return Results.Ok(items);
    }
}

internal sealed class EnrollmentQuery
{
    public int? SchoolId { get; init; }
    public int? GradeId { get; init; }
    public int? AcademicYearId { get; init; }
    public DateOnly? AsOfDate { get; init; }
}
