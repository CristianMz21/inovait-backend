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

        group.MapGet("/class-groups", ListClassGroupsAsync)
            .WithName("listClassGroups");

        group.MapGet("/teachers", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListTeachersAsync(cancellationToken)))
            .WithName("listTeachers");

        group.MapGet("/subjects", async (CatalogReadService reads, CancellationToken cancellationToken) =>
            Results.Ok(await reads.ListSubjectsAsync(cancellationToken)))
            .WithName("listSubjects");

        group.MapGet("/schools/{schoolId}/teachers", ListTeachersBySchoolAsync)
            .WithName("listTeachersBySchool");

        return app;
    }

    private static async Task<IResult> ListClassGroupsAsync(
        [AsParameters] ClassGroupQuery query,
        CatalogReadService reads,
        ReferenceLookupService lookups,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator()
            .RequireOptionalPositiveId("schoolId", query.SchoolId)
            .RequireOptionalPositiveId("gradeId", query.GradeId)
            .RequireOptionalPositiveId("academicYearId", query.AcademicYearId);
        if (validator.HasErrors)
        {
            return validator.ToProblem();
        }

        if (query.SchoolId is int schoolId && !await lookups.SchoolExistsAsync(schoolId, cancellationToken))
        {
            return CatalogProblems.SchoolNotFound();
        }

        if (query.GradeId is int gradeId && !await lookups.GradeExistsAsync(gradeId, cancellationToken))
        {
            return CatalogProblems.GradeNotFound();
        }

        if (query.AcademicYearId is int academicYearId &&
            !await lookups.AcademicYearExistsAsync(academicYearId, cancellationToken))
        {
            return CatalogProblems.AcademicYearNotFound();
        }

        return Results.Ok(await reads.ListClassGroupsAsync(
            query.SchoolId, query.GradeId, query.AcademicYearId, cancellationToken));
    }

    private static async Task<IResult> ListTeachersBySchoolAsync(
        int schoolId,
        DateOnly? asOfDate,
        TeacherContractReadService reads,
        ReferenceLookupService lookups,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator().RequirePositiveId("schoolId", schoolId);
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
    }
}

internal sealed class ClassGroupQuery
{
    public int? SchoolId { get; init; }
    public int? GradeId { get; init; }
    public int? AcademicYearId { get; init; }
}
