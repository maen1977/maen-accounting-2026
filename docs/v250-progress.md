# v2.5.0 Grand Release — Progress State

## Phase status
- Phase 10 (analysis): DONE — plan at docs/grand-release-plan.md
- Phase 11 (core engines): IN PROGRESS

## Core services created (in src/Maen.Accounting.Core/Services/)
1. `MovementSearchEngine.cs` — keyword/date/type search over ProfitEntry. Query record: MovementSearchQuery(Keyword, FromDate, ToDate, MovementTypes).
2. `PersonalFinancialPlanCalculator.cs` — plan tracking with PersonalPlanProgress (IncomeUtilizationPercent, SavingsProgressPercent, CategoryProgress). Records: FinancialPlan, PlanCategoryLimit, PlanCategoryProgress. Uses IsValid().
3. `ObligationScheduleCalculator.cs` — recurring obligations with Obligation record (StartDate, Cycle daily/weekly/monthly/yearly, PaidOccurrences DateOnly list), returns ObligationEvent list. Uses IsValid().
4. `BusinessFinancialSummaryCalculator.cs` — Summarize(contacts, invoices, payments, asOfDate) -> BusinessFinancialSummary with receivable/payable net, due buckets (DueSoon/Overdue for sales & purchases), NetPosition, TopContactsByBalance, ContactBalance.
5. `CategoryRegistryCalculator.cs` — ComputeRegistry(entries CategoryEntry, defaults) -> CategoryRegistryItem (Category, ColorHex, EntriesCount, LastUsedDate).

## Schema migration 5
- Catalog: added `new(5, "personal-plans-obligations-deposits")`
- UserDatabaseFactory: CreateTableAsync for FinancialPlanRow, ObligationRow, DepositRow + EnsurePlansObligationsDepositsAsync (3 indexes)
- New file: src/Maen.Accounting.App/Data/FinancialPlanRow.cs (rows: financial_plans, obligations, deposits; all with UserId, IsDeleted, Version, DeviceId, UpdatedAtUtcTicks)
- JSON blobs: CategoryLimitsJson, PaidOccurrencesJson

## SchemaMigrationCatalog current list
1 personal-movement-columns, 2 business-payment-account-code, 3 ledger-and-query-indexes, 4 business-entity-sync-indexes, 5 personal-plans-obligations-deposits

## Next steps (phase 11 remaining)
1. Sync: add plans+obligations to personal Firestore sync collections (FirestoreSyncService) — collections: plans, obligations under users/{uid}
2. MainStateViewModel: hook new calculators, save plan/obligations via repositories (create repositories in Data/ or simple insert via connection)
3. Phase 12 UX: SmartDashboard card, obligation ticker, search page, command palette (quick actions), category management page, business position card, empty states
4. UiText new keys: start at T395
5. Phase 13: tests (SearchEngine, Plan, Obligation, BusinessSummary, Registry) — target 60+; migration catalog test update for v5; build Debug+Release
6. Phase 14: csproj bump to 2.5.0 + version 7, docs/release-notes-2.5.0.md, commit, push, gh release v2.5.0 with Signed apk/aab from bin/Release/net10.0-android/publish/

## Key commands (in sandbox session default)
- export PATH=/home/ubuntu/.dotnet:/home/ubuntu/android-sdk/cmdline-tools/latest/bin:/home/ubuntu/android-sdk/platform-tools:$PATH; JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64; ANDROID_HOME=/home/ubuntu/android-sdk; ANDROID_SDK_ROOT=/home/ubuntu/android-sdk
- cd /home/ubuntu/maen-accounting-debug
- tests: dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj --no-restore (currently 47 passing)
- build: dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Debug --no-restore
- publish: dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release --no-restore
- github release create: gh release create v2.5.0 -R maen1977/maen-accounting-2026 --title '...' --notes-file docs/release-notes-2.5.0.md apk aab
- current git tip: 207f539 (v2.4.0), branch main

## Business sync collections (already in v2.4.0)
- contacts, invoices, payments under users/{uid}/... ; service: BusinessFirestoreSyncService; merge engine: SyncDocumentMergeEngine (BuildPlan with LocalWins/RemoteWins/MergedItems/ItemsToPush)

## Personal firestore sync
- Service: FirestoreSyncService, collection users/{uid}/personalEntries, SyncResult has LocalWins/RemoteWins + SyncStatus

## Language
Working language: Arabic for user messages; English for code/docs.

## Phase 11 progress (continued)

### Done (new files since last update)
- `src/Maen.Accounting.App/Services/PersonalFirestoreSyncService.cs` — syncs plans/obligations/deposits to Firestore collections: personalPlans, personalObligations, personalDeposits (payload field = JSON). Record: PersonalEntitySyncResult with Totals/Uploaded/LocalWins/RemoteWins per entity + CompletedAtUtc. Uses SyncDocumentMergeEngine.BuildPlan generic. NOTE: removed accidental duplicate CreateFirestoreException overload.
- `src/Maen.Accounting.App/Data/PlanningRepository.cs` — CRUD + batch upserts for FinancialPlanRow/ObligationRow/DepositRow + JSON helpers (ParseCategoryLimits, SerializeCategoryLimits, ParsePaidOccurrences, SerializePaidOccurrences using Core PlanCategoryLimit and DateOnly).

### MainStateViewModel edits done
- Injected PersonalFirestoreSyncService + PlanningRepository; added fields _lastPersonalEntitySyncResult, _planProgress, _upcomingObligations.

### MainStateViewModel edits REMAINING
1. SyncInternalAsync: add `_lastPersonalEntitySyncResult = string.Equals(_preferences.StorageScope, "personal", ...) ? await _personalEntitySyncService.SyncAsync() : null;`
2. FormatSyncSummary: add personal-entity counters (T395) after existing business block; business block is personal-scope-gated by `_lastBusinessSyncResult is null`.
3. Add a new method (e.g. async Task RefreshPlanningAsync / call from ReloadAsync) computing _planProgress via PersonalFinancialPlanCalculator.TrackPlan(plan, entries, date) and _upcomingObligations via ObligationScheduleCalculator.UpcomingEvents(rows->Obligation models, date, 30) with public properties PlanProgress / UpcomingObligations (ObservableCollection or list) for dashboard binding.
4. Wire ReloadAsync to fetch plan/obligations/deposits from _planningRepository.

### Sync summary UiText keys already present
- T392 (personal entries sync msg: uploaded, localWins, remoteWins, total, completedAt)
- T393 (full sync summary same order + completedAt first)
- T394 (business entities: contactsTotal, invoicesTotal, paymentsTotal, uploaded, localWins, remoteWins, totalRecords)
- NEW needed: T395 personal planning entities sync summary line (plansTotal, obligationsTotal, depositsTotal, uploaded, localWins, remoteWins, totalRecords) — format like T394

### Remaining phase 11/12 items
- MauiProgram.cs: register PlanningRepository + PersonalFirestoreSyncService
- PersonalDashboardPage: add plan progress card + obligations ticker + search button
- Business dashboard (BusinessViewModel or new page): position summary via BusinessFinancialSummaryCalculator
- Category registry page (personal)
- Command palette / quick actions page
- UiText: T395+ keys (AR+EN)
- Tests: MovementSearchEngineTests, PersonalFinancialPlanCalculatorTests, ObligationScheduleCalculatorTests, BusinessFinancialSummaryCalculatorTests, CategoryRegistryCalculatorTests, SchemaMigrationCatalogTests update (version 5)
- SyncMergeEngine/SummaryCalculator tests untouched; existing 47 tests still pass

### SyncMergeEngine.BuildPlan signature (used by both services)
BuildPlan(userId, local, remote, idSelector, getUserIdSelector, updatedAtSelector, versionSelector, deviceSelector) -> plan with ItemsToPush/MergedItems/LocalWins/RemoteWins (generic).

## Build fix status (after DI integration)
Errors fixed so far: added `using Maen.Accounting.App.Services;` in PlanningRepository.cs; added `using Maen.Accounting.Core.Models;` and `using Maen.Accounting.Core.Services;` in PersonalFirestoreSyncService.cs.
Remaining errors from last build (to fix and rebuild):
1. `PersonalFirestoreSyncService.cs:149` CS8821 — static anonymous function refers to `this`. In `SyncEntityAsync` call `versionSelector: static row => row.Version` ok but the `saveMerged` delegate or ReadUserId switch may reference this. Check line 149: `static row => ReadUserId(item)` — `ReadUserId` is static so fine; likely the lambda `merged => _repository.UpsertPlansAsync(...)` uses closure `this` — change to local static method or non-static delegate. In BusinessFirestoreSyncService the calls use `merged => _repository...` too and compiled OK there — difference: there `SaveMerged` is declared `Func<IReadOnlyList<T>, Task> saveMerged` (non-static OK). The static error likely points at `idSelector: static row => row.PlanId` combined with `ReadUserId` inside BuildPlan `getUserIdSelector: static item => ReadUserId(item)` — Business service uses `static item => GetUserId(item)` which compiled. Actually the real issue: line 149 column 28 — inspect file around BuildPlan call.
2. `MainStateViewModel.cs:401` — `new DateOnly(ticks)` invalid; use `DateOnly.FromDateTime(new DateTime(row.StartDateTicks, DateTimeKind.Utc))` or FromTimeSpan. Deposit/plan rows store ticks in UTC.
3. `MainStateViewModel.cs:405` — Obligation record positional params; remove named args updatedAtUtc/version/deviceId and pass as positional after PaidOccurrences.
Next: rebuild, then Phase 12 (UI): PersonalDashboard cards + obligations ticker + T395 UiText key + search UI.

## Phase 12 status (UPDATED)
Done so far in phase 12 (UI):
- UiText T395-T412 added (plan cards, obligations, search, registry).
- MainStateViewModel: PlanExpectedIncomeText/PlanSpendingLimitText/PlanSavingsTargetText/PlanActualIncomeText/PlanActualSpendingText/PlanSpendingProgress/PlanSavingsProgress/PlanProgressPercentText/HasPlan/OverdueObligationsCount + RaisePlanningProperties inside RaiseSummaries. DisplayMovementType made internal static.
- PersonalDashboardPage.xaml: plan card + obligations ticker (BindableLayout UpcomingObligations w/ ObligationStatusConverter/ObligationTextConverter) + MOVEMENT SEARCH card (Entry MovementSearchText -> OnMovementSearchTextChanged in code-behind, results list MovementSearchResults ObservableCollection<MovementSearchItemViewModel>, T405 title, T406 placeholder, T407 empty label with InverseBooleanConverter).
- New Views/Converters.cs: ObligationStatusConverter, ObligationTextConverter, InverseBooleanConverter.
- New MovementSearchItemViewModel.cs (TitleText=Category, DetailText=Notes, AmountText/AmountColor, MovementTypeText via internal MainStateViewModel.DisplayMovementType).
- Build Debug: 0 errors.

Remaining phase 12:
1. Planning management screens: Financial plan editor (income limit, spending ceiling, savings target, per-category limits), obligations manager (add/list/pay occurrence), deposits (manual bank cash deposits). Options: new Views/PlanningPage.xaml or sections in SettingsPage.xaml. Repos: PlanningRepository (SavePlanAsync(FinancialPlan), SaveObligationAsync(Obligation), UpsertDepositAsync(Deposit), GetPlansAsync/GetObligationsAsync/GetDepositsAsync). Models: FinancialPlan(MonthlyIncomeMinor/MonthlySpendingLimitMinor/MonthlySavingsTargetMinor/CategoryLimits List<PlanCategoryLimit>), Obligation(Title/Category/AmountMinor/StartDate/StartDateTicks/Cycle/IsActive/PaidOccurrences List<DateOnly>).
2. Category registry card on personal dashboard (CategoryRegistryCalculator.ComputeRegistry + RegistryColor palette in Core).
3. Business position card on DashboardPage (BusinessFinancialSummaryCalculator.Summarize — needs BusinessViewModel/Repository integration).
4. Tests phase 13: MovementSearchEngineTests, PersonalFinancialPlanCalculatorTests, ObligationScheduleCalculatorTests, BusinessFinancialSummaryCalculatorTests, CategoryRegistryCalculatorTests + update migration catalog test for v5. Target 60+.
5. csproj bump 2.5.0/versionCode 7, release notes, commit, push, gh release v2.5.0 (apk/aab signed in bin/Release/net10.0-android/publish/).

## Phase 12 continued (after cards added)
DONE:
- BusinessViewModel: added FinancialPosition (BusinessFinancialSummary), ReceivableNetText/PayableNetText/NetPositionText/NetPositionColor/OverdueSalesText/OverduePurchasesText/HasFinancialPosition. RebuildFinancialPosition() called at end of ReloadAsync + ReloadCoreAsync. NOTE: Contacts.Select(ContactsBy()) is BROKEN (recursive) — must fix: cache raw lists _rawContacts/_rawInvoices/_rawPayments and use them in Summarize.
- MovementSearchItemViewModel: uses internal MainStateViewModel.DisplayMovementType — ok.

FIXME immediately in BusinessViewModel.RebuildFinancialPosition:
- Replace Contacts.Select(ContactsBy()) recursion with raw lists: add fields `IReadOnlyList<AccountingContact> _rawContacts`, `_rawInvoices`, `_rawPayments`; fill in ReloadCoreAsync before VM lists; pass to Summarize.

TODO (phase 12 remaining):
1. DashboardPage.xaml business position card using the above VM properties (title key new, use T412+).
2. Planning management UI: choose approach — add a PlanningPage.xaml (tab on PersonalTabbedPage) OR Settings sections. Need: plan editor (income/spending limit/savings target/per-category limits), obligations list+add+mark paid occurrence, deposits list+add. Use PlanningRepository UpsertPlanAsync/UpsertObligationAsync/UpsertDepositAsync + PlanningRepository.Parse/SerializeCategoryLimits/PaidOccurrences.
3. Category registry card on personal dashboard: CategoryRegistryCalculator.ComputeRegistry(entries.Select(e=>new CategoryEntry(e.Category, e.EntryDate)), defaults from ledger) — need RegistryTexts properties in MainStateViewModel + recompute on ReloadAsync (after RebuildPlanningAsync).
4. UiText keys still free from T413.
5. Phase 13 tests: MovementSearchEngineTests, PersonalFinancialPlanCalculatorTests, ObligationScheduleCalculatorTests, BusinessFinancialSummaryCalculatorTests, CategoryRegistryCalculatorTests + update SchemaMigrationCatalogTests for v5. Target 60+ tests (currently 47).
6. csproj version 2.5.0/versionCode 7, docs/release-notes-2.5.0.md, commit+push, gh release v2.5.0 (Signed apk/aab in bin/Release/net10.0-android/publish/).
7. Build command: dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release --no-restore.

Contracts reference:
- FinancialPlan(MonthlyIncomeMinor, MonthlySpendingLimitMinor, MonthlySavingsTargetMinor, IReadOnlyList<PlanCategoryLimit> CategoryLimits); PlanCategoryLimit(Category, LimitMinor).
- Obligation(ObligationId, UserId, Title, Category, AmountMinor, StartDate DateOnly, Cycle "daily|weekly|monthly|yearly", IsActive, IReadOnlyList<DateOnly> PaidOccurrences); Obligation.Empty(id, userId).
- ObligationScheduleCalculator.UpcomingEvents(obligations, asOfDate, lookAheadDays=30) -> IReadOnlyList<ObligationEvent(ObligationId, Title, Category, AmountMinor, DueDate, Cycle, IsPaid)); IsValid.
- BusinessFinancialSummaryCalculator.Summarize(contacts, invoices, payments, asOfDate) -> BusinessFinancialSummary(AsOfDate, TotalSalesMinor, TotalPurchasesMinor, CustomerReceiptsMinor, SupplierPaymentsMinor, ReceivableNetMinor, PayableNetMinor, NetPositionMinor, DueSoonSalesMinor, OverdueSalesMinor, DueSoonPurchasesMinor, OverduePurchasesMinor, ActiveContacts, PostedInvoices, TopContactsByBalance); ContactBalance(ContactId, Name, Type, NetBalanceMinor).
- CategoryRegistryCalculator.ComputeRegistry(IEnumerable<CategoryEntry>, IReadOnlyCollection<string> defaults) -> IReadOnlyList<CategoryRegistryItem(Category, ColorHex, EntriesCount, LastUsedDate)); CategoryEntry(Category, EntryDate DateOnly).
- MainStateViewModel: RebuildPlanningAsync(session.UserId, entries) called from ReloadAsync; entries variable is `IReadOnlyList<ProfitEntry> entries` before call.
- PersonalFinancialPlanCalculator.TrackPlan(plan, entries, asOfDate) -> PersonalPlanProgress(ExpectedIncomeMinor, ExpectedSpendingLimitMinor, ExpectedSavingsTargetMinor, ActualIncomeMinor, ActualSpendingMinor, SavingsProgressPercent, CategoryProgress IReadOnlyList<PlanCategoryProgress>).

## Phase 12 status (updated 2)
FIXED: BusinessViewModel recursive ContactsBy() replaced with _rawContacts/_rawInvoices/_rawPayments fields, filled in ReloadAsync+ReloadCoreAsync, RebuildFinancialPosition uses raw lists. (3 edits done, compile pending.)
UiText DONE: T413-T450 added (financial position card keys: T413 title, T414-T418 metrics; planning T419-T430; deposits T431-T435, T436-T446; misc T447-T450).
Next steps:
1. Card in DashboardPage.xaml (business dashboard): metric card "T413" with grid of T414 ReceivableNetText, T415 PayableNetText, T416 NetPositionText (NetPositionColor), T417 OverdueSalesText, T418 OverduePurchasesText. Binding: {Binding ReceivableNetText} etc. Insert before add-button section (line ~80).
2. Personal dashboard: category registry card (T408). Need MainStateViewModel: CategoryRegistry list + RegistryItemsTexts property (ObservableCollection<RegistryItemViewModel> with Category, ColorHex, EntriesCount, LastUsedText). Compute in ReloadAsync after RebuildPlanningAsync: CategoryRegistryCalculator.ComputeRegistry(entries.Select(e=>new CategoryEntry(e.Category, e.EntryDate)), defaultCategories from DefaultChartOfAccounts or empty). Default categories: use DefaultChartOfAccounts category names (grep for T356-T365 keys).
3. Planning management UI: PlanningPage.xaml as new tab on PersonalTabbedPage (or modal). Must have: plan editor (T419: income T429/spending T398/savings T399/per-category limits T445 + T446 section), obligations section (T400: list w/ T401-T404 status, T421 add form T422+amount+T423 (T424-T427), T439/T440 toggle), deposits section (T431: list, T432 add with T442 date + T436 amount + T443 description). Save results T433/T434/T435. Use PlanningRepository: UpsertPlanAsync(userId, new FinancialPlan(...)), UpsertObligationAsync, UpsertDepositAsync. Model FinancialPlan(MonthlyIncomeMinor, MonthlySpendingLimitMinor, MonthlySavingsTargetMinor, IReadOnlyList<PlanCategoryLimit>(Category, LimitMinor)). Deposit model: check DepositRow fields in Data/FinancialPlanRow.cs.
4. PersonalTabbedPage.cs: add PlanningPage tab after personal dashboard. Register in MauiProgram.
5. Phase 13 tests: MovementSearchEngineTests, PersonalFinancialPlanCalculatorTests, ObligationScheduleCalculatorTests (due soon/overdue/paid cycle), BusinessFinancialSummaryCalculatorTests (receivable/payable/net/buckets), CategoryRegistryCalculatorTests. Update SchemaMigrationCatalogTests to expect migration 5. Target 60+ (currently 47).
6. csproj -> 2.5.0/versionCode 7; docs/release-notes-2.5.0.md; commit+push; gh release v2.5.0 with signed apk/aab (bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk/.aab).
Build cmd: dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release --no-restore
Test cmd: dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj --no-restore

## Phase 12 status (updated 3)
DONE: BusinessViewModel financial position card wired (ReceivableNetText/PayableNetText/NetPositionText/NetPositionColor/OverdueSalesText/OverduePurchasesText; HasFinancialPosition). Compile OK.
DONE: UiText T413-T450.
DONE: DashboardPage.xaml: added T413 financial position card (IsVisible HasFinancialPosition) before "Add entry" button. Styles: CardBorder, SectionTitle, MetricCaption, PrimaryButton. Converters namespace: local=Maen.Accounting.App (Views use local for ObligationStatusConverter).
PersonalDashboardPage.xaml layout: line ~108 plan card (HasPlan), ~143 ProgressBar, obligations card at line ~172 (UpcomingObligations BindableLayout, ObligationStatusConverter/ObligationTextConverter via local), T155 budget card after line ~199.
NEXT: 
1. CategoryRegistryCalculator.ComputeRegistry(entries: IEnumerable<CategoryEntry>, defaults: IReadOnlyCollection<string>) returns CategoryRegistryItem(Category, ColorHex, EntriesCount, LastUsedDate). In MainStateViewModel RebuildPlanningAsync: add _categoryRegistry = ComputeRegistry(entries.Select(e=>new CategoryEntry(e.Category, DateOnly.FromDateTime(e.EntryDate))), defaultCategories). Add property ObservableCollection<RegistryItemViewModel> CategoryRegistryItems + OnPropertyChanged in RaisePlanningProperties. Need default categories list: use PersonalLedgerSummaryCalculator's categories? grep PersonalCategoryItemViewModel defaultCategories (T356-T365 keys exist: get from PersonalMovementTypes or from MainStateViewModel CurrentMonthCategorySpending defaults).
2. PlanningPage.xaml: new page under Views, tab in PersonalTabbedPage.cs (add after PersonalDashboardPage). Sections: plan editor (MonthlyIncomeMinor/MonthlySpendingLimitMinor/MonthlySavingsTargetMinor + per-category limits via CategoryRegistryCalculator defaults), obligations list+add form (Title, AmountMinor, Cycle (Daily/Weekly/Monthly/Yearly) from ObligationRow.Cycle), deposits list+add (Date, AmountMinor, Description). Save via PlanningRepository (UpsertPlanAsync/UpsertObligationAsync/UpsertDepositAsync). Toast messages T433/T434/T435 + T441. Register page in MauiProgram.
3. DepositRow fields: check src/Maen.Accounting.App/Data/FinancialPlanRow.cs (DepositId, UserId, DepositDateTicks, AmountMinor, Description, IsDeleted, Version, DeviceId?).
4. Phase 13: tests + schema migration 5 expected in SchemaMigrationCatalogTests.

## Phase 12 status (updated 4) — patterns found for PlanningPage
- ProfitEntryRow.DeviceId via `_deviceIdentity.GetOrCreate()` (IDeviceIdentity injected in MainStateViewModel ctor). PlanningPage can inject IDeviceIdentity too (check registration in MauiProgram).
- Upsert pattern: repository.UpsertXAsync(userId, row). Date ticks: DateTime.UtcNow.Ticks for UpdatedAtUtcTicks. StartDateTicks/DepositDateTicks: DateOnly.ToDateTime(TimeOnly.MinValue).UtcTicks.
- PlanCategoryLimit record in PersonalFinancialPlanCalculator.cs (Category, MonthlyLimitMinor). PlanningRepository.ParseCategoryLimits(json)/SerializeCategoryLimits(limits), ParsePaidOccurrences/SerializePaidOccurrences.
- No default category list exists; defaults = T390 "Uncategorized". CategoryRegistryItem uses defaults + existing entry categories. For CategoryRegistry calc: pass entries = entries.Select(e => new CategoryEntry(e.Category, e.EntryDate)), defaults = ["Uncategorized" translated: use UiText.Get("T390")].
- PersonalEntryPage.xaml.cs: OnSaveClicked calls _state.SavePersonalCurrentAsync(). PersonalTabbedPage.cs holds tabs: add PlanningPage as 3rd tab.
- T419-T450 keys exist (Arabic/English pairs above).
- Build succeeded after BusinessViewModel brace fix + DashboardPage card.

## Phase 12 status (updated 5) — category registry card DONE
DONE: MainStateViewModel: CategoryRegistryItems ObservableCollection<CategoryRegistryItem> + compute in RebuildPlanningAsync via CategoryRegistryCalculator.ComputeRegistry(entries, [UiText.Get("T390")]) + OnPropertyChanged.
DONE: PersonalDashboardPage.xaml: registry card (T446 title, T418 subtitle, FlexLayout of 140x72 gold tiles w/ ColorHex dot + Category + EntriesCountText, before T181 budget card, line ~222).
NEXT: build Debug to verify, then PlanningPage.xaml (planning management UI as new tab on PersonalTabbedPage.cs) — plan editor + obligations list/add + deposits list/add. Use PlanningRepository contracts from above. Register in MauiProgram. Then phase 13 tests + release.

## Phase 12 status (updated 6) — PlanningPage started
DONE: PlanningPage.xaml created at src/Maen.Accounting.App/Views/PlanningPage.xaml with 3 sections: plan editor (T446 title + T447/T448 eyebrow/subtitle; inputs PlanIncomeInput T429, PlanSpendingLimitInput T398, PlanSavingsTargetInput T399, PlanLimitCategories per-category limits T445, Save button T420), obligations (T400 title + T401 subtitle; ObligationTitleInput T421?, ObligationAmountInput T336, Picker T423 cycles, Add button T422; list ObligationAmountText/CycleText/IsActive switch; T441 empty), deposits (T431 title + T432 subtitle; DepositTitleInput T437?, DepositAmountInput T336, DepositDate DatePicker, Add button T436; list AmountText/DateText; T438 empty).
UI ISSUE: T042 = "القيود المرحّلة/Posted journal entries" — WRONG title for PlanningPage. Must repurpose: choose new keys.
UiText actual keys found (grep): T419 خطة مالية, T420 إدارة الخطط والالتزامات, T421 إضافة التزام, T422 اسم الالتزام, T423 الدورية, T429 الدخل المتوقع الشهري, T431 الإيداعات النقدية, T432 إضافة إيداع, T436 أدخل المبلغ, T437 لا توجد التزامات نشطة (wrong!), T438 لا توجد إيداعات بعد, T439 إيقاف الالتزام, T441 تم الحفظ محلياً, T445 حد المصروف لهذه الفئة, T446 فئات الخطة, T447 لا يوجد حد لهذه الفئة (wrong), T448 مؤشر الادخار, T449 المصروف الفعلي, T450 الدخل الفعلي.
MUST add new UiText keys: T451 planning page title ("التخطيط والودائع"/"Planning & deposits"), T452 subtitle, T453 plan income placeholder, T454 obligation amount placeholder, T455 deposit title placeholder, T456 obligation cycles (daily/weekly/monthly/yearly 4 keys or reuse existing), T457 save plan toast, T458 add obligation toast, T459 add deposit toast.
PlanningPage code-behind: bind to a NEW PlanningViewModel (inject PlanningRepository, IDeviceIdentity). VM properties: PlanIncomeInput/PlanSpendingLimitInput/PlanSavingsTargetInput (strings), PlanLimitCategories ObservableCollection<PlanLimitItem(Category,ColorHex,LimitText)>, Obligations ObservableCollection<ObligationItem(ObligationId,Title,AmountText,AmountMinor,CycleText,IsActive)>, Deposits ObservableCollection<DepositItem(DepositId,Title,AmountText,AmountMinor,DateText)), ObligationTitleInput/ObligationAmountInput/ObligationCycleSelection/ObligationCycles(list), DepositTitleInput/DepositAmountInput/DepositDate.
NEXT: 1) fix/add UiText keys, 2) PlanningPage.xaml.cs + PlanningViewModel, 3) PersonalTabbedPage.cs add PlanningPage as 3rd tab (inject + Title T451), 4) MauiProgram register page, 5) build, 6) phase 13 tests, 7) release 2.5.0.
DepositRow: DepositId, UserId, Title, OwnerName, AmountMinor, DepositDateTicks, Kind "deposit", IsWithdrawn, Notes, UpdatedAtUtcTicks, Version, DeviceId, IsDeleted.
ObligationRow: ObligationId, UserId, Title, Category, AmountMinor, StartDateTicks, Cycle (daily/weekly/monthly/yearly), IsActive, PaidOccurrencesJson, UpdatedAtUtcTicks, Version, DeviceId, IsDeleted.
FinancialPlanRow: PlanId, UserId, MonthlyIncomeMinor, MonthlySpendingLimitMinor, MonthlySavingsTargetMinor, CategoryLimitsJson, UpdatedAtUtcTicks, Version, DeviceId, IsDeleted.

## Phase 12 status (updated 7) — PlanningPage.xaml + .xaml.cs DONE
DONE: PlanningPage.xaml created (3 sections: plan editor w/ PlanIncomeInput T429, PlanSpendingLimitInput T398, PlanSavingsTargetInput T399, PlanLimitCategories; obligations w/ ObligationTitleInput T453, ObligationAmountInput T336?? [placeholder uses T336 debt payment — ok], Picker cycles T423, button T461; deposits w/ DepositTitleInput T454, amount T336, DatePicker, button T462). Title T451, BrandHeaderView T452.
DONE: PlanningPage.xaml.cs w/ PlanningViewModel (ReloadAsync, SavePlanAsync, AddObligationAsync, AddDepositAsync; uses Money.MinorToString, Money.TryParseMinor, SessionCoordinator.CurrentUserId, IDeviceIdentity injected). ObligationItem (IsActive toggle), DepositItem records. NEW UiText keys T451-T462 added (T451 title planning, T453 obligation title placeholder, T454 deposit name placeholder, T457/458/459 toast, T460 cycles, T461 add obligation button, T462 add deposit button).
NOTE: PlanningViewModel has a placeholder `private async Task LoadPlanAsync()` — MUST implement body. Also `Money.MinorToString`/`TryParseMinor` names must be verified (check src/Maen.Accounting.Core/Services/Money.cs), `PlanningRepository.GetPlansAsync(userId)` returns rows (check), `ParseCategoryLimits(json)` static, `SessionCoordinator.CurrentUserId` static property (verify), IDeviceIdentity.GetOrCreate() injected in ctor.
NEXT: 1) verify Money method names, SessionCoordinator, PlanningRepository signatures; fix errors; 2) PersonalTabbedPage.cs: add PlanningPage tab (inject + Title T451), register in MauiProgram; 3) build; 4) phase 13 tests; 5) release 2.5.0 (csproj 2.5.0 + versionCode 7, docs/release-notes-2.5.0.md, commit+push, gh release v2.5.0).

## Phase 12 status (updated 8) — CRITICAL FACTS
PlanningPage.xaml.cs FIXED with real APIs: Money.TryParse(text,out minor), Money.Format(minor), AuthSessionStore.LoadAsync() returns AuthSession?(UserId,IsLocal...), DeviceIdentityService (NOT IDeviceIdentity; registered as Singleton in MauiProgram line ~51), DisplayAlertAsync on page. GetUserIdAsync() uses _sessionStore.LoadAsync(). PlanCategoryLimit(Category, MonthlyLimitMinor) in Core.Services.PersonalFinancialPlanCalculator namespace.
DepositDateTicks/StartDateTicks/UpdatedAtUtcTicks are DateTime ticks (new DateTime(ticks, Unspecified)) NOT unix — FIXED.
NEXT STEPS: 1) Add PlanningPage tab to PersonalTabbedPage.cs (inject PlanningPage, AuthSessionStore, DeviceIdentityService — check existing ctor pattern; title T451 "التخطيط والودائع"); 2) MauiProgram: register PlanningPage + planning viewmodel if needed (PlanningPage resolves itself via ctor injection from app services); 3) Build Debug; 4) Phase 13: tests (PersonalFinancialPlanCalculator, ObligationScheduleCalculator, MovementSearchEngine, BusinessFinancialSummaryCalculator, CategoryRegistryCalculator — need ~10+ new tests); 5) csproj -> 2.5.0, versionCode 7; release-notes-2.5.0.md; commit+push main; gh release v2.5.0 (APK AAB from publish, notes file).
IMPORTANT: current version is 2.4.0 (versionCode 6 assumed), release v2.4.0 exists on GitHub. Last commit 207f539 (2.4.0).

## Phase 12 DONE — build Release succeeds (net10.0-android). PlanningPage fixed (ObligationItem class inherits ObservableObject, T460 added).

## Phase 13 (tests) — IN PROGRESS
- Created tests/Maen.Accounting.Core.Tests/V250EngineTests.cs with 72 new tests for the 5 v2.5.0 engines. Base was 47 tests.
- Behavioral facts verified from reading the engine sources (keep in mind if tests fail):
  1. BusinessFinancialSummaryCalculator uses invoice.TotalMinor (= Subtotal + TaxMinor) for ALL totals (TotalSalesMinor, ReceivableNetMinor, buckets). Tax is INCLUDED, not excluded.
  2. NetPositionMinor = ReceivableNetMinor - PayableNetMinor (receivables use customer receipts, payables use supplier payments, gross = sum of TotalMinor of posted invoices).
  3. BucketTotal splits paid amount pro-rata across bucket invoices (unpaid share), doesn't simply subtract from bucket sum.
  4. ObligationScheduleCalculator monthly day-31 start: advances via AddMonthsSafe which clamps day to end of month (31/7 -> 31/8, 30/9). LookAheadDays=5 from 8/15 covers up to 8/20 only -> 1 event for start 8/10.
  5. PersonalFinancialPlanCalculator.UtilizationPercent is float division -> use Assert.InRange.
  6. Summarize signature: (IEnumerable<Contact>, IEnumerable<Invoice>, IEnumerable<Payment>, DateOnly). Payment ctor: (PaymentId, UserId, Number, Type, PaymentDate, ContactId, AmountMinor, Notes, LinkedTo, CreatedAtUtc, UpdatedAtUtc...).
- Tests run OK after fixes (pending final run to confirm all pass + verify SchemaMigrationCatalogTests still covers v5 if updated).
- NEXT: run full test suite, update SchemaMigrationCatalogTests if needed, then Phase 14: csproj -> 2.5.0 versionCode 7, docs/release-notes-2.5.0.md, commit+push main, dotnet publish Release (apk/aab signed in bin/Release/net10.0-android/publish/com.maen.accounting-Signed.*), gh release create v2.5.0.

## CRITICAL BUILD FACTS (fix list for PlanningPage.xaml.cs)
1. ObservableObject lives in Maen.Accounting.App.Infrastructure (abstract class: SetProperty<T>, OnPropertyChanged protected). Records CANNOT inherit from it -> ObligationItem must be `public sealed class ObligationItem : ObservableObject` with private readonly fields + props (not a record with Row parameter).
2. DisplayAlert is DisplayAlertAsync in ContentPage (MAUI 10) — OK.
3. Money.TryParse(text, out minor), Money.Format(minor) — OK.
4. AuthSessionStore.LoadAsync() -> Task<AuthSession?> with UserId. DeviceIdentityService.GetOrCreate().
5. Ticks are DateTime ticks (new DateTime(ticks, Unspecified)), DepositDate saved local .Date.Ticks.
6. PlanningPage ctor DI: PlanningRepository, DeviceIdentityService, AuthSessionStore. Registered in MauiProgram. Tab added to PersonalTabbedPage with title T451.
7. T451 (Planning tab title) + T457-T462 all exist in UiText.
NEXT: convert ObligationItem to class, build debug again, then phase 13 tests.
