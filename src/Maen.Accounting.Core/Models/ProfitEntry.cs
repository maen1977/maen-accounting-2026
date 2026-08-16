using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Models;

public sealed record ProfitEntry(
    string EntryId,
    string UserId,
    DateOnly EntryDate,
    long SalesMinor,
    long CostMinor,
    long ExpensesMinor,
    string Notes,
    bool IsDeleted,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Version,
    string DeviceId,
    long AmountMinor = 0,
    string MovementType = "other",
    string Category = "",
    string Wallet = "main",
    string Counterparty = "",
    string IntegrityHash = "",
    string AttachmentBase64 = "",
    string CurrencyCode = "")
{
    public bool HasAttachment => !string.IsNullOrEmpty(AttachmentBase64);

    /// <summary>Resolved currency code falling back to the user's display currency when unspecified.</summary>
    public string ResolvedCurrency(string displayCurrency) =>
        !string.IsNullOrEmpty(CurrencyCode) ? CurrencyCode : displayCurrency;

    public long GrossProfitMinor => checked(SalesMinor - CostMinor);
    public long NetProfitMinor => checked(GrossProfitMinor - ExpensesMinor);

    public long EffectiveAmountMinor => AmountMinor > 0
        ? AmountMinor
        : checked(SalesMinor + CostMinor + ExpensesMinor);

    public bool IsIncome => SalesMinor > 0;
    public bool IsOutflow => checked(CostMinor + ExpensesMinor) > 0;

    public ProfitEntry WithAttachment(string base64) => this with { AttachmentBase64 = base64, Version = checked(Version + 1), UpdatedAtUtc = DateTimeOffset.UtcNow };

    public ProfitEntry MarkDeleted(DateTimeOffset nowUtc, string deviceId) => this with
    {
        IsDeleted = true,
        UpdatedAtUtc = nowUtc,
        Version = checked(Version + 1),
        DeviceId = deviceId,
        IntegrityHash = DataIntegrityService.ComputeProfitEntryHash(
            EntryId, UserId, checked(Version + 1), SalesMinor, CostMinor, ExpensesMinor, isDeleted: true)
    };
}
