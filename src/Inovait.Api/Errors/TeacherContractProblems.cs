using Inovait.Core.Features.TeacherContracts;

namespace Inovait.Api.Errors;

internal static class TeacherContractProblems
{
    private const string ConflictType = "teacher-contract-conflict";
    private const string ConflictTitle = "La solicitud contractual entra en conflicto";

    public static IResult Map(TeacherContractError error) => error switch
    {
        TeacherContractError.InvalidDateRange => ProblemFactory.Create(
            StatusCodes.Status422UnprocessableEntity, "invalid-date-range",
            "El rango de fechas no es válido", "invalid_date_range",
            "La fecha de fin no puede ser anterior a la fecha de inicio."),
        TeacherContractError.NoSchoolsSelected => ProblemFactory.Create(
            StatusCodes.Status400BadRequest, "invalid-request",
            "La solicitud no es válida", "no_schools_selected", "Debe indicar al menos una escuela."),
        TeacherContractError.DuplicateSchool => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "teacher_contract_conflict",
            "La solicitud repite una escuela.",
            errors: new Dictionary<string, string[]>
            {
                ["schoolIds"] = ["No puede contener identificadores repetidos."],
            }),
        TeacherContractError.TeacherNotFound => CatalogProblems.TeacherNotFound(),
        TeacherContractError.SchoolNotFound => CatalogProblems.SchoolNotFound(),
        TeacherContractError.OverlapConflict => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "teacher_contract_conflict",
            "La solicitud superpone un contrato existente."),
        TeacherContractError.ConcurrencyConflict => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "teacher_contract_conflict",
            "No fue posible confirmar el contrato por una actualización concurrente; reintente la operación."),
        _ => ProblemFactory.Create(StatusCodes.Status500InternalServerError, "internal-error", "Error interno", "internal_error"),
    };
}
