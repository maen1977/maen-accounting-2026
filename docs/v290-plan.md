# v2.9.0 Plan — Maximum Upgrade Wave

Current state: v2.8.0 released (195 tests). 11,648 lines CS + 1,780 lines XAML.

## Gaps identified (what a world-class free accounting app still lacks)

1. **No savings goals tracking** — app tracks a monthly savings target but has no named savings goals (e.g. "Buy car", "Travel") with target amounts, deadlines, and progress. → SavingsGoalCalculator engine + UI card + Goals page.
2. **No financial health score / KPI dashboard** — individual dashboard shows movements but no health indicators (savings rate, debt ratio, runway, category budget health). → FinancialHealthCalculator engine + health card.
3. **No month-over-month comparison for business** — BusinessMonthlyReportCalculator reports months but no YoY/MoM comparison insights. → BusinessComparisonCalculator.
4. **No cash forecast** — recurring obligations exist (ObligationScheduleCalculator) but no "next 90 days cash forecast" combining plan + obligations. → CashForecastCalculator.
5. **Accounting page (journal entries) UI is minimal** — AccountingPage.xaml has only 99 lines; trial balance shown but entry creation is single debit/credit only. → Multi-line journal entry editing.
6. **No quick totals/insights on reports page (personal)** — fine-tune.
7. **Performance**: dashboard reloads recompute everything per keystroke; movement filter works; check for sequential awaits that can be parallelized in ReloadAsync.
8. **Security**: audit trail — record who changed what (device id already stored). Add an audit log page? Keep scope: add "last sync integrity" summary to SettingsPage.

## Decision: v2.9.0 scope (balanced, high-impact, shippable)

Phase 2 engines (Core):
- A) `SavingsGoalCalculator`: goals = list of (id,title,targetMinor,deadline,category); progress from actual category spending + deposits; engine returns SavingsGoalProgress[] + OnTrack flags. Local storage: reuse Settings/preferences JSON (keep local-first; goals stored via PersonalRepository preferences or new columns? Simpler: store goals as JSON in preferences via existing pattern? Check AccountingRepository for preferences storage. If too complex, store in SQLite new table? Prefer new SQLite table with migration 8: savings_goals (goal_id,user_id,title,target_minor,deadline,saved_minor). Compute progress engine-side.
- B) `FinancialHealthCalculator`: savings rate %, fixed costs ratio, emergency runway months, spending volatility; returns score 0-100 + flags.
- C) `CashForecastCalculator`: forecast next 90 days from FinancialPlan + obligation schedule + average daily spending.
- D) `BusinessComparisonCalculator`: compares monthly slices YoY and MoM with delta amounts and percent.

Phase 3 UI:
- Savings Goals page (PersonalTabbedPage new tab) with add/delete, progress bars, on-track badges.
- Financial health card on PersonalDashboardPage (score ring + flags).
- Forecast card on PlanningPage (next 90 days).
- Business reports comparison section (YoY toggle).
- ~20-30 new T keys T551-T580.

Phase 4 perf/security:
- Parallelize independent loads in MainStateViewModel ReloadAsync (invoice reconciliation + aging + quality + business report independent → Task.WhenAll).
- Audit summary on SettingsPage: count of records, hash-verified count, last sync time.

Phase 5: tests (~30), version 2.9.0 Build 11, release notes, GitHub release.
