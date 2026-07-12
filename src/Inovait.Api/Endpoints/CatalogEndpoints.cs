using Inovait.Api.Errors;
using Inovait.Api.Reads;

namespace Inovait.Api.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/schools", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListSchoolsAsync(cancellationToken)))
            .WithName("listSchools");

        group.MapGet("/grades", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListGradesAsync(cancellationToken)))
            .WithName("listGrades");

        group.MapGet("/academic-years", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListAcademicYearsAsync(cancellationToken)))
            .WithName("listAcademicYears");

        group.MapGet("/class-groups", async (
            int? schoolId, int? gradeId, int? academicYearId,
            CatalogReadService reads, ReferenceLookupService lookups, CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator()
                .Require("schoolId", schoolId is null or >= 1, "Debe ser mayor o igual a 1.")
                .Require("gradeId", gradeId is null or >= 1, "Debe ser mayor o igual a 1.")
                .Require("academicYearId", academicYearId is null or >= 1, "Debe ser mayor o igual a 1.");
            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            if (schoolId is int schoolIdValue && !await lookups.SchoolExistsAsync(schoolIdValue, cancellationToken))
            {
                return CatalogProblems.SchoolNotFound();
            }

            if (gradeId is int gradeIdValue && !await lookups.GradeExistsAsync(gradeIdValue, cancellationToken))
            {
                return CatalogProblems.GradeNotFound();
            }

            if (academicYearId is int academicYearIdValue &&
                !await lookups.AcademicYearExistsAsync(academicYearIdValue, cancellationToken))
            {
                return CatalogProblems.AcademicYearNotFound();
            }

            return Results.Ok(await reads.ListClassGroupsAsync(schoolId, gradeId, academicYearId, cancellationToken));
        }).WithName("listClassGroups");

        group.MapGet("/teachers", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListTeachersAsync(cancellationToken)))
            .WithName("listTeachers");

        group.MapGet("/subjects", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListSubjectsAsync(cancellationToken)))
            .WithName("listSubjects");

        group.MapGet("/schools/{schoolId}/teachers", async (
            int schoolId, DateOnly? asOfDate, TeacherContractReadService reads, ReferenceLookupService lookups,
            TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator().Require("schoolId", schoolId >= 1, "Debe ser mayor o igual a 1.");
            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            if (!await lookups.SchoolExistsAsync(schoolId, cancellationToken))
            {
                return CatalogProblems.SchoolNotFound();
            }

            var evaluatedAt = asOfDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            return Results.Ok(await reads.ListBySchoolAsync(schoolId, evaluatedAt, cancellationToken));
        }).WithName("listTeachersBySchool");

        return app;
    }
}
