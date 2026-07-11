namespace Inovait.Api.Errors;

internal static class CatalogProblems
{
    public static IResult SchoolNotFound() => ProblemFactory.Create(
        StatusCodes.Status404NotFound, "resource-not-found",
        "No se encontró la escuela", "school_not_found", "La escuela indicada no existe.");

    public static IResult GradeNotFound() => ProblemFactory.Create(
        StatusCodes.Status404NotFound, "resource-not-found",
        "No se encontró el grado", "grade_not_found", "El grado indicado no existe.");

    public static IResult AcademicYearNotFound() => ProblemFactory.Create(
        StatusCodes.Status404NotFound, "resource-not-found",
        "No se encontró el año académico", "academic_year_not_found", "El año académico indicado no existe.");

    public static IResult TeacherNotFound() => ProblemFactory.Create(
        StatusCodes.Status404NotFound, "resource-not-found",
        "No se encontró el docente", "teacher_not_found", "El docente indicado no existe.");
}
