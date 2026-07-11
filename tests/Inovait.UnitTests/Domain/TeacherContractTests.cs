using Inovait.Core.Domain.Staff;

namespace Inovait.UnitTests.Domain;

[Trait("Priority", "P0")]
public sealed class TeacherContractTests
{
    [Fact]
    [Trait("Evidence", "UT-CONTRACT-CANCELLATION")]
    public void NewContract_IsConfirmedWithoutCancellationData()
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), null);

        Assert.Equal(TeacherContractStatus.Confirmed, contract.Status);
        Assert.Null(contract.CancelledAtUtc);
        Assert.Null(contract.CancellationReason);
        Assert.Null(contract.CancellationEffectiveDate);
    }

    [Fact]
    [Trait("Evidence", "UT-CONTRACT-CANCELLATION")]
    public void Cancel_TransitionsConfirmedContractWithCompleteData()
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), new(2026, 11, 30));
        var cancelledAt = new DateTime(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

        contract.Cancel(cancelledAt, " Position closed ", new(2026, 7, 31));

        Assert.Equal((TeacherContractStatus.Cancelled, cancelledAt, " Position closed ", new DateOnly(2026, 7, 31)),
            (contract.Status, contract.CancelledAtUtc, contract.CancellationReason, contract.CancellationEffectiveDate));
    }

    [Fact]
    [Trait("Evidence", "UT-CONTRACT-CANCELLATION")]
    public void ContractAndCancellation_RejectInvalidDatesAndReason()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TeacherContract(10, 20, new(2026, 3, 2), new(2026, 3, 1)));
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), new(2026, 11, 30));
        Assert.Throws<ArgumentException>(() => contract.Cancel(UtcNow(), " \t\n ", new(2026, 7, 31)));
        Assert.Throws<ArgumentOutOfRangeException>(() => contract.Cancel(UtcNow(), "Valid", new(2026, 12, 1)));
        Assert.Throws<ArgumentException>(() => contract.Cancel(DateTime.SpecifyKind(UtcNow(), DateTimeKind.Local), "Valid", new(2026, 7, 31)));
    }

    [Fact]
    [Trait("Evidence", "UT-CONTRACT-CANCELLATION")]
    public void Cancel_RejectsASecondTransition()
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), null);
        contract.Cancel(UtcNow(), "First", new(2026, 7, 31));

        Assert.Throws<InvalidOperationException>(() => contract.Cancel(UtcNow(), "Second", new(2026, 8, 1)));
    }

    [Theory]
    [Trait("Evidence", "UT-CONTRACT-STATUS")]
    [InlineData(false, 2, 28, EffectiveContractStatus.Upcoming)]
    [InlineData(false, 3, 1, EffectiveContractStatus.Effective)]
    [InlineData(false, 11, 30, EffectiveContractStatus.Effective)]
    [InlineData(false, 12, 1, EffectiveContractStatus.Expired)]
    [InlineData(true, 2, 28, EffectiveContractStatus.Upcoming)]
    [InlineData(true, 3, 1, EffectiveContractStatus.Effective)]
    [InlineData(true, 7, 30, EffectiveContractStatus.Effective)]
    [InlineData(true, 7, 31, EffectiveContractStatus.Cancelled)]
    [InlineData(true, 12, 1, EffectiveContractStatus.Cancelled)]
    public void EffectiveStatus_UsesDatesAndEffectiveCancellation(
        bool cancelled, int month, int day, EffectiveContractStatus expected)
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), new(2026, 11, 30));
        if (cancelled)
            contract.Cancel(UtcNow(), "Closed", new(2026, 7, 31));
        Assert.Equal(expected, contract.GetEffectiveStatus(new(2026, month, day)));
    }

    [Theory]
    [Trait("Evidence", "UT-CONTRACT-OVERLAP")]
    [InlineData("2025-12-01", "2026-02-28", false)]
    [InlineData("2026-02-01", "2026-03-01", true)]
    [InlineData("2026-04-01", "2026-05-01", true)]
    [InlineData("2026-11-30", "2026-12-15", true)]
    [InlineData("2026-12-01", "2026-12-15", false)]
    public void Overlaps_UsesInclusiveIntervalIntersection(string start, string end, bool expected)
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), new(2026, 11, 30));

        Assert.Equal(expected, contract.Overlaps(DateOnly.Parse(start), DateOnly.Parse(end)));
    }

    [Fact]
    [Trait("Evidence", "UT-CONTRACT-OVERLAP")]
    public void Overlaps_TreatsNullEndAsOpen()
    {
        var contract = new TeacherContract(10, 20, new(2026, 3, 1), null);

        Assert.True(contract.Overlaps(new(2099, 1, 1), null));
    }

    private static DateTime UtcNow() => new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);
}
