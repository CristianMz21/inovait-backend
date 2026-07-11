using Inovait.Core.Features.Enrollments;

namespace Inovait.Api.Errors;

internal static class EnrollmentProblems
{
    private const string ConflictType = "enrollment-conflict";
    private const string ConflictTitle = "La inscripción entra en conflicto con la historia existente";

    public static IResult Map(EnrollmentError error) => error switch
    {
        EnrollmentError.InvalidBirthDate => ProblemFactory.Create(
            StatusCodes.Status422UnprocessableEntity, "invalid-birth-date",
            "La fecha de nacimiento no es válida", "invalid_birth_date",
            "La fecha de nacimiento no puede ser posterior a la fecha actual."),
        EnrollmentError.DocumentTypeNotFound => ProblemFactory.Create(
            StatusCodes.Status404NotFound, "resource-not-found",
            "No se encontró el tipo de documento", "document_type_not_found",
            "El tipo de documento indicado no existe."),
        EnrollmentError.SchoolNotFound => CatalogProblems.SchoolNotFound(),
        EnrollmentError.AcademicYearNotFound => CatalogProblems.AcademicYearNotFound(),
        EnrollmentError.GradeNotFound => CatalogProblems.GradeNotFound(),
        EnrollmentError.ClassGroupNotFound => ProblemFactory.Create(
            StatusCodes.Status404NotFound, "resource-not-found",
            "No se encontró el grupo", "class_group_not_found", "El grupo indicado no existe."),
        EnrollmentError.AcademicContextMismatch => ProblemFactory.Create(
            StatusCodes.Status422UnprocessableEntity, "academic-context-invalid",
            "El contexto académico no es válido", "academic_context_invalid",
            "El grupo no pertenece a la escuela, grado y año indicados."),
        EnrollmentError.IdentityConflict => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "enrollment_conflict",
            "La identidad suministrada difiere de la identidad ya registrada para ese documento."),
        EnrollmentError.AnnualEnrollmentConflict => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "enrollment_conflict",
            "El estudiante ya tiene una inscripción para el año académico indicado."),
        EnrollmentError.ConcurrencyConflict => ProblemFactory.Create(
            StatusCodes.Status409Conflict, ConflictType, ConflictTitle, "enrollment_conflict",
            "No fue posible confirmar la inscripción por una actualización concurrente; reintente la operación."),
        _ => ProblemFactory.Create(StatusCodes.Status500InternalServerError, "internal-error", "Error interno", "internal_error"),
    };
}
