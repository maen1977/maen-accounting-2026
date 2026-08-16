using SQLite;

namespace Maen.Accounting.App.Data;

/// <summary>Persisted monthly spending cap for personal accounting (minor units).</summary>
[Table("budget_plans")]
public sealed class BudgetPlanRow
{
    [PrimaryKey]
    public string PlanId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>YearMonth in yyyyMM form, e.g. 202608.</summary>
    public int YearMonth { get; set; }

    public long AmountMinor { get; set; }

    public bool IsActive { get; set; } = true;

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}

/// <summary>User-defined currency profile (code, display symbol).</summary>
[Table("currency_profiles")]
public sealed class CurrencyProfileRow
{
    [PrimaryKey]
    public string Code { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}

/// <summary>Offline exchange rate stored as an exact rational pair per user.</summary>
[Table("currency_rates")]
public sealed class CurrencyRateRow
{
    [PrimaryKey]
    public string PairKey { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>Pair key in BASEQUOTE upper-case form, e.g. JODUSD.</summary>
    public string FromCode { get; set; } = string.Empty;

    public string ToCode { get; set; } = string.Empty;

    public long RateNumerator { get; set; }

    public long RateDenominator { get; set; }

    public long UpdatedAtUtcTicks { get; set; }

    public int Version { get; set; } = 1;

    public string DeviceId { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
}
