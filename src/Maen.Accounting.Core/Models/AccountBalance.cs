namespace Maen.Accounting.Core.Models;

public sealed record AccountBalance(
    Account Account,
    long TotalDebitMinor,
    long TotalCreditMinor)
{
    public long NetMinor => checked(TotalDebitMinor - TotalCreditMinor);

    public long DebitBalanceMinor => Math.Max(NetMinor, 0);

    public long CreditBalanceMinor => Math.Max(-NetMinor, 0);
}

public sealed record TrialBalance(
    IReadOnlyList<AccountBalance> Accounts,
    long TotalDebitMinor,
    long TotalCreditMinor)
{
    public bool IsBalanced => TotalDebitMinor == TotalCreditMinor;
}
