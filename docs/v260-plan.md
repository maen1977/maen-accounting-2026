# v2.6.0 — Maximum Development Plan (post-v2.5.0 Grand Release)

Current state after v2.5.0 (commit a872a47): 118 tests pass, Release builds, app has personal tabs (Dashboard, Entry, Planning, Reports, Settings) and business tabs (Dashboard, Contacts, Invoices, Payments). Reports page has monthly day-by-day bar view + business totals. CSV export exists in Core (ReportCsvExporter) but personal export needs a UI.

## Gap analysis
Missing compared to a world-class free accounting app:
1. No annual view (12-month trend chart/list) for personal income/spending/net — only month view.
2. No category breakdown report (pie-like list) for personal spending.
3. No personal ledger export UI (CSV exists but unreachable for personal users).
4. No savings tracking over time (plan vs actual across months).
5. No recurring income automation (auto-create salary entry monthly reminder).
6. Business: no aging report detail (only bucket totals), no invoice PDF/print, no expense reports per supplier.
7. No quick stats on splash/dashboard yearly comparison (this year vs last year).

## v2.6.0 scope (max value, feasible in one run)
### New Core engines (pure, testable):
- `PersonalAnnualReportCalculator`: yearly month-by-month series (income, spending, net per month), totals, YoY comparison against previous year.
- `PersonalCategoryReportCalculator`: per-category monthly spending with ranking, totals, percent of spending.
- `SavingsTrendCalculator`: for each month in last N months: income, spending, saved (income-spending) vs plan targets → trend list.
### New App features:
- `AnnualReportPage` (personal): 12-month bars + month selector + YoY compare card + CSV export button.
- Personal CSV export action (file write + DisplayAlert with path).
- `CategoryReportCard` on PersonalDashboardPage (top 5 categories by spending this month with colored dots + percent).
- Recurring salary reminder: obligation-based hint shown on dashboard when no salary entry this month yet (uses existing obligations engine).
- Business: aging detail view on Dashboard financial position card (expandable overdue list).
### Localization: new T-keys (start T463).
### Tests: 25+ new tests for the 3 new engines.
### Release: csproj 2.6.0 (versionCode 8), docs/release-notes-2.6.0.md, commit+push, gh release v2.6.0 with signed apk+aab.

## Build/test commands
- PATH=/home/ubuntu/.dotnet:/home/ubuntu/android-sdk/cmdline-tools/latest/bin:/home/ubuntu/android-sdk/platform-tools:$PATH
- JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64, ANDROID_HOME=ANDROID_SDK_ROOT=/home/ubuntu/android-sdk
- cd /home/ubuntu/maen-accounting-debug
- test: dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj
- publish: dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release
- signed artifacts: src/Maen.Accounting.App/bin/Release/net10.0-android/publish/com.maen.accounting-Signed.{apk,aab}
- gh release: gh release create v2.6.0 -R maen1977/maen-accounting-2026 --title ... --notes-file docs/release-notes-2.6.0.md apk aab

## Key facts
- ObservableViewModel base: Maen.Accounting.App.Infrastructure.ObservableObject (SetProperty, OnPropertyChanged). NO record inheritance.
- Money.TryParse(text,out minor), Money.Format(minor). Ticks are DateTime ticks (new DateTime(ticks, Unspecified)).
- AuthSessionStore.LoadAsync() -> AuthSession?(UserId...). DeviceIdentityService registered singleton.
- PlanningRepository: UpsertPlanAsync(userId, FinancialPlan), GetPlansAsync(userId) -> FinancialPlanRow[]. ParseCategoryLimits(json).
- Core Services namespace: Maen.Accounting.Core.Services (PersonalFinancialPlanCalculator, ObligationScheduleCalculator, CategoryRegistryCalculator, MovementSearchEngine, BusinessFinancialSummaryCalculator, PersonalLedgerSummaryCalculator, ProfitCalculator, ReportCsvExporter, TrialBalanceCalculator, DefaultChartOfAccounts, SchemaMigrationCatalog (v5)).
- PersonalMovementTypes: salary, freelance, sale, other_income, purchase, expense, withdrawal, transfer, debt_payment.
- PersonalLedgerSummary record: Overall/CurrentMonth/CurrentYear (FinancialSummary), BankBalanceMinor, CurrentMonthIncomeMinor/Salary/Freelance/Purchases/Withdrawals/DebtPayments/AvailableBalanceMinor, CurrentMonthCategorySpending.
- UiText: static Get(key). Language: UiText.Language == AppLanguage.English. Keys T395-T462 exist; next free ~T463.
- ReportCsvExporter.Build(entries, headers) requires exactly 10 headers; CSV format RFC-style with Escape.
- MainStateViewModel: ReloadAsync flow; RaiseSummaries; entries = IReadOnlyList<ProfitEntry>; session via SessionCoordinator.CurrentUserId (verify) or AuthSessionStore.
- Tabbed pages: PersonalTabbedPage (dashboard/entry/planning/reports/settings), BusinessTabbedPage (contacts/invoices/payments — dashboard is separate, opened from MainTabbedPage).
- XAML styles: CardBorder, SectionTitle, MutedLabel, PrimaryButton, BrandHeaderView. Converters in Views/Converters.cs + XAML namespace local=Maen.Accounting.App.
- MauiProgram registers pages via DI; views take ctor DI (PlanningPage pattern: PlanningRepository, DeviceIdentityService, AuthSessionStore).
