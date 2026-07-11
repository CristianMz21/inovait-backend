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

        group.MapPost("", async (
            CreateEnrollmentRequest request, CreateEnrollmentHandler handler, EnrollmentReadService reads,
            CancellationToken cancellationToken) =>
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
        }).WithName("createEnrollment");

        group.MapGet("", async (
            int? schoolId, int? gradeId, int? academicYearId, DateOnly? asOfDate,
            EnrollmentReadService reads, ReferenceLookupService lookups, TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator()
                .Require("schoolId", schoolId is not null, "El campo es obligatorio.")
                .Require("gradeId", gradeId is not null, "El campo es obligatorio.")
                .Require("academicYearId", academicYearId is not null, "El campo es obligatorio.");
            if (schoolId is int schoolIdValue)
            {
                validator.Require("schoolId", schoolIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (gradeId is int gradeIdValue)
            {
                validator.Require("gradeId", gradeIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (academicYearId is int academicYearIdValue)
            {
                validator.Require("academicYearId", academicYearIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            if (!await lookups.SchoolExistsAsync(schoolId!.Value, cancellationToken))
            {
                return CatalogProblems.SchoolNotFound();
            }

            if (!await lookups.GradeExistsAsync(gradeId!.Value, cancellationToken))
            {
                return CatalogProblems.GradeNotFound();
            }

            if (!await lookups.AcademicYearExistsAsync(academicYearId!.Value, cancellationToken))
            {
                return CatalogProblems.AcademicYearNotFound();
            }

            var items = await reads.ListAsync(schoolId.Value, gradeId.Value, academicYearId.Value, asOfDate, cancellationToken);
            return Results.Ok(items);
        }).WithName("listEnrollments");

        return app;
    }
}
