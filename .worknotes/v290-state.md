# v2.9.0 Progress Notes (working state) — updated

## Current state (as of latest build)
- Full app BUILD SUCCEEDED (0 errors) after fixing:
  - SavingsGoalsPage.xaml.cs: added `using Maen.Accounting.App.Infrastructure;`
  - MainStateViewModel UpsertGoalAsync: replaced `row with {...}` (SavingsGoalRow is a class, not record) with explicit copy constructor
  - SavingsGoalRow.FromModel: removed `goal.Version` (doesn't exist in SavingsGoal record)
- UiText keys T600-T656 added (goals/health/forecast/comparison bilingual)
- PersonalDashboardPage.xaml: Financial Health card added (T640-649, HealthScoreText/HealthLevelText/SavingsRateText/FixedCostRatioText/RunwayText/HealthFlags/HasHealthFlags)
- PlanningPage.xaml: Forecast card (T630-636) + PlanningViewModel has ForecastNextMonthText/Color, ForecastWorstText, ForecastSlices (ObservableCollection<CashForecastSliceItem>)
- BusinessReportsPage.xaml: comparisons card (T650-656) + BusinessReportsViewModel Comparison*Text/Color props + MonthlySliceViewModel gets MonthlyComparison (DeltaText/DeltaColor/DeltaArrow/YoYText/YoYColor)
- MainStateViewModel: RebuildGoalsAndForecastAsync via Task.WhenAll inside RebuildPlanningAsync; HasHealthFlags, ForecastHasSlices, ForecastSlices added

## Model constructors (for tests)
- ProfitEntry(EntryId, UserId, EntryDate, SalesMinor, CostMinor, ExpensesMinor, Notes, IsDeleted, CreatedAtUtc, UpdatedAtUtc, Version, DeviceId, AmountMinor=0, MovementType="other", Category="", Wallet="main", Counterparty="", IntegrityHash="")
- SavingsGoal(GoalId, UserId, Title, Category, TargetMinor, AlreadySavedMinor, StartDate, Deadline, IsClosed=false)
- FinancialPlan(MonthlyIncomeMinor, MonthlySpendingLimitMinor, MonthlySavingsTargetMinor, CategoryLimits) — from PersonalFinancialPlanCalculator.cs
- Obligation(ObligationId, UserId, Title, Category, AmountMinor, StartDate, Cycle, IsActive, PaidOccurrences) — ObligationScheduleCalculator.cs
- FinancialHealthCalculator.Assess(entries, recentPlans)
- SavingsGoalCalculator.Track(goals, entries, asOf)
- CashForecastCalculator.Forecast(entries, obligations, asOf)
- BusinessComparisonCalculator.Compare(months) — MonthlyComparison has SalesDeltaMinor/Percent, NetCashDeltaMinor/Percent, SalesYoYDeltaMinor/Percent, etc. (no Purchases delta)

## Test fixes status (as of now)
- V290EngineTests.cs written (4 classes: V290SavingsGoalTests x10, V290FinancialHealthTests x10, V290CashForecastTests x10, V290BusinessComparisonTests x10) = 40 tests
- Fixed: slice count >= 3 check, Runway < 1 (expenses 200k vs income 100k), LowSavingsRate with 3 months income 300/spend 95, DeletedEntries uses Zip comparison, YoY NetCashYoYDeltaMinor=0 (same receipts), PaidObligations test: ObligationScheduleCalculator.UpcomingEvents treats PaidOccurrences differently — actual 75000 (3 monthly occurrences 25k each in window; PaidOccurrences only excludes that exact occurrence but future occurrences still count → changed to expect 75_000? NO — still pending fix)
- Last remaining fail: Forecast_PaidObligationsAreExcluded (Expected 0, Actual 75000)
- Total so far: 230 passing / 5 failed (4 fixed just now in last edit; need rerun)

## Remaining steps
1. V290EngineTests.cs: ~30 tests (goals tracking, health assessment, forecast, comparison, Money/edge cases)
2. Run all tests → expect 195+30 = 225 passing
3. Bump csproj version to 2.9.0 build 11 (check Maen.Accounting.App.csproj: Version/Build)
4. Write docs/release-notes-2.9.0.md
5. Commit+push; gh release v2.9.0 with signed APK+AAB (build: dotnet publish -c Release -p:AndroidPackageFormat=apk -f net10.0-android)
6. Update docs/v290-progress.md status

## Patterns
- Tests use xUnit; V280EngineTests.cs is the model.
- Build cmd: dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -p:AndroidSdkDirectory=/home/ubuntu/android-sdk
- Test cmd: dotnet test tests/Maen.Accounting.Core.Tests (from src dir: dotnet test Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj)
