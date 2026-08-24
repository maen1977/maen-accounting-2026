using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

/// <summary>Tests for the v3.1.0 engine wave (budgets, currencies, duplicate detection, PDF export).</summary>
public sealed class BudgetPlannerTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static ProfitEntry Outflow(DateOnly date, long minor) =>
        new(
            Guid.NewGuid().ToString("N"),
            "user",
            date,
            0,
            minor,
            0,
            "cost",
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            "device",
            minor,
            "expense",
            "food");

    [Fact]
    public void Assess_WithNoSpending_AlertsNone()
    {
        var status = BudgetPlannerCalculator.Assess(100_000, [], Today);
        Assert.Equal(100_000, status.CapMinor);
        Assert.Equal(0, status.SpentMinor);
        Assert.Equal(100_000, status.RemainingMinor);
        Assert.Equal(BudgetAlert.None, status.Alert);
        Assert.Equal(0, status.UsagePercent, 1);
    }

    [Fact]
    public void Assess_OverCap_AlertsExceeded()
    {
        var entries = new[] { Outflow(new DateOnly(2026, 8, 2), 60_000), Outflow(new DateOnly(2026, 8, 10), 50_000) };
        var status = BudgetPlannerCalculator.Assess(100_000, entries, Today);
        Assert.Equal(110_000, status.SpentMinor);
        Assert.True(status.RemainingMinor < 0);
        Assert.Equal(BudgetAlert.Exceeded, status.Alert);
    }

    [Fact]
    public void Assess_AboveEightyPercent_AlertsWarning()
    {
        var entries = new[] { Outflow(new DateOnly(2026, 8, 5), 85_000) };
        var status = BudgetPlannerCalculator.Assess(100_000, entries, Today);
        Assert.Equal(BudgetAlert.Warning, status.Alert);
    }

    [Fact]
    public void Assess_IgnoresOtherMonthsAndDeleted()
    {
        var entries = new[]
        {
            Outflow(new DateOnly(2026, 8, 5), 90_000),
            Outflow(new DateOnly(2026, 7, 20), 500_000),
            Outflow(new DateOnly(2026, 8, 9), 40_000)
                with { IsDeleted = true }
        };
        var status = BudgetPlannerCalculator.Assess(100_000, entries, Today);
        Assert.Equal(90_000, status.SpentMinor);
    }

    [Fact]
    public void Assess_IncomeDoesNotCountAsSpending()
    {
        var entries = new[] { new ProfitEntry("x", "user", new DateOnly(2026, 8, 5), 90_000, 0, 0, "income", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "device") };
        var status = BudgetPlannerCalculator.Assess(100_000, entries, Today);
        Assert.Equal(0, status.SpentMinor);
    }

    [Fact]
    public void ProjectedOverage_ReturnsPositiveDelta()
    {
        var entries = new[] { Outflow(new DateOnly(2026, 8, 1), 95_000) };
        var status = BudgetPlannerCalculator.Assess(100_000, entries, Today);
        Assert.True(status.ProjectedMonthTotalMinor > 0);
        var overage = BudgetPlannerCalculator.ProjectedOverageMinor(status);
        Assert.Equal(Math.Max(0, status.ProjectedMonthTotalMinor - status.CapMinor), overage);
    }

    [Fact]
    public void Assess_ZeroCap_AlertsNone()
    {
        var status = BudgetPlannerCalculator.Assess(0, new[] { Outflow(new DateOnly(2026, 8, 1), 50_000) }, Today);
        Assert.Equal(BudgetAlert.None, status.Alert);
    }
}

public sealed class CurrencyConverterTests
{
    [Fact]
    public void Convert_JodToUsd_AppliesRate()
    {
        var rates = CurrencyConverter.FromSeedProfiles("JOD", CurrencyConverter.SeedProfiles);
        // 1 USD = 0.709 JOD → 709 JOD millifills = 1000 USD millifills
        var converted = CurrencyConverter.Convert(709_000, "JOD", "USD", rates);
        Assert.Equal(1_000_000, converted);
    }

    [Fact]
    public void Convert_ThroughInverse_RoundTrips()
    {
        var rates = CurrencyConverter.FromSeedProfiles("JOD", CurrencyConverter.SeedProfiles);
        var usd = CurrencyConverter.Convert(1_000_000, "JOD", "USD", rates);
        var back = CurrencyConverter.Convert(usd, "USD", "JOD", rates);
        Assert.Equal(1_000_000, back);
    }

    [Fact]
    public void Convert_SameCurrency_ReturnsIdentity()
    {
        var rates = CurrencyConverter.FromSeedProfiles("JOD", CurrencyConverter.SeedProfiles);
        Assert.Equal(50_000, CurrencyConverter.Convert(50_000, "USD", "USD", rates));
    }

    [Fact]
    public void ResolveRate_UnknownPair_ReturnsNull()
    {
        var rates = CurrencyConverter.FromSeedProfiles("JOD", CurrencyConverter.SeedProfiles);
        Assert.Null(CurrencyConverter.ResolveRate("X1Y", "X2Y", rates));
    }

    [Fact]
    public void FromSeedProfiles_UnknownBase_ReturnsEmpty()
    {
        var rates = CurrencyConverter.FromSeedProfiles("ZZZ", CurrencyConverter.SeedProfiles);
        Assert.Empty(rates);
    }

    [Fact]
    public void ResolveProfile_KnownCode_ReturnsProfile()
    {
        var profile = CurrencyConverter.ResolveProfile("usd");
        Assert.NotNull(profile);
        Assert.Equal("USD", profile!.Code);
    }

    [Fact]
    public void ResolveProfile_UnknownCode_ReturnsNull()
    {
        Assert.Null(CurrencyConverter.ResolveProfile("XYZ"));
        Assert.Null(CurrencyConverter.ResolveProfile(""));
    }

    [Fact]
    public void SeedPairKey_CaseInsensitiveAndStable()
    {
        Assert.Equal("JODUSD", CurrencyConverter.SeedPairKey("jod", "USD"));
    }
}

public sealed class DuplicateDetectorTests
{
    private static ProfitEntry Entry(DateOnly date, long minor, string notes = "lunch") =>
        new(
            Guid.NewGuid().ToString("N"),
            "user",
            date,
            0,
            0,
            minor,
            notes,
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            "device",
            minor,
            "expense",
            "food");

    [Fact]
    public void IsLikelyDuplicate_SameAmountWithinWindow_Matches()
    {
        var existing = new[] { Entry(new DateOnly(2026, 8, 15), 5_000) };
        var candidate = Entry(new DateOnly(2026, 8, 16), 5_000);
        Assert.True(DuplicateDetector.IsLikelyDuplicate(candidate, existing));
    }

    [Fact]
    public void IsLikelyDuplicate_BeyondWindow_DoesNotMatch()
    {
        var existing = new[] { Entry(new DateOnly(2026, 8, 10), 5_000) };
        var candidate = Entry(new DateOnly(2026, 8, 16), 5_000);
        Assert.False(DuplicateDetector.IsLikelyDuplicate(candidate, existing));
    }

    [Fact]
    public void IsLikelyDuplicate_DifferentAmount_DoesNotMatch()
    {
        var existing = new[] { Entry(new DateOnly(2026, 8, 15), 5_000) };
        var candidate = Entry(new DateOnly(2026, 8, 16), 7_000);
        Assert.False(DuplicateDetector.IsLikelyDuplicate(candidate, existing));
    }

    [Fact]
    public void IsLikelyDuplicate_DeletedEntriesIgnored()
    {
        var existing = new[] { Entry(new DateOnly(2026, 8, 15), 5_000) with { IsDeleted = true } };
        var candidate = Entry(new DateOnly(2026, 8, 16), 5_000);
        Assert.False(DuplicateDetector.IsLikelyDuplicate(candidate, existing));
    }

    [Fact]
    public void IsLikelyDuplicate_ZeroAmountNeverDuplicate()
    {
        var existing = new[] { Entry(new DateOnly(2026, 8, 15), 0) };
        var candidate = Entry(new DateOnly(2026, 8, 16), 0);
        Assert.False(DuplicateDetector.IsLikelyDuplicate(candidate, existing));
    }

    [Fact]
    public void CountMatches_ReturnsAllMatchesWithinWindow()
    {
        var existing = new[]
        {
            Entry(new DateOnly(2026, 8, 15), 5_000),
            Entry(new DateOnly(2026, 8, 14), 5_000),
            Entry(new DateOnly(2026, 8, 10), 5_000)
        };
        var candidate = Entry(new DateOnly(2026, 8, 16), 5_000);
        Assert.Equal(2, DuplicateDetector.CountMatches(candidate, existing));
    }
}

public sealed class PdfReportExporterTests
{
    [Fact]
    public void BuildReportHtml_EscapesHtmlAndContainsRows()
    {
        var html = PdfReportExporter.BuildReportHtml(
            "<title>",
            "subtitle",
            new (string, string)[] { ("Label", "<b>value</b>") },
            new (string, string, string)[] { ("A", "B", "C"), ("D", "E", "F") });

        Assert.Contains("&lt;title&gt;", html);
        Assert.Contains("&lt;b&gt;value&lt;/b&gt;", html);
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<td>D</td>", html);
        Assert.Contains("dir=\"rtl\"", html);
    }

    [Fact]
    public void BuildReportHtml_EmptyTable_OmitsTable()
    {
        var html = PdfReportExporter.BuildReportHtml("t", "s", Array.Empty<(string, string)>(), Array.Empty<(string, string, string)>());
        Assert.DoesNotContain("<table>", html);
    }

    [Fact]
    public void Renderer_NoRendererInstalled_SaysUnavailable()
    {
        if (!string.IsNullOrEmpty(PdfReportExporter.Renderer))
        {
            Assert.True(PdfReportExporter.IsAvailable);
        }
    }
}

public sealed class RecurringUpcomingTests
{
    private static readonly DateOnly Today = new(2026, 8, 16);

    private static RecurringMovement Movement(DateOnly next, string cycle = "monthly") =>
        new(
            Guid.NewGuid().ToString("N"),
            "user",
            "rent",
            "housing",
            50_000,
            "expense",
            new DateOnly(2026, 7, 1),
            cycle.ToString(),
            next,
            true,
            "");

    [Fact]
    public void PreviewUpcoming_ReturnsFutureOccurrences()
    {
        var rows = new[] { Movement(new DateOnly(2026, 8, 16)), Movement(new DateOnly(2026, 9, 16)) };
        var upcoming = RecurringMovementCalculator.PreviewUpcoming(rows, Today, 1);
        Assert.Equal(2, upcoming.Count);
    }

    [Fact]
    public void PreviewUpcoming_SchedulesNextAfterStaleOccurrences()
    {
        // Movements whose NextOccurrence is in the past advance to their next period:
        // 2026-07-20 monthly → 2026-08-20,  2026-08-01 monthly → 2026-09-01.
        var rows = new[] { Movement(new DateOnly(2026, 7, 20)), Movement(new DateOnly(2026, 8, 1)) };
        var upcoming = RecurringMovementCalculator.PreviewUpcoming(rows, Today, 1);
        Assert.All(upcoming, item => Assert.True(item.Upcoming[0] > Today));
        Assert.Contains(upcoming, item => item.Upcoming[0] == new DateOnly(2026, 8, 20));
        Assert.Contains(upcoming, item => item.Upcoming[0] == new DateOnly(2026, 9, 1));
    }
}
