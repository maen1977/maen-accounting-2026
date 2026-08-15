using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class DefaultChartOfAccounts
{
    public const string CashCode = "1000";
    public const string BankCode = "1010";
    public const string ReceivablesCode = "1100";
    public const string InventoryCode = "1200";
    public const string PayablesCode = "2000";
    public const string TaxPayableCode = "2100";
    public const string CapitalCode = "3000";
    public const string RetainedEarningsCode = "3100";
    public const string SalesRevenueCode = "4000";
    public const string CostOfSalesCode = "5000";
    public const string OperatingExpensesCode = "6000";
    public const string MigrationSuspenseCode = "9999";

    public static IReadOnlyList<Account> Create(string userId, DateTimeOffset? now = null, string deviceId = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var timestamp = now ?? DateTimeOffset.UtcNow;
        return
        [
            CreateAccount(userId, CashCode, "الصندوق", AccountType.Asset, 10, timestamp, deviceId),
            CreateAccount(userId, BankCode, "البنك", AccountType.Asset, 15, timestamp, deviceId),
            CreateAccount(userId, ReceivablesCode, "العملاء", AccountType.Asset, 20, timestamp, deviceId),
            CreateAccount(userId, InventoryCode, "المخزون", AccountType.Asset, 30, timestamp, deviceId),
            CreateAccount(userId, PayablesCode, "الموردون", AccountType.Liability, 40, timestamp, deviceId),
            CreateAccount(userId, TaxPayableCode, "الضرائب المستحقة", AccountType.Liability, 45, timestamp, deviceId),
            CreateAccount(userId, CapitalCode, "رأس المال", AccountType.Equity, 50, timestamp, deviceId),
            CreateAccount(userId, RetainedEarningsCode, "الأرباح المحتجزة", AccountType.Equity, 60, timestamp, deviceId),
            CreateAccount(userId, SalesRevenueCode, "إيرادات المبيعات", AccountType.Revenue, 70, timestamp, deviceId),
            CreateAccount(userId, CostOfSalesCode, "تكلفة المبيعات", AccountType.Expense, 80, timestamp, deviceId),
            CreateAccount(userId, OperatingExpensesCode, "المصروفات التشغيلية", AccountType.Expense, 90, timestamp, deviceId),
            CreateAccount(userId, MigrationSuspenseCode, "حساب التسوية", AccountType.Equity, 999, timestamp, deviceId)
        ];
    }

    public static string IdForCode(string code) => $"system-account-{code}";

    private static Account CreateAccount(
        string userId,
        string code,
        string name,
        AccountType type,
        int sortOrder,
        DateTimeOffset timestamp,
        string deviceId) => new(
            IdForCode(code),
            userId,
            code,
            name,
            type,
            IsSystem: true,
            SortOrder: sortOrder,
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp,
            DeviceId: deviceId);
}
