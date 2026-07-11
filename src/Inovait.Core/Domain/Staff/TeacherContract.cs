using Inovait.Core.Domain.Common;

namespace Inovait.Core.Domain.Staff;

public enum TeacherContractStatus
{
    Confirmed,
    Cancelled,
}

public enum EffectiveContractStatus
{
    Upcoming,
    Effective,
    Expired,
    Cancelled,
}

public sealed class TeacherContract(
    int teacherPersonId,
    int schoolId,
    DateOnly startDate,
    DateOnly? endDate) : AuditableEntity
{
    public int Id { get; private set; }
    public int TeacherPersonId { get; private set; } = teacherPersonId;
    public int SchoolId { get; private set; } = schoolId;
    public DateOnly StartDate { get; private set; } = startDate;
    public DateOnly? EndDate { get; private set; } = ValidateEndDate(startDate, endDate);
    public TeacherContractStatus Status { get; private set; } = TeacherContractStatus.Confirmed;
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateOnly? CancellationEffectiveDate { get; private set; }

    public void Cancel(DateTime cancelledAtUtc, string reason, DateOnly effectiveDate)
    {
        if (Status != TeacherContractStatus.Confirmed)
            throw new InvalidOperationException("Only a confirmed contract can be cancelled.");
        if (cancelledAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Cancellation timestamp must be UTC.", nameof(cancelledAtUtc));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason is required.", nameof(reason));
        if (effectiveDate < StartDate || effectiveDate > (EndDate ?? DateOnly.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(effectiveDate));
        Status = TeacherContractStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancellationReason = reason;
        CancellationEffectiveDate = effectiveDate;
    }

    public EffectiveContractStatus GetEffectiveStatus(DateOnly date)
    {
        if (date < StartDate)
            return EffectiveContractStatus.Upcoming;
        if (Status == TeacherContractStatus.Cancelled
            && CancellationEffectiveDate is { } effectiveDate && date >= effectiveDate)
            return EffectiveContractStatus.Cancelled;
        return date > (EndDate ?? DateOnly.MaxValue)
            ? EffectiveContractStatus.Expired
            : EffectiveContractStatus.Effective;
    }
    public bool Overlaps(DateOnly otherStartDate, DateOnly? otherEndDate)
    {
        ValidateEndDate(otherStartDate, otherEndDate);
        return StartDate <= (otherEndDate ?? DateOnly.MaxValue)
            && otherStartDate <= (EndDate ?? DateOnly.MaxValue);
    }

    private static DateOnly? ValidateEndDate(DateOnly startDate, DateOnly? endDate) =>
        endDate < startDate ? throw new ArgumentOutOfRangeException(nameof(endDate)) : endDate;
}
