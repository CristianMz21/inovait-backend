using Inovait.Core.Features.Enrollments;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
[Trait("Evidence", "UT-AGE")]
public sealed class AgeCalculatorTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(13)]
    public void Calculate_IncrementsAgeOnBirthdayAtEachBoundary(int boundaryAge)
    {
        var birthDate = new DateOnly(2010, 6, 15);
        var birthday = birthDate.AddYears(boundaryAge);

        Assert.Equal(boundaryAge - 1, AgeCalculator.Calculate(birthDate, birthday.AddDays(-1)));
        Assert.Equal(boundaryAge, AgeCalculator.Calculate(birthDate, birthday));
    }

    [Fact]
    public void Calculate_ReturnsZeroOnBirthDateItself()
    {
        var birthDate = new DateOnly(2020, 7, 11);

        Assert.Equal(0, AgeCalculator.Calculate(birthDate, birthDate));
    }

    [Fact]
    public void Calculate_KeepsAgeBetweenBirthdays()
    {
        var birthDate = new DateOnly(2015, 4, 20);

        Assert.Equal(8, AgeCalculator.Calculate(birthDate, new DateOnly(2023, 4, 21)));
        Assert.Equal(8, AgeCalculator.Calculate(birthDate, new DateOnly(2024, 4, 19)));
    }

    [Theory]
    [InlineData(2028, 2, 28, 11)]
    [InlineData(2028, 2, 29, 12)]
    [InlineData(2027, 2, 28, 10)]
    [InlineData(2027, 3, 1, 11)]
    public void Calculate_TreatsLeapDayBirthdayAsMarchFirstInNonLeapYears(
        int asOfYear, int asOfMonth, int asOfDay, int expectedAge)
    {
        var birthDate = new DateOnly(2016, 2, 29);
        var asOfDate = new DateOnly(asOfYear, asOfMonth, asOfDay);

        Assert.Equal(expectedAge, AgeCalculator.Calculate(birthDate, asOfDate));
    }

    [Theory]
    [InlineData(2026, 7, 12, -1)]
    [InlineData(2027, 7, 11, -1)]
    [InlineData(2027, 7, 12, -2)]
    [InlineData(2030, 5, 10, -4)]
    public void Calculate_ReturnsNegativeYearsWhenAsOfDatePrecedesBirthDate(
        int birthYear, int birthMonth, int birthDay, int expectedAge)
    {
        var birthDate = new DateOnly(birthYear, birthMonth, birthDay);
        var asOfDate = new DateOnly(2026, 7, 11);

        Assert.Equal(expectedAge, AgeCalculator.Calculate(birthDate, asOfDate));
    }
}
