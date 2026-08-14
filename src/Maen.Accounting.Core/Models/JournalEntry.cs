namespace Maen.Accounting.Core.Models;

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Voided = 3
}

public sealed record JournalLine(
    string LineId,
    string AccountId,
    long DebitMinor = 0,
    long CreditMinor = 0,
    string Description = "")
{
    public bool HasDebit => DebitMinor > 0;

    public bool HasCredit => CreditMinor > 0;

    public long AmountMinor => checked(DebitMinor + CreditMinor);
}

public sealed record JournalEntry(
    string EntryId,
    string UserId,
    DateOnly EntryDate,
    string EntryNumber,
    string Description,
    IReadOnlyList<JournalLine> Lines,
    JournalEntryStatus Status = JournalEntryStatus.Draft,
    string Reference = "",
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "")
{
    public long TotalDebitMinor => Lines.Sum(static line => line.DebitMinor);

    public long TotalCreditMinor => Lines.Sum(static line => line.CreditMinor);

    public bool IsBalanced => TotalDebitMinor == TotalCreditMinor;

    public bool IsPosted => Status == JournalEntryStatus.Posted;
}
