using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

/// <summary>
/// Unit tests for the five new v2.5.0 core engines:
/// MovementSearchEngine, PersonalFinancialPlanCalculator,
/// ObligationScheduleCalculator, CategoryRegistryCalculator,
/// BusinessFinancialSummaryCalculator.
/// </summary>
public sealed class MovementSearchEngineTests
{
    private static ProfitEntry Entry(
        string id = "e1",
        DateOnly? date = null,
        string notes = "نقود للأكل",
        string category = "طعام",
        string counterparty = "سوق",
        string wallet = "main",
        string movementType = "purchase",
        long sales = 0,
        long amount = 1000,
        bool deleted = false) =>
        new(id, "user", date ?? new DateOnly(2026, 8, 15), sales, 0, 0, notes, deleted,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "device",
            amount, movementType, category, wallet, counterparty);

    [Fact]
    public void EmptyKeywordReturnsAllNonDeletedEntries()
    {
        var entries = new[] { Entry(), Entry("e2", deleted: true), Entry("e3") };
        var results = MovementSearchEngine.Search(entries, new MovementSearchQuery());
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void KeywordMatchesNotes()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(notes: "شراء خبز"), Entry(notes: "فاتورة كهرباء") },
            new MovementSearchQuery(Keyword: "خبز"));
        Assert.Single(results);
        Assert.Equal("شراء خبز", results[0].Notes);
    }

    [Fact]
    public void KeywordMatchesCategory()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(category: "مواصلات"), Entry(category: "صحة") },
            new MovementSearchQuery(Keyword: "مواصل"));
        Assert.Single(results);
    }

    [Fact]
    public void KeywordMatchesCounterparty()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(counterparty: "محل أحمد"), Entry(counterparty: "مطعم نادية") },
            new MovementSearchQuery(Keyword: "أحمد"));
        Assert.Single(results);
    }

    [Fact]
    public void KeywordMatchesWallet()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(wallet: "الحساب البنكي"), Entry(wallet: "نقدي") },
            new MovementSearchQuery(Keyword: "بنكي"));
        Assert.Single(results);
    }

    [Fact]
    public void KeywordMatchesMovementType()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(movementType: "salary"), Entry(movementType: "purchase") },
            new MovementSearchQuery(Keyword: "salary"));
        Assert.Single(results);
    }

    [Fact]
    public void KeywordMatchIsCaseInsensitive()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(notes: "راتب الشهر") },
            new MovementSearchQuery(Keyword: "راتب"));
        Assert.Single(results);
    }

    [Fact]
    public void DateRangeFiltersEntries()
    {
        var entries = new[]
        {
            Entry("e1", new DateOnly(2026, 7, 1)),
            Entry("e2", new DateOnly(2026, 8, 10)),
            Entry("e3", new DateOnly(2026, 9, 5)),
        };
        var results = MovementSearchEngine.Search(entries, new MovementSearchQuery(
            FromDate: new DateOnly(2026, 8, 1),
            ToDate: new DateOnly(2026, 8, 31)));
        Assert.Single(results);
        Assert.Equal("e2", results[0].EntryId);
    }

    [Fact]
    public void MovementTypeFilterIsCaseInsensitive()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(movementType: "Purchase") },
            new MovementSearchQuery(MovementTypes: new[] { "purchase" }));
        Assert.Single(results);
    }

    [Fact]
    public void MultipleMovementTypesAllowed()
    {
        var results = MovementSearchEngine.Search(
            new[]
            {
                Entry("e1", movementType: "purchase"),
                Entry("e2", movementType: "salary"),
                Entry("e3", movementType: "transfer"),
            },
            new MovementSearchQuery(MovementTypes: new[] { "purchase", "salary" }));
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void DeletedEntriesNeverMatch()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry(deleted: true) },
            new MovementSearchQuery(Keyword: "نقود"));
        Assert.Empty(results);
    }

    [Fact]
    public void ResultsSortedByDateDescendingThenCreatedAtDescending()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 8, 1), 0, 0, 0, "a", false, now, now, 1, "d"),
            new ProfitEntry("e2", "user", new DateOnly(2026, 8, 15), 0, 0, 0, "b", false, now, now, 1, "d"),
            new ProfitEntry("e3", "user", new DateOnly(2026, 8, 15), 0, 0, 0, "c", false, now.AddMinutes(1), now, 1, "d"),
        };
        var results = MovementSearchEngine.Search(entries, new MovementSearchQuery());
        Assert.Equal("e3", results[0].EntryId);
        Assert.Equal("e2", results[1].EntryId);
        Assert.Equal("e1", results[2].EntryId);
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => MovementSearchEngine.Search(null!, new MovementSearchQuery()));
        Assert.Throws<ArgumentNullException>(() => MovementSearchEngine.Search(Array.Empty<ProfitEntry>(), null!));
    }

    [Fact]
    public void OpenDateRangeWorks()
    {
        var results = MovementSearchEngine.Search(
            new[] { Entry("e1", new DateOnly(2026, 7, 1)), Entry("e2", new DateOnly(2026, 8, 1)) },
            new MovementSearchQuery(FromDate: new DateOnly(2026, 8, 1)));
        Assert.Single(results);
    }
}

public sealed class PersonalFinancialPlanCalculatorTests
{
    private static ProfitEntry Entry(
        string movementType = "purchase",
        long sales = 0,
        long cost = 0,
        long expenses = 0,
        string category = "",
        bool deleted = false) =>
        new("e", "user", new DateOnly(2026, 8, 10), sales, cost, expenses, "n", deleted,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d", 0, movementType, category);

    [Fact]
    public void EmptyPlanWithNoEntriesReturnsZeros()
    {
        var progress = PersonalFinancialPlanCalculator.TrackPlan(
            FinancialPlan.Empty, Array.Empty<ProfitEntry>(), new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.ActualIncomeMinor);
        Assert.Equal(0, progress.ActualSpendingMinor);
        Assert.Equal(0, progress.IncomeUtilizationPercent);
    }

    [Fact]
    public void IncomeEntriesCountAsActualIncome()
    {
        var entries = new[]
        {
            Entry(movementType: "salary", sales: 500000),
            Entry(movementType: "freelance", sales: 200000),
        };
        var plan = new FinancialPlan(1000000, 300000, 100000, Array.Empty<PlanCategoryLimit>());
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(700000, progress.ActualIncomeMinor);
        Assert.Equal(70, progress.IncomeUtilizationPercent);
    }

    [Fact]
    public void SpendingSumsCostPlusExpenses()
    {
        var entries = new[]
        {
            Entry(movementType: "purchase", cost: 30000, expenses: 20000),
            Entry(movementType: "expense", expenses: 10000),
            Entry(movementType: "withdrawal", expenses: 5000),
        };
        var plan = FinancialPlan.Empty with { MonthlySpendingLimitMinor = 100000 };
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(65000, progress.ActualSpendingMinor);
    }

    [Fact]
    public void IncomeEntriesNotCountedAsSpending()
    {
        var entries = new[] { Entry(movementType: "salary", sales: 900000) };
        var plan = FinancialPlan.Empty with { MonthlySpendingLimitMinor = 500000 };
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.ActualSpendingMinor);
    }

    [Fact]
    public void CrossMonthEntriesAreIgnored()
    {
        var entries = new[]
        {
            new ProfitEntry("e1", "user", new DateOnly(2026, 7, 30), 100000, 0, 0, "n", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "d"),
        };
        var progress = PersonalFinancialPlanCalculator.TrackPlan(
            new FinancialPlan(100000, 0, 0, Array.Empty<PlanCategoryLimit>()),
            entries, new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.ActualIncomeMinor);
    }

    [Fact]
    public void DeletedEntriesAreIgnored()
    {
        var entries = new[] { Entry(deleted: true, sales: 100000) };
        var progress = PersonalFinancialPlanCalculator.TrackPlan(
            new FinancialPlan(100000, 0, 0, Array.Empty<PlanCategoryLimit>()),
            entries, new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.ActualIncomeMinor);
    }

    [Fact]
    public void CategoryLimitsTrackSpendingPerCategory()
    {
        var entries = new[]
        {
            Entry(movementType: "purchase", cost: 40000, category: "طعام"),
            Entry(movementType: "purchase", cost: 70000, category: "طعام"),
            Entry(movementType: "purchase", cost: 10000, category: "مواصلات"),
        };
        var plan = new FinancialPlan(0, 0, 0,
        [
            new PlanCategoryLimit("طعام", 100000),
            new PlanCategoryLimit("مواصلات", 50000),
        ]);
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(2, progress.CategoryProgress.Count);
        var food = progress.CategoryProgress.First(item => item.Category == "طعام");
        Assert.Equal(110000, food.SpentMinor);
        Assert.True(food.IsExceeded);
        Assert.InRange(food.UtilizationPercent, 109.99, 110.01);
        var transport = progress.CategoryProgress.First(item => item.Category == "مواصلات");
        Assert.Equal(20, transport.UtilizationPercent);
        Assert.False(transport.IsExceeded);
    }

    [Fact]
    public void CategoryMatchIsCaseInsensitiveAndTrimmed()
    {
        var entries = new[] { Entry(movementType: "purchase", cost: 5000, category: "  طعام ") };
        var plan = new FinancialPlan(0, 0, 0, [new PlanCategoryLimit("طعام", 10000)]);
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Single(progress.CategoryProgress);
        Assert.Equal(5000, progress.CategoryProgress[0].SpentMinor);
    }

    [Fact]
    public void CategoryProgressSortedByUtilizationDescending()
    {
        var entries = new[]
        {
            Entry(movementType: "expense", expenses: 1000, category: "b"),
            Entry(movementType: "expense", expenses: 9000, category: "a"),
        };
        var plan = new FinancialPlan(0, 0, 0,
        [
            new PlanCategoryLimit("a", 10000),
            new PlanCategoryLimit("b", 10000),
        ]);
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal("a", progress.CategoryProgress[0].Category);
    }

    [Fact]
    public void SavingsTargetZeroYieldsZeroProgress()
    {
        var entries = new[] { Entry(movementType: "salary", sales: 500000) };
        var plan = new FinancialPlan(500000, 300000, 0, Array.Empty<PlanCategoryLimit>());
        var progress = PersonalFinancialPlanCalculator.TrackPlan(plan, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.SavingsProgressPercent);
    }

    [Fact]
    public void IncomeZeroYieldsZeroUtilization()
    {
        var entries = new[] { Entry(movementType: "purchase", cost: 100000) };
        var progress = PersonalFinancialPlanCalculator.TrackPlan(
            FinancialPlan.Empty, entries, new DateOnly(2026, 8, 15));
        Assert.Equal(0, progress.IncomeUtilizationPercent);
    }

    [Fact]
    public void IsValidRejectsNegativeValues()
    {
        Assert.False(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(-1, 0, 0, Array.Empty<PlanCategoryLimit>())));
        Assert.False(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(0, -1, 0, Array.Empty<PlanCategoryLimit>())));
        Assert.False(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(0, 0, -1, Array.Empty<PlanCategoryLimit>())));
    }

    [Fact]
    public void IsValidRejectsSavingsAboveIncome()
    {
        Assert.False(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(1000, 0, 1001, Array.Empty<PlanCategoryLimit>())));
    }

    [Fact]
    public void IsValidRejectsNegativeCategoryLimits()
    {
        Assert.False(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(1000, 0, 0,
            [new PlanCategoryLimit("x", -1)])));
    }

    [Fact]
    public void IsValidAcceptsValidPlan()
    {
        Assert.True(PersonalFinancialPlanCalculator.IsValid(new FinancialPlan(1000, 500, 200,
            [new PlanCategoryLimit("x", 100)])));
    }

    [Fact]
    public void IsValidRejectsNull()
    {
        Assert.False(PersonalFinancialPlanCalculator.IsValid(null!));
    }
}

public sealed class ObligationScheduleCalculatorTests
{
    private static Obligation Obligation(
        string cycle = "monthly",
        DateOnly? start = null,
        long amount = 50000,
        bool active = true,
        DateOnly[]? paid = null) =>
        new("o1", "user", "الإيجار", "سكن", amount,
            start ?? new DateOnly(2026, 7, 1), cycle, active, paid ?? Array.Empty<DateOnly>(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    [Fact]
    public void MonthlyObligationGeneratesExpectedOccurrences()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 7, 1)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal(2, events.Count);
        Assert.Equal(new DateOnly(2026, 7, 1), events[0].DueDate);
        Assert.Equal(new DateOnly(2026, 8, 1), events[1].DueDate);
    }

    [Fact]
    public void WeeklyObligationStepsBySevenDays()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(cycle: "weekly", start: new DateOnly(2026, 8, 1)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal(3, events.Count);
        Assert.Equal(new DateOnly(2026, 8, 15), events[^1].DueDate);
    }

    [Fact]
    public void DailyObligationStepsByOneDay()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(cycle: "daily", start: new DateOnly(2026, 8, 13)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public void YearlyObligationStepsByTwelveMonths()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(cycle: "yearly", start: new DateOnly(2025, 8, 1)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal(2, events.Count);
        Assert.Equal(new DateOnly(2026, 8, 1), events[1].DueDate);
    }

    [Fact]
    public void PaidOccurrencesMarkedAsPaid()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 7, 1), paid: [new DateOnly(2026, 7, 3)]) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.All(events.Where(item => item.DueDate.Month == 7), item => Assert.True(item.IsPaid));
        Assert.All(events.Where(item => item.DueDate.Month == 8), item => Assert.False(item.IsPaid));
    }

    [Fact]
    public void MonthlyPaymentMatchesWholeMonthPeriod()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 7, 1), paid: [new DateOnly(2026, 7, 28)]) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.True(events[0].IsPaid);
    }

    [Fact]
    public void YearlyPaymentMatchesWholeYearPeriod()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(cycle: "yearly", start: new DateOnly(2026, 1, 1), paid: [new DateOnly(2026, 12, 1)]) },
            new DateOnly(2026, 12, 31), lookAheadDays: 0);
        Assert.True(events[^1].IsPaid);
    }

    [Fact]
    public void InactiveObligationsAreSkipped()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(active: false) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Empty(events);
    }

    [Fact]
    public void EventsOrderedByDueDateThenId()
    {
        var obligations = new[]
        {
            new Obligation("b", "user", "ب", "", 1000, new DateOnly(2026, 8, 5), "monthly", true, Array.Empty<DateOnly>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new Obligation("a", "user", "أ", "", 2000, new DateOnly(2026, 8, 1), "monthly", true, Array.Empty<DateOnly>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        };
        var events = ObligationScheduleCalculator.UpcomingEvents(obligations, new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal("a", events[0].ObligationId);
    }

    [Fact]
    public void LookAheadDaysLimitsFutureEvents()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 8, 10)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 31);
        Assert.Equal(2, events.Count);
        Assert.Equal(new DateOnly(2026, 8, 10), events[0].DueDate);
        Assert.Equal(new DateOnly(2026, 9, 10), events[1].DueDate);
    }

    [Fact]
    public void MonthlyStartDateOnDayThirtyOneClampsToEndOfMonth()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 7, 31)) },
            new DateOnly(2026, 10, 1), lookAheadDays: 0);
        Assert.Contains(events, item => item.DueDate == new DateOnly(2026, 8, 31));
        Assert.Contains(events, item => item.DueDate == new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void WeeklyCycleMatchesSameWeekPeriod()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(cycle: "weekly", start: new DateOnly(2026, 8, 10), paid: [new DateOnly(2026, 8, 12)]) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.All(events.Where(item => item.DueDate.Day == 10), item => Assert.True(item.IsPaid));
    }

    [Fact]
    public void EventsCarryObligationMetadata()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 8, 1)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Equal("الإيجار", events[0].Title);
        Assert.Equal("سكن", events[0].Category);
        Assert.Equal(50000, events[0].AmountMinor);
        Assert.Equal("monthly", events[0].Cycle);
    }

    [Fact]
    public void NullObligationsThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ObligationScheduleCalculator.UpcomingEvents(null!, new DateOnly(2026, 8, 15)));
    }

    [Fact]
    public void FutureStartDateProducesNoEventsYet()
    {
        var events = ObligationScheduleCalculator.UpcomingEvents(
            new[] { Obligation(start: new DateOnly(2026, 9, 1)) },
            new DateOnly(2026, 8, 15), lookAheadDays: 0);
        Assert.Empty(events);
    }
}

public sealed class CategoryRegistryCalculatorTests
{
    [Fact]
    public void DefaultCategoriesIncludedWithZeroCounts()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            Array.Empty<CategoryEntry>(), new[] { "طعام", "مواصلات" });
        Assert.Equal(2, registry.Count);
        Assert.All(registry, item => Assert.Equal(0, item.EntriesCount));
    }

    [Fact]
    public void NewCategoriesFromEntriesAdded()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[] { new CategoryEntry("كهرباء", new DateOnly(2026, 8, 1)) },
            new[] { "طعام" });
        Assert.Equal(2, registry.Count);
        Assert.Contains(registry, item => item.Category == "كهرباء");
    }

    [Fact]
    public void EntryCountsAccumulatedPerCategory()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[]
            {
                new CategoryEntry("طعام", new DateOnly(2026, 8, 1)),
                new CategoryEntry("طعام", new DateOnly(2026, 8, 10)),
            },
            new[] { "طعام" });
        Assert.Equal(2, registry.First(item => item.Category == "طعام").EntriesCount);
    }

    [Fact]
    public void LastUsedDateTracksMostRecentEntry()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[]
            {
                new CategoryEntry("طعام", new DateOnly(2026, 8, 1)),
                new CategoryEntry("طعام", new DateOnly(2026, 8, 10)),
            },
            new[] { "طعام" });
        Assert.Equal(new DateOnly(2026, 8, 10), registry.First(item => item.Category == "طعام").LastUsedDate);
    }

    [Fact]
    public void SortedByCountDescendingThenName()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[]
            {
                new CategoryEntry("ب", new DateOnly(2026, 8, 1)),
                new CategoryEntry("أ", new DateOnly(2026, 8, 1)),
                new CategoryEntry("أ", new DateOnly(2026, 8, 2)),
            },
            new[] { "ج" });
        Assert.Equal("أ", registry[0].Category);
    }

    [Fact]
    public void DefaultCategoryMatchingIsCaseInsensitive()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[] { new CategoryEntry("طعام", new DateOnly(2026, 8, 1)) },
            new[] { "طعام" });
        Assert.Single(registry);
        Assert.Equal(1, registry[0].EntriesCount);
    }

    [Fact]
    public void DuplicateDefaultsCollapsed()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            Array.Empty<CategoryEntry>(), new[] { "طعام", "طعام" });
        Assert.Single(registry);
    }

    [Fact]
    public void WhitespaceDefaultsIgnored()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            Array.Empty<CategoryEntry>(), new[] { " ", "" });
        Assert.Empty(registry);
    }

    [Fact]
    public void ColorsAssignedFromPaletteCyclically()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            Array.Empty<CategoryEntry>(), Enumerable.Range(0, 12).Select(index => $"cat{index}").ToArray());
        Assert.Equal("#C8A45D", registry.First(item => item.Category == "cat0").ColorHex);
        Assert.Equal("#5B8DB8", registry.First(item => item.Category == "cat1").ColorHex);
        Assert.Equal("#C8A45D", registry.First(item => item.Category == "cat10").ColorHex);
    }

    [Fact]
    public void EmptyCategoryEntriesSkipped()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[] { new CategoryEntry("", new DateOnly(2026, 8, 1)) },
            new[] { "طعام" });
        Assert.Single(registry);
    }

    [Fact]
    public void WhitespaceCategoryEntriesSkipped()
    {
        var registry = CategoryRegistryCalculator.ComputeRegistry(
            new[] { new CategoryEntry("   ", new DateOnly(2026, 8, 1)) },
            new[] { "طعام" });
        Assert.Single(registry);
    }

    [Fact]
    public void NullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CategoryRegistryCalculator.ComputeRegistry(null!, Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() =>
            CategoryRegistryCalculator.ComputeRegistry(Array.Empty<CategoryEntry>(), null!));
    }
}

public sealed class BusinessFinancialSummaryCalculatorTests
{
    private static Contact Contact(
        string id = "c1", string name = "عميل", ContactType type = ContactType.Customer) =>
        new(id, "user", type, name, "", "", "", true,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static Invoice Invoice(
        string id = "i1",
        InvoiceType type = InvoiceType.Sales,
        long total = 100000,
        DateOnly? due = null) =>
        new(id, "user", $"inv-{id}", type, new DateOnly(2026, 8, 1),
            due ?? new DateOnly(2026, 8, 31), "c1",
            [new InvoiceLine("l1", "item", total)], 0,
            InvoiceStatus.Posted, CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static Payment Payment(
        string id = "p1",
        PaymentType type = PaymentType.CustomerReceipt,
        long amount = 50000) =>
        new(id, "user", $"pay-{id}", type, new DateOnly(2026, 8, 10), "c1", amount,
            CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void EmptyDataReturnsEmptySummary()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            Array.Empty<Contact>(), Array.Empty<Invoice>(), Array.Empty<Payment>(),
            DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(0, summary.TotalSalesMinor);
        Assert.Empty(summary.TopContactsByBalance);
    }

    [Fact]
    public void SalesTotalsPostedSalesInvoices()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(total: 100000), Invoice("i2", total: 50000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(150000, summary.TotalSalesMinor);
    }

    [Fact]
    public void PurchasesTotaledSeparately()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact(type: ContactType.Supplier) },
            new[] { Invoice(type: InvoiceType.Purchase, total: 80000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(80000, summary.TotalPurchasesMinor);
        Assert.Equal(0, summary.TotalSalesMinor);
    }

    [Fact]
    public void ReceiptsReduceReceivableNet()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(total: 100000) },
            new[] { Payment(amount: 40000) },
            new DateOnly(2026, 8, 15));
        Assert.Equal(60000, summary.ReceivableNetMinor);
        Assert.Equal(40000, summary.CustomerReceiptsMinor);
    }

    [Fact]
    public void SupplierPaymentsReducePayableNet()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact(type: ContactType.Supplier) },
            new[] { Invoice(type: InvoiceType.Purchase, total: 100000) },
            new[] { Payment(type: PaymentType.SupplierPayment, amount: 30000) },
            new DateOnly(2026, 8, 15));
        Assert.Equal(70000, summary.PayableNetMinor);
        Assert.Equal(30000, summary.SupplierPaymentsMinor);
    }

    [Fact]
    public void NetPositionIsReceivableMinusPayable()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact(), Contact("c2", "مورد", ContactType.Supplier) },
            new[] { Invoice(total: 100000), Invoice("i2", type: InvoiceType.Purchase, total: 20000) },
            new[] { Payment(type: PaymentType.SupplierPayment, amount: 10000) },
            new DateOnly(2026, 8, 15));
        Assert.Equal(100000, summary.ReceivableNetMinor);
        Assert.Equal(10000, summary.PayableNetMinor);
        Assert.Equal(90000, summary.NetPositionMinor);
    }

    [Fact]
    public void OverdueSalesDetectedByDueDate()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(due: new DateOnly(2026, 7, 1), total: 50000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(50000, summary.OverdueSalesMinor);
    }

    [Fact]
    public void OverduePurchasesDetectedByDueDate()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact(type: ContactType.Supplier) },
            new[] { Invoice(type: InvoiceType.Purchase, due: new DateOnly(2026, 7, 1), total: 50000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(50000, summary.OverduePurchasesMinor);
    }

    [Fact]
    public void DueSoonFlaggedWithinWindow()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(due: new DateOnly(2026, 8, 18), total: 50000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(50000, summary.DueSoonSalesMinor);
        Assert.Equal(0, summary.OverdueSalesMinor);
    }

    [Fact]
    public void ContactsCountedOnlyIfActive()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(total: 100000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(1, summary.ActiveContacts);
    }

    [Fact]
    public void PostedInvoicesCounted()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(total: 100000) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(1, summary.PostedInvoices);
    }

    [Fact]
    public void PaidInFullZeroesReceivable()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() },
            new[] { Invoice(total: 100000) },
            new[] { Payment(amount: 100000) },
            new DateOnly(2026, 8, 15));
        Assert.Equal(0, summary.ReceivableNetMinor);
    }

    [Fact]
    public void TaxIncludedInTotals()
    {
        var invoice = new Invoice("i1", "user", "inv-i1", InvoiceType.Sales,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "c1",
            [new InvoiceLine("l1", "item", 100000)], 15000,
            InvoiceStatus.Posted, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact() }, new[] { invoice }, Array.Empty<Payment>(), new DateOnly(2026, 8, 15));
        Assert.Equal(115000, summary.TotalSalesMinor);
        Assert.Equal(115000, summary.ReceivableNetMinor);
    }

    [Fact]
    public void MultipleContactsAggregated()
    {
        var summary = BusinessFinancialSummaryCalculator.Summarize(
            new[] { Contact("c1", "أ"), Contact("c2", "ب") },
            new[] { Invoice(total: 40000), new Invoice("i2", "user", "inv-i2", InvoiceType.Sales, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "c2", [new InvoiceLine("l2", "x", 60000)], 0, InvoiceStatus.Posted, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) },
            Array.Empty<Payment>(),
            new DateOnly(2026, 8, 15));
        Assert.Equal(2, summary.ActiveContacts);
        Assert.Equal(2, summary.TopContactsByBalance.Count);
    }
}
