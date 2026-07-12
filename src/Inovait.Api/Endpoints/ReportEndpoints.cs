using Inovait.Api.Errors;
using Inovait.Api.Reads;

namespace Inovait.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports");

        group.MapGet("/age-distribution", async (
            int? academicYearId, int? schoolId, int? gradeId, DateOnly? asOfDate,
            ReportReadService reads, ReferenceLookupService lookups, TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator()
                .Require("academicYearId", academicYearId is not null, "El campo es obligatorio.");
            if (academicYearId is int academicYearIdValue)
            {
                validator.Require("academicYearId", academicYearIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (schoolId is int schoolIdValue)
            {
                validator.Require("schoolId", schoolIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (gradeId is int gradeIdValue)
            {
                validator.Require("gradeId", gradeIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            if (!await lookups.AcademicYearExistsAsync(academicYearId!.Value, cancellationToken))
            {
                return CatalogProblems.AcademicYearNotFound();
            }

            if (schoolId is int school && !await lookups.SchoolExistsAsync(school, cancellationToken))
            {
                return CatalogProblems.SchoolNotFound();
            }

            if (gradeId is int grade && !await lookups.GradeExistsAsync(grade, cancellationToken))
            {
                return CatalogProblems.GradeNotFound();
            }

            var evaluatedAsOfDate = asOfDate ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var result = await reads.GetAgeDistributionAsync(
                academicYearId.Value, schoolId, gradeId, evaluatedAsOfDate, cancellationToken);
            if (result.AsOfDateInvalid)
            {
                return ReportProblems.AsOfDateInvalid();
            }

            return Results.Ok(result.Response);
        }).WithName("getAgeDistribution");

        group.MapGet("/teacher-counts-by-sector", async (
            DateOnly? periodStart, DateOnly? periodEnd, ReportReadService reads, TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator();
            if (periodStart is null && periodEnd is not null)
            {
                validator.Require("periodStart", false, "Debe enviarse junto con periodEnd.");
            }

            if (periodEnd is null && periodStart is not null)
            {
                validator.Require("periodEnd", false, "Debe enviarse junto con periodStart y ser igual o posterior.");
            }

            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            DateOnly effectivePeriodStart;
            DateOnly effectivePeriodEnd;
            if (periodStart is null && periodEnd is null)
            {
                var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
                effectivePeriodStart = today;
                effectivePeriodEnd = today;
            }
            else
            {
                effectivePeriodStart = periodStart!.Value;
                effectivePeriodEnd = periodEnd!.Value;
                if (effectivePeriodEnd < effectivePeriodStart)
                {
                    return ReportProblems.PeriodInvalid();
                }
            }

            var response = await reads.GetDistinctTeacherCountsBySectorAsync(
                effectivePeriodStart, effectivePeriodEnd, cancellationToken);
            return Results.Ok(response);
        }).WithName("getDistinctTeacherCountsBySector");

        group.MapGet("/top-schools", async (
            int? academicYearId, ReportReadService reads, ReferenceLookupService lookups,
            CancellationToken cancellationToken) =>
        {
            var validator = new RequestValidator()
                .Require("academicYearId", academicYearId is not null, "El campo es obligatorio.");
            if (academicYearId is int academicYearIdValue)
            {
                validator.Require("academicYearId", academicYearIdValue >= 1, "Debe ser mayor o igual a 1.");
            }

            if (validator.HasErrors)
            {
                return validator.ToProblem();
            }

            if (!await lookups.AcademicYearExistsAsync(academicYearId!.Value, cancellationToken))
            {
                return CatalogProblems.AcademicYearNotFound();
            }

            return Results.Ok(await reads.GetTopSchoolsByEnrollmentAsync(academicYearId.Value, cancellationToken));
        }).WithName("getTopSchoolsByEnrollment");

        return app;
    }
}
