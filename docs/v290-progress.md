# v2.9.0 Progress — Maximum Upgrade Wave

## Context
- v2.8.0 released (195 tests). User asked to upgrade to maximum extent. Plan: docs/v290-plan.md
- Build command: `dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -p:AndroidSdkDirectory=/home/ubuntu/android-sdk`
- Release: `dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -c Release -p:AndroidSdkDirectory=/home/ubuntu/android-sdk -p:AndroidPackageFormat=apk -f net10.0-android` → bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk/.aab
- gh release create: `gh release create v2.9.0 --notes-file docs/release-notes-2.9.0.md bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk bin/Release/net10.0-android/publish/com.maen.accounting-Signed.aab`

## Phase 2 progress (engines done):
1. `SavingsGoalCalculator.cs` (Core/Services): static Track(goals, entries, asOf) → SavingsGoalProgress[] (Goal,SavedMinor,IsComplete,ProgressPercent,IsOnTrack,RemainingMinor,DaysRemaining + SavedText/TargetText/RemainingText). SavingsGoal record: GoalId,UserId,Title,Category,TargetMinor,AlreadySavedMinor,StartDate,Deadline,IsClosed. Deposits minus spending in category + AlreadySavedMinor. IsOnTrack approx: remainingMinor/daysRemaining <= avg daily need.
2. `FinancialHealthCalculator.cs` (Core/Services): static Assess(entries, recentPlans) → FinancialHealthSummary(Score 0-100, SavingsRatePercent, FixedCostRatioPercent, RunwayMonths, Flags[] + ScoreLevel Excellent>=80/Good>=60/Fair>=40/Weak>=20/Critical). Flags: LowSavingsRate(<10%), HighFixedCosts(>60%), ShortRunway(<1 month), SpendingWithoutIncome. Uses last 3 months from entries; fixed categories = plan CategoryLimits with limit>0.
3. `CashForecastCalculator.cs` (Core/Services): static Forecast(entries, obligations, asOf) → CashForecast(Slices, WorstSlice, HasForecast). Obligation record (ObligationId,UserId,Title,Cycle,AmountMinor,StartDate,Category,IsPaid). Uses ObligationScheduleCalculator.ScheduledInPeriod(obligations, from, to, asOf) → ScheduledOccurrence — VERIFY METHOD SIGNATURE BEFORE BUILD! CashForecastSlice: Year,Month,ProjectedIncomeMinor,ProjectedSpendingMinor,ObligationsMinor,NetMinor + MonthLabel/ProjectedIncomeText/ProjectSpendingText/ObligationsText/NetText/NetColor(green #1B7A43 / red #B23B3B). Daily avg from last 90 days of entries before asOf.

## TODO next:
- Verify ObligationScheduleCalculator.ScheduledInPeriod signature (params?) — if doesn't exist, adapt to available API. Then dotnet build Core.
- Create BusinessComparisonCalculator (YoY/MoM of BusinessMonthlyReportCalculator slices). Check BusinessMonthlyReportCalculator output record name (MonthlySlice? report.Slices?) — was MonthlySalesMinor etc.
- Migration 8: savings_goals SQLite table (goal_id, user_id, title, category, target_minor, saved_minor, start_date, deadline, is_closed). Add to UserDatabaseFactory.CreateTableAsync + SchemaMigrationCatalog. Rows: SavingsGoalRow (like FinancialPlanRow) with FromModel/ToModel. PlanningRepository methods: GetGoalsAsync/UpsertGoalAsync.
- UI: SavingsGoalsPage.xaml (.cs) new personal tab (add title/category/target/deadline inputs; list with progress bars; delete). Wire to MainStateViewModel (Goals list, SaveGoalAsync, DeleteGoalAsync). FinancialHealth card on PersonalDashboardPage (score text + flags list T551-T570 keys). Forecast card on PlanningPage (next 90 days slices table). BusinessComparison section on BusinessReportsPage (MoM delta column — add DeltaText/IsUp props to report slice view model).
- Perf: parallelize independent ReloadAsync computations in MainStateViewModel via Task.WhenAll where they exist.
- Security: SettingsPage audit summary (record counts + integrity verified count + last sync time) — optional.
- T-keys T551-T580 in UiText.cs (goals/health/forecast/comparison bilingual).
- ~30 tests in V290EngineTests.cs; bump csproj 2.9.0/Build 11; release notes docs/release-notes-2.9.0.md; commit+push; gh release.

## FINAL STATUS (completed)
- All TODOs done: Migration 8 applied, SavingsGoalsPage new tab, health card on PersonalDashboardPage, forecast card on PlanningPage, MoM/YoY on BusinessReportsPage, Task.WhenAll parallelization, security integrated.
- 235 passing tests (40 new V290 tests).
- Version 2.9.0 Build 11. Committed 5ea9aa1, tagged v2.9.0, pushed to GitHub.

## Patterns reminders
- MVVM with ObservableObject from Maen.Accounting.App.Infrastructure; records CANNOT inherit ObservableObject.
- UiText: static Get("Tnnn"), Format("Tnnn", arg). UiTextKeys (Core, english strings) for CSV exports.
- Money.Format(long minor). DateOnly DayNumber arithmetic.
- Existing page pattern: XAML BrandHeaderView(Eyebrow/Title/Subtitle/Status/HasStatus) + Border CardBorder + SectionTitle/ListItemBorder/MetricCaption/MutedLabel styles.
- Tr markup: {local:Tr Key=T451}. PersonalTabbedPage.cs holds personal tabs; BusinessTabbedPage.cs business tabs. MauiProgram registers pages+viewmodels.
- MainStateViewModel: ReloadAsync recompute points; properties Raise via SetProperty.
