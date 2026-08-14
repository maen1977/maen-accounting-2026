using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

public static class TrialBalanceCalculator
{
    public static TrialBalance Build(
        IEnumerable<Account> accounts,
        IEnumerable<JournalEntry> entries,
        DateOnly? fromDate = null,
        DateOnly? toDate = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(entries);

        var accountList = accounts.ToArray();
        var balances = accountList.ToDictionary(
            static account => account.AccountId,
            static account => new MutableBalance(account));

        foreach (var entry in entries)
        {
            if (entry.Status != JournalEntryStatus.Posted ||
                fromDate is not null && entry.EntryDate < fromDate.Value ||
                toDate is not null && entry.EntryDate > toDate.Value)
            {
                continue;
            }

            foreach (var line in entry.Lines)
            {
                if (!balances.TryGetValue(line.AccountId, out var balance))
                {
                    continue;
                }

                balance.TotalDebitMinor = checked(balance.TotalDebitMinor + line.DebitMinor);
                balance.TotalCreditMinor = checked(balance.TotalCreditMinor + line.CreditMinor);
            }
        }

        var result = balances.Values
            .OrderBy(static item => item.Account.SortOrder)
            .ThenBy(static item => item.Account.Code, StringComparer.Ordinal)
            .Select(static item => new AccountBalance(
                item.Account,
                item.TotalDebitMinor,
                item.TotalCreditMinor))
            .ToArray();

        return new TrialBalance(
            result,
            result.Sum(static item => item.TotalDebitMinor),
            result.Sum(static item => item.TotalCreditMinor));
    }

    private sealed class MutableBalance(Account account)
    {
        public Account Account { get; } = account;

        public long TotalDebitMinor { get; set; }

        public long TotalCreditMinor { get; set; }
    }
}
