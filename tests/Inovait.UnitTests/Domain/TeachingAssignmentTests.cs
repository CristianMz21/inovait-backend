using Inovait.Core.Domain.Academics;
using Inovait.Core.Features.TeachingAssignments;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P1")]
public sealed class TeachingAssignmentTests
{
    [Fact]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    public void Constructor_RejectsEndDateBeforeStartDate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TeachingAssignment(1, 2, 3, new(2026, 3, 2), new(2026, 3, 1)));
    }

    [Fact]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    public void Constructor_AllowsOpenEndedAssignment()
    {
        var assignment = new TeachingAssignment(1, 2, 3, new(2026, 3, 1), null);

        Assert.Null(assignment.EndDate);
    }

    [Fact]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    public void SchoolsMatch_ComparesContractAndGroupSchool()
    {
        var contract = new TeacherContractSnapshot(1, new(2026, 1, 1), null, null);

        Assert.True(TeachingAssignmentPeriodPolicy.SchoolsMatch(
            contract, new ClassGroupSnapshot(1, new(2026, 1, 1), new(2026, 12, 31))));
        Assert.False(TeachingAssignmentPeriodPolicy.SchoolsMatch(
            contract, new ClassGroupSnapshot(2, new(2026, 1, 1), new(2026, 12, 31))));
    }

    [Theory]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    [InlineData("2026-03-01", "2026-06-30", true)]
    [InlineData("2025-12-01", "2026-06-30", false)]
    [InlineData("2026-03-01", "2027-01-15", false)]
    [InlineData("2026-02-01", "2026-06-30", false)]
    public void IsPeriodContained_ChecksYearAndContractBounds(string start, string end, bool expected)
    {
        var contract = new TeacherContractSnapshot(1, new(2026, 3, 1), new(2026, 11, 30), null);
        var group = new ClassGroupSnapshot(1, new(2026, 1, 1), new(2026, 12, 31));

        Assert.Equal(expected, TeachingAssignmentPeriodPolicy.IsPeriodContained(
            DateOnly.Parse(start), DateOnly.Parse(end), contract, group));
    }

    [Fact]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    public void IsPeriodContained_NullEndDateIsBoundedByAcademicYearEnd()
    {
        var openContract = new TeacherContractSnapshot(1, new(2026, 1, 1), null, null);
        var group = new ClassGroupSnapshot(1, new(2026, 1, 1), new(2026, 12, 31));
        Assert.True(TeachingAssignmentPeriodPolicy.IsPeriodContained(new(2026, 3, 1), null, openContract, group));

        var shortContract = new TeacherContractSnapshot(1, new(2026, 1, 1), new(2026, 6, 30), null);
        Assert.False(TeachingAssignmentPeriodPolicy.IsPeriodContained(new(2026, 3, 1), null, shortContract, group));
    }

    [Fact]
    [Trait("Evidence", "UT-ASSIGNMENT")]
    public void IsPeriodContained_RejectsPeriodPastCancellationEffectiveDate()
    {
        var cancelledContract = new TeacherContractSnapshot(1, new(2026, 1, 1), new(2026, 12, 31), new(2026, 6, 30));
        var group = new ClassGroupSnapshot(1, new(2026, 1, 1), new(2026, 12, 31));

        Assert.True(TeachingAssignmentPeriodPolicy.IsPeriodContained(new(2026, 3, 1), new(2026, 6, 30), cancelledContract, group));
        Assert.False(TeachingAssignmentPeriodPolicy.IsPeriodContained(new(2026, 3, 1), new(2026, 7, 1), cancelledContract, group));
    }
}
