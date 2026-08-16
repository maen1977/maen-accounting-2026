using SQLite;

namespace Maen.Accounting.App.Data;

[Table("financial_plans")]
public sealed class FinancialPlanRow
{
    [PrimaryKey]
    public string PlanId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public long MonthlyIncomeMinor { get; set; }

    public long MonthlySpendingLimitMinor { get; set; }

    public long MonthlySavingsTargetMinor { get; set; }

    public string CategoryLimitsJson { get; set; } = "[]";

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}

[Table("obligations")]
public sealed class ObligationRow
{
    [PrimaryKey]
    public string ObligationId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    public long StartDateTicks { get; set; }

    public string Cycle { get; set; } = "monthly";

    public bool IsActive { get; set; } = true;

    public string PaidOccurrencesJson { get; set; } = "[]";

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}

[Table("recurring_movements")]
public sealed class RecurringMovementRow
{
    [PrimaryKey]
    public string RecurringId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    public string Kind { get; set; } = "expense";

    public long StartDateTicks { get; set; }

    public string Cycle { get; set; } = "monthly";

    public long NextOccurrenceTicks { get; set; }

    public bool IsActive { get; set; } = true;

    public string Notes { get; set; } = string.Empty;

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}

[Table("deposits")]
public sealed class DepositRow
{
    [PrimaryKey]
    public string DepositId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public long AmountMinor { get; set; }

    public long DepositDateTicks { get; set; }

    public string Kind { get; set; } = "deposit";

    public bool IsWithdrawn { get; set; }

    public string Notes { get; set; } = string.Empty;

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}
