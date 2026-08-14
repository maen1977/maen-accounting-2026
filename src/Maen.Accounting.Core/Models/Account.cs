namespace Maen.Accounting.Core.Models;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public sealed record Account(
    string AccountId,
    string UserId,
    string Code,
    string Name,
    AccountType Type,
    string? ParentAccountId = null,
    bool IsSystem = false,
    bool IsActive = true,
    int SortOrder = 0,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? UpdatedAtUtc = null,
    int Version = 1,
    string DeviceId = "")
{
    public bool HasParent => !string.IsNullOrWhiteSpace(ParentAccountId);

    public bool IsDebitNature => Type is AccountType.Asset or AccountType.Expense;
}
