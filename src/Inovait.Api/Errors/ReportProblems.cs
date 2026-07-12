namespace Inovait.Api.Errors;

internal static class ReportProblems
{
    public static IResult AsOfDateInvalid() => ProblemFactory.Create(
        StatusCodes.Status422UnprocessableEntity, "as-of-date-invalid",
        "La fecha de referencia no es válida", "as_of_date_invalid",
        "La fecha de referencia no puede ser anterior a una fecha de nacimiento incluida en el resultado.");

    public static IResult PeriodInvalid() => ProblemFactory.Create(
        StatusCodes.Status422UnprocessableEntity, "period-invalid",
        "El período no es válido", "period_invalid",
        errors: new Dictionary<string, string[]>
        {
            ["periodEnd"] = ["Debe ser igual o posterior a periodStart."],
        });
}
