using Inovait.Api.Contracts;

namespace Inovait.Api.Errors;

internal static class RequestValidation
{
    public static IResult? ValidateCreateEnrollment(CreateEnrollmentRequest request)
    {
        var validator = new RequestValidator();
        if (request.Student is null)
        {
            validator.Require("student", false, "El campo es obligatorio.");
        }
        else
        {
            var student = request.Student;
            validator
                .Require("student.documentType", IsWithinLength(student.DocumentType, 1, 20),
                    "El campo debe tener entre 1 y 20 caracteres.")
                .Require("student.documentNumber", IsWithinLength(student.DocumentNumber, 1, 32),
                    "El campo debe tener entre 1 y 32 caracteres.")
                .Require("student.firstNames", IsWithinLength(student.FirstNames, 1, 120),
                    "El campo debe tener entre 1 y 120 caracteres.")
                .Require("student.lastNames", IsWithinLength(student.LastNames, 1, 120),
                    "El campo debe tener entre 1 y 120 caracteres.")
                .Require("student.birthDate", student.BirthDate is not null, "El campo es obligatorio.");
        }

        validator
            .RequirePositiveId("schoolId", request.SchoolId)
            .RequirePositiveId("academicYearId", request.AcademicYearId)
            .RequirePositiveId("gradeId", request.GradeId)
            .RequirePositiveId("classGroupId", request.ClassGroupId);

        return validator.HasErrors ? validator.ToProblem() : null;
    }

    public static IResult? ValidateCreateTeacherContracts(CreateTeacherContractsRequest request)
    {
        var validator = new RequestValidator()
            .Require("schoolIds", request.SchoolIds is { Count: > 0 }, "Debe contener al menos un elemento.")
            .Require("schoolIds", request.SchoolIds is null || request.SchoolIds.All(id => id >= 1),
                "Los identificadores deben ser mayores o iguales a 1.")
            .Require("startDate", request.StartDate is not null, "El campo es obligatorio.");

        return validator.HasErrors ? validator.ToProblem() : null;
    }

    private static bool IsWithinLength(string? value, int minLength, int maxLength) =>
        value is not null && value.Length >= minLength && value.Length <= maxLength;
}
