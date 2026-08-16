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
    string IntegrityHash = "")
{
    public long GrossProfitMinor => checked(SalesMinor - CostMinor);
    public long NetProfitMinor => checked(GrossProfitMinor - ExpensesMinor);

    public long EffectiveAmountMinor => AmountMinor > 0
        ? AmountMinor
        : checked(SalesMinor + CostMinor + ExpensesMinor);

    public bool IsIncome => SalesMinor > 0;
    public bool IsOutflow => checked(CostMinor + ExpensesMinor) > 0;

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
