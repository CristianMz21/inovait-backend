namespace Inovait.Core.Features.Enrollments;

public static class AgeCalculator
{
    public static int Calculate(DateOnly birthDate, DateOnly asOfDate)
    {
        var age = asOfDate.Year - birthDate.Year;
        if (birthDate > asOfDate.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
