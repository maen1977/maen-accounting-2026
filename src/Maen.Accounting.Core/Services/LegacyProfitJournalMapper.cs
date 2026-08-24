using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class LegacyProfitJournalMapper
{
    public static JournalEntry? Map(ProfitEntry entry, string deviceId = "")
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsDeleted || entry.SalesMinor <= 0 && entry.CostMinor <= 0 && entry.ExpensesMinor <= 0)
        {
            return null;
        }

        var lines = new List<JournalLine>();
        AddPair(
            lines,
            $"legacy-{entry.EntryId}-sales-debit",
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode),
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.SalesRevenueCode),
            entry.SalesMinor,
            "ترحيل مبيعات السجل القديم");
        AddPair(
            lines,
            $"legacy-{entry.EntryId}-cost-debit",
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CostOfSalesCode),
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.InventoryCode),
            entry.CostMinor,
            "ترحيل تكلفة المبيعات من السجل القديم");
        AddPair(
            lines,
            $"legacy-{entry.EntryId}-expense-debit",
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.OperatingExpensesCode),
            DefaultChartOfAccounts.IdForCode(DefaultChartOfAccounts.CashCode),
            entry.ExpensesMinor,
            "ترحيل المصروفات من السجل القديم");

        return new JournalEntry(
            $"legacy-profit-{entry.EntryId}",
            entry.UserId,
            entry.EntryDate,
            $"LEG-{entry.EntryDate:yyyyMMdd}-{entry.EntryId[..Math.Min(8, entry.EntryId.Length)]}",
            string.IsNullOrWhiteSpace(entry.Notes) ? "ترحيل سجل الربح القديم" : entry.Notes,
            lines,
            JournalEntryStatus.Posted,
            entry.EntryId,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            entry.Version,
            string.IsNullOrWhiteSpace(deviceId) ? entry.DeviceId : deviceId);
    }

    private static void AddPair(
        List<JournalLine> lines,
        string debitLineId,
        string debitAccountId,
        string creditAccountId,
        long amountMinor,
        string description)
    {
        if (amountMinor <= 0)
        {
            return;
        }

        lines.Add(new JournalLine(debitLineId, debitAccountId, DebitMinor: amountMinor, Description: description));
        lines.Add(new JournalLine(
            debitLineId.Replace("-debit", "-credit", StringComparison.Ordinal),
            creditAccountId,
            CreditMinor: amountMinor,
            Description: description));
    }
}
