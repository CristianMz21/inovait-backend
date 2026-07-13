using Inovait.Api.Errors;
using Inovait.Api.Reads;

namespace Inovait.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports");

        group.MapGet("/age-distribution", GetAgeDistributionAsync)
            .WithName("getAgeDistribution");
        group.MapGet("/teacher-counts-by-sector", GetDistinctTeacherCountsBySectorAsync)
            .WithName("getDistinctTeacherCountsBySector");
        group.MapGet("/top-schools", GetTopSchoolsByEnrollmentAsync)
            .WithName("getTopSchoolsByEnrollment");

        return app;
    }

    private static async Task<IResult> GetAgeDistributionAsync(
        [AsParameters] AgeDistributionQuery query,
        ReportReadService reads,
        ReferenceLookupService lookups,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator()
            .RequirePositiveId("academicYearId", query.AcademicYearId)
            .RequireOptionalPositiveId("schoolId", query.SchoolId)
            .RequireOptionalPositiveId("gradeId", query.GradeId);
        if (validator.HasErrors)
        {
            return validator.ToProblem();
        }

        if (!await lookups.AcademicYearExistsAsync(query.AcademicYearId!.Value, cancellationToken))
        {
            return CatalogProblems.AcademicYearNotFound();
        }

        if (query.SchoolId is int schoolId && !await lookups.SchoolExistsAsync(schoolId, cancellationToken))
        {
            return CatalogProblems.SchoolNotFound();
        }

        if (query.GradeId is int gradeId && !await lookups.GradeExistsAsync(gradeId, cancellationToken))
        {
            return CatalogProblems.GradeNotFound();
        }

        var evaluatedAsOfDate = query.AsOfDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var result = await reads.GetAgeDistributionAsync(
            query.AcademicYearId.Value, query.SchoolId, query.GradeId, evaluatedAsOfDate, cancellationToken);
        return result.AsOfDateInvalid
            ? ReportProblems.AsOfDateInvalid()
            : Results.Ok(result.Response);
    }

    private static async Task<IResult> GetDistinctTeacherCountsBySectorAsync(
        DateOnly? periodStart,
        DateOnly? periodEnd,
        ReportReadService reads,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator()
            .Require("periodStart", periodStart is not null || periodEnd is null,
                "Debe enviarse junto con periodEnd.")
            .Require("periodEnd", periodEnd is not null || periodStart is null,
                "Debe enviarse junto con periodStart y ser igual o posterior.");
        if (validator.HasErrors)
        {
            return validator.ToProblem();
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var effectivePeriodStart = periodStart ?? today;
        var effectivePeriodEnd = periodEnd ?? today;
        if (effectivePeriodEnd < effectivePeriodStart)
        {
            return ReportProblems.PeriodInvalid();
        }

        var response = await reads.GetDistinctTeacherCountsBySectorAsync(
            effectivePeriodStart, effectivePeriodEnd, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetTopSchoolsByEnrollmentAsync(
        int? academicYearId,
        ReportReadService reads,
        ReferenceLookupService lookups,
        CancellationToken cancellationToken)
    {
        var validator = new RequestValidator()
            .RequirePositiveId("academicYearId", academicYearId);
        if (validator.HasErrors)
        {
            return validator.ToProblem();
        }

        if (!await lookups.AcademicYearExistsAsync(academicYearId!.Value, cancellationToken))
        {
            return CatalogProblems.AcademicYearNotFound();
        }

        return Results.Ok(await reads.GetTopSchoolsByEnrollmentAsync(academicYearId.Value, cancellationToken));
    }
}

internal sealed class AgeDistributionQuery
{
    public int? AcademicYearId { get; init; }
    public int? SchoolId { get; init; }
    public int? GradeId { get; init; }
    public DateOnly? AsOfDate { get; init; }
}
