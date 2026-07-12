namespace Inovait.Api.Errors;

internal static class StudentHistoryProblems
{
    public static IResult StudentNotFound() => ProblemFactory.Create(
        StatusCodes.Status404NotFound, "student-not-found",
        "No se encontró el estudiante", "student_not_found", "El estudiante indicado no existe.");
}
