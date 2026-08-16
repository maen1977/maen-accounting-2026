# v3.1.0 State Notes (for context recovery)

## Status
- v3.0.0 released & pushed: tag v3.0.0, commit 09654f5 on main, release with signed APK/AAB at https://github.com/maen1977/maen-accounting-2026/releases/tag/v3.0.0
- 251 tests passing. Version was 3.0.0 / build 12.
- Now building v3.1.0 per plan at /home/ubuntu/maen-accounting-debug/docs/v310-plan.md
- Plan summary (8 workstreams): A) BudgetPlannerCalculator + BudgetPlans table, B) CurrencyProfile/CurrencyConverter + CurrencyRates + CurrencyCode col on ProfitEntry, C) PDF export (HTML→weasyprint shell), D) extend MovementSearchEngine, E) PreviewUpcoming in RecurringMovementCalculator, F) PIN app-lock via SecureStorage, G) DuplicateDetector warn at save, H) audit recent-mutations UI rows on SettingsPage.

## Data layer patterns (follow exactly)
- Rows: [Table("name")] class with [PrimaryKey] string Id + UserId + Ticks as long + Version int + DeviceId + IsDeleted bool; namespace Maen.Accounting.App.Data; using SQLite.
- UserDatabaseFactory (src/Maen.Accounting.App/Data/UserDatabaseFactory.cs):
  - GetAsync creates tables then calls ApplySchemaMigrationsAsync.
  - migrationActions dict maps int→Func<Task>: [9] = EnsureAttachmentsAndRecurringAsync. ADD [10].
- SchemaMigrationCatalog (src/Maen.Accounting.Core/Services/SchemaMigrationCatalog.cs): record SchemaMigrationDefinition(int Version, string Name); All list v1..v9 ("entry-attachments-and-recurring-movements"). ADD v10.
- Migration 9 funcs: EnsureAttachmentsAndRecurringAsync in UserDatabaseFactory — check it uses ALTER TABLE ... ADD COLUMN for AttachmentBase64 on profit_entries (must do same for CurrencyCode).

## ProfitEntryRow (src/Maen.Accounting.App/Data/ProfitEntryRow.cs)
- FromModel computes IntegrityHash via DataIntegrityService.ComputeProfitEntryHash(EntryId, UserId, Version, Sales, Cost, Expenses, IsDeleted).
- ToModel maps to ProfitEntry record: params order = (EntryId, UserId, EntryDate, Sales, Cost, Expenses, Notes, IsDeleted, CreatedAtUtc, UpdatedAtUtc, Version, DeviceId, AmountMinor, MovementType, Category, Wallet, Counterparty, IntegrityHash, AttachmentBase64).
- ProfitEntry model has optional trailing params: AmountMinor=0, MovementType="other", Category="", Wallet="main", Counterparty="", IntegrityHash="", AttachmentBase64="". Migration 10 will add CurrencyCode = "" optional param; must update FromModel/ToModel + hash function signature if hash covers currency (decide: DO NOT include currency in hash to keep migration simple? Better: add currency to hash params for consistency — check DataIntegrityService first).

## Build commands
- Tests: `dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj --nologo -v q` (exit 0 = pass; grep "Passed|Failed")
- App build: `dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -c Release -p:AndroidSdkDirectory=/home/ubuntu/android-sdk --nologo -v q`
- APK publish build: add `-f net10.0-android`; signed outputs at bin/Release/net10.0-android/publish/com.maen.accounting-Signed.{apk,aab}
- csproj version props: ApplicationDisplayVersion, ApplicationVersion.
- Release publish: `gh release create v3.1.0 --title "..." --notes-file docs/release-notes-3.1.0.md artifacts/maen-accounting-3.1.0-signed.apk artifacts/maen-accounting-3.1.0-signed.aab`

## UI patterns
- XAML pages in src/Maen.Accounting.App/Views/: PlanningPage.xaml (recurring card added in v3.0), ReportsPage.xaml (category analytics card), SettingsPage.xaml (audit card), PersonalEntryPage.xaml (attachment picker).
- Bilingual keys in src/Maen.Accounting.App/UiText.cs: dict ["TNNN"] = (ar, en). Keys T181-T188 already exist for budget! T721/T722/T723 = attachments. T332/T334 = quick buttons.
- Converter: NonNullConverter; InverseBooleanConverter; {local:Tr Key=TNNN} markup extension.
- ViewModel: src/Maen.Accounting.App/ViewModels/MainStateViewModel.cs — central; has budget text mentions (check actual properties; likely HasBudget etc already partial from FinancialPlanRow MonthlySpendingLimitMinor).

## Existing services (src/Maen.Accounting.Core/Services/)
Money.cs (Money.Format), DataIntegrityService, RecurringMovementCalculator (MaxMissedDays=7, DueMovements/AdvanceOccurrence/BuildAmounts), TopCategoryAnalyticsEngine (Analyze/MonthlyTotals), AuditTrailCalculator, MovementSearchEngine (exists), SavingsGoalCalculator, CashForecastCalculator, FinancialHealthCalculator, InputSanitizer, MoneyTests etc.

## DONE so far (v3.1.0 core engines)
- [A] BudgetPlannerCalculator.cs created (BudgetStatus record, BudgetAlert enum, thresholds 80/100, run-rate projection). Uses Money.Format.
- [B] CurrencyConverter.cs created (CurrencyProfile record, CurrencyRate record rational N/D, Convert/ResolveRate/ConvertThroughAnchor/FromSeedProfiles/FormatWithSymbol, Seed profiles JOD base: USD 0.709, EUR 0.78, GBP 0.91, SAR 0.189, AED 0.193, KWD 2.32, EGP 0.014, TRY 0.022, CNY 0.098).
- [G] DuplicateDetector.cs created (IsLikelyDuplicate/CountMatches, window 3 days, matches amount+category+counterparty).
- [C] PdfReportExporter.cs created (BuildReportHtml RTL + ExportAsync via wkhtmltopdf then weasyprint shell; Renderer/IsAvailable props).
- [E] PreviewUpcoming added to RecurringMovementCalculator (before AdvanceOccurrence).
- [D] MovementSearchEngine extended: EntryIdPrefix, HasAttachment, IsRecurringSourced filters on MovementSearchQuery; IsRecurringSourced = MovementType=="recurring".
- ProfitEntry has EffectiveAmountMinor (used by search/duplicate) — check ProfitEntry record signature before touching.

## DONE more (after engines)
- ProfitEntry model: CurrencyCode="" added (last param); ResolvedCurrency(display) helper; NOT in hash.
- ProfitEntryRow: CurrencyCode col added; FromModel/ToModel updated.
- BudgetPlanRow.cs: BudgetPlanRow (UserId, YearMonth int yyyyMM, AmountMinor, IsActive, Ticks/Version/DeviceId/IsDeleted) + CurrencyProfileRow (Code PK, UserId, Symbol, IsDefault) + CurrencyRateRow (PairKey PK "BASEQUOTE", FromCode, ToCode, RateNumerator/Denominator).
- UserDatabaseFactory: CreateTableAsync for the 3 new rows + Migration [10] EnsureMultiCurrencyAndBudgetAsync (ALTER TABLE profit_entries ADD COLUMN CurrencyCode).
- SchemaMigrationCatalog: v10 "multi-currency-and-budget-plans".
- AppLockService.cs in App/Services (SecureStorage keys: maen.app.pin.hash.v1/.salt.v1/.lock.enabled.v1/.idle.minutes.v1; IsValidPin 4-8 digits; SetPinAsync/VerifyPinAsync/HasPinAsync/GetIdleMinutesAsync).
- App.xaml.cs: OnSleep/OnResume → EnforceAppLockAsync; after idle minutes, swaps window.Page = new AppLockPage(unlockCb) which restores MainTabbedPage.
- AppLockPage.xaml + .xaml.cs created (keys T801-T815).
- UiText.cs: T801-T828 added (PIN lock, budget, PDF export, currency, duplicate warning, upcoming movements, filters).
- Translation helper in code-behind: use UiText.Get("Txxx") NOT Local[] (TrExtension is XAML-only markup).
- NOTE: SessionCoordinator used in App ctor; MainTabbedPage exists in Views.

## ViewModel wiring facts (verified)
- MainStateViewModel has MonthlyBudgetInput/_monthlyBudgetInput via Preferences (key maen_personal_monthly_budget_v1), SaveMonthlyBudget(), MonthlyBudgetText/Progress/PercentText/StatusText/StatusColor, RaiseBudgetProperties(). Budget currently per-user (not per month).
- RebuildRecurringAsync (line 1618): builds RecurringMovement models from _planningRepository.GetRecurringAsync; due movements auto-inserted with MovementType income/expense; NOT marked "recurring" sourced (uses "income"/"expense"). ToModel mapping for RecurringMovementRow shown (RecurringId, UserId, Title, Category, AmountMinor, Kind, StartDateTicks, Cycle, NextOccurrenceTicks, IsActive, Notes).
- UpsertRecurringMovementAsync (line 1678): params (title, category, amountMinor, kind, cycle, startDate, notes); then UpsertRecurringAsync + ReloadAsync.
- _planningRepository is PersonalPlanningRepository-like; ReloadAsync clears Entries and rebuilds summaries.
- MainTabbedPage ctor takes (MainStateViewModel state, DashboardPage, AccountingPage, BusinessTabbedPage, EntryPage, ReportsPage, SettingsPage) via DI in MauiProgram.
- SessionCoordinator.ShowCurrentSessionAsync(window) rebuilds main page — used by App OnUnlocked.
- App lock implemented: AppLockPage() default ctor, event Unlocked; App.xaml.cs EnforceAppLockAsync swaps page.
- All builds succeed, 251 tests pass. dotnet binary at /home/ubuntu/.dotnet/dotnet.

## ViewModel wiring progress (verified lines)
- MainStateViewModel (2650+ lines): _planningRepository type = PlanningRepository (src/Maen.Accounting.App/Data/PlanningRepository.cs). _repository = PersonalLedgerRepository.
- Line 89: added private string _displayCurrency="JOD", BudgetStatus? _currentBudgetStatus, bool _possibleDuplicateDetected.
- Lines 136-174: added DisplayCurrency property (Preferences.Default key "maen_display_currency_v1"; calls RaiseCurrencyProperties() & RaiseReportSummary()), CurrentBudgetStatus/HasBudgetStatus/BudgetStatusAlertText(T818/T819/T158)/AlertColor(#C2413A/#A16207/#137A53), BudgetRemainingText, BudgetProjectedText, PossibleDuplicateDetected.
- RaiseCurrencyProperties() NOT yet added — MUST add (raises DisplayCurrency, Budget* props, TopCategoryText?).
- SavePersonalCurrentAsync line ~822 (after edits), SaveCurrentAsync ~867: constructs ProfitEntry(…, AttachmentBase64); entry ctor now needs CurrencyCode last param if set — add CurrencyCode = DisplayCurrency != "JOD"... Simplest: entry currency = string.IsNullOrWhiteSpace(CurrencyInput) ? "" : CurrencyInput — optional CurrencyInput field not yet added. Simpler: use DisplayCurrency only when user sets CurrencyInput; keep empty default.
- Duplicate detection: in SavePersonalCurrentAsync before SaveCurrentAsync: call DuplicateDetector.IsLikelyDuplicate(entry, existing entries, window 3d) → PossibleDuplicateDetected=true; show T824 as warning (StatusMessage). DuplicateDetector signature: IsLikelyDuplicate(ProfitEntry candidate, IEnumerable<ProfitEntry> existing, …). VERIFY signature in DuplicateDetector.cs.
- Budget plan: add method SetMonthlyBudgetAsync(yearMonth, amountMinor) using BudgetPlanRow in PlanningRepository (add repo methods GetBudgetPlansAsync/UpsertBudgetPlanAsync).
- Recurring rows: auto-inserted entries should set MovementType="recurring"? Currently "income"/"expense" — decided leave as is (MovementType filter IsRecurringSourced uses "recurring" — but search filter added; entries created by recurring use "income"/"expense". Update RebuildRecurringAsync to use "recurring" MovementType? Keep "income"/"expense" for ledger consistency; instead the filter value should be checked. DECISION: set MovementType="recurring" for auto-inserted, so duplicates can identify + audit. UPDATE entry creation in RebuildRecurringAsync: kind.Equals("income") ? "recurring"... Actually safer to keep "income"/"expense" and change filter to check IsRecurringSourced via MovementType in {"income","expense"} for entries whose amount>0? Too ambiguous. DECISION: use MovementType="recurring" for auto-created entries.
- RaiseBudgetProperties at line ~1534; RaiseReportSummary line 1611; RaisePlanningProperties ~1544; RaiseAnnualReportProperties ~1593; RaiseSummaries ~1505 (after edits).

## Wiring COMPLETED (Phase 9 done)
- PlanningRepository: added GetBudgetPlansAsync + UpsertBudgetPlanAsync (deactivates prior same-month plans).
- CurrencyConverter: added ResolveProfile(code); SeedProfiles JOD=1.0, USD 0.709, EUR 0.78, GBP 0.91, SAR 0.189, AED 0.193, KWD 2.32, EGP 0.014, TRY 0.022, CNY 0.098 (value per JOD base unit).
- MainStateViewModel: CurrencyInput field+property, DisplayCurrencyOptions (JOD,SAR,USD,EUR,KWD,AED,EGP,IQD,SYP,GBP), DisplayCurrency property (Preferences maen_display_currency_v1), BudgetStatus wiring (CurrentBudgetStatus/AlertText T818-T819-T158/Color/Remaining/Projected), PossibleDuplicateDetected, ResolveEntryCurrency(), SaveMonthlyBudgetAsync (BudgetPlanRow yearMonth=yyyyMM), RebuildBudgetAsync called in ReloadAsync, SaveMonthlyBudget() delegates to async, ResetEditor clears CurrencyInput, SaveCurrentAsync: entryCurrency param + DuplicateDetector.IsLikelyDuplicate → T824 status.
- BudgetStatus record fields: CapMinor, SpentMinor, RemainingMinor, UsagePercent, ProjectedMonthTotalMinor, DailyRunRateMinor, DaysRemaining, Alert. BudgetPlannerCalculator.Assess(cap, outflows, asOf).
- Migration 10 (multi-currency-and-budget-plans) registered in catalog; tables BudgetPlanRow/CurrencyProfileRow/CurrencyRateRow created in UserDatabaseFactory.
- ProfitEntry/CurrencyProfile/CurrencyRate records exist in Core; ProfitEntry ctor last param CurrencyCode=""; ResolvedCurrency(displayCurrency).
- Build succeeds; 251 tests pass.
- uiText keys T801-T828 exist (lock/budget/pdf/currency/duplicate/upcoming/filters). T824=duplicate warning; T818=exceeded, T819=warning, T158=on track.

## TODO next
1. Data layer: CurrencyProfileRow + CurrencyRateRow + BudgetPlanRow in Data/; Migration 10 EnsureMultiCurrencyAndBudgetsAsync in UserDatabaseFactory + SchemaMigrationCatalog entry.
2. ProfitEntry model + row: add CurrencyCode col (default ""); check hash decision (exclude currency from hash — keep existing entries valid).
3. Security: AppLockService using SecureStorage (hash SHA256+salt), PinHash stored; also IdleTracker via App lifecycle in App.cs.
4. ViewModel wiring: BudgetStatus property (compute in MainStateViewModel from BudgetPlanRow/FinancialPlanRow), CurrencyDisplay props, Duplicate warning at save, Pdf export command.
5. UI: PlanningPage budget card (T181-T188 exist), SettingsPage currency+PDF+PIN cards, ReportsPage search filters.
6. Tests V310EngineTests.cs (~50), version bump 3.1.0/build 13, build, commit, tag v3.1.0, gh release.

## Notes for PDF export
- weasyprint may or may not be installed in sandbox (check `weasyprint --version`). Alternative: SkiaSharp text drawing. Prefer checking weasyprint availability; else generate plain text PDF via manual %PDF generation or use QuestPDF (check nuget availability offline).
- CSV exporters unchanged.

## Verified signatures (for V310EngineTests)
- RecurringMovement record defined in RecurringMovementCalculator.cs:107 (namespace Maen.Accounting.Core.Services). Constructor order: (RecurringId, UserId, Title, Category, AmountMinor, Kind, StartDate, Cycle, NextOccurrence, IsActive, Notes). Cycle = string (not enum) — "monthly"/"weekly"/etc.
- BudgetStatus (BudgetPlannerCalculator.cs:71): CapMinor, SpentMinor, RemainingMinor, UsagePercent, ProjectedMonthTotalMinor, DailyRunRateMinor, DaysRemaining, BudgetAlert. Has UsageText/SpentText/CapText/RemainingText/ProjectedText.
- BudgetAlert enum: None, Warning, Exceeded.
- PreviewUpcoming(movements, asOf, count): returns IReadOnlyList<(RecurringMovement, DateOnly[])>. Signature: (IEnumerable<RecurringMovement> movements, DateOnly asOf, int count) — NOT lookAheadDays!
- DuplicateDetector.CountMatches exists (line 59). Signature verify before compile.
- CurrencyConverter.FromSeedProfiles(baseCurrency, SeedProfiles); ResolveRate, ResolveProfile, SeedPairKey. SeedProfiles: JOD 1.0, USD 0.709, EUR 0.78, GBP 0.91, SAR 0.189, AED 0.193, KWD 2.32, EGP 0.014, TRY 0.022, CNY 0.098.
- V310EngineTests.cs written (needs fixes: PreviewUpcoming uses count not lookAheadDays; RecurringMovement uses string cycle; imports).
