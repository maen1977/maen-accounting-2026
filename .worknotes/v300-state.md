# v3.0.0 State Notes

## Context
- v2.9.0 released (235 tests, tag v2.9.0). User asked: "رفع مستوى البرنامج بكل المعايير باقصى الحدود" → new wave v3.0.0.
- Plan: docs/v300-plan.md (engines: RecurringMovementCalculator, TopCategoryAnalyticsEngine, AuditTrailCalculator; migration 9: entry_memos + recurring_movements tables; UI: memo on PersonalEntryPage, recurring in PlanningPage, audit card in SettingsPage, analytics card on ReportsPage; ~30 T-keys T700-T735; ~40 tests → ≥275; version 3.0.0 Build 12).

## Build & publish commands
- Build: `dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -p:AndroidSdkDirectory=/home/ubuntu/android-sdk`
- Publish APK: `dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -c Release -p:AndroidSdkDirectory=/home/ubuntu/android-sdk -p:AndroidPackageFormat=apk -f net10.0-android` → bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk/.aab
- Tests: `dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj --nologo -v q`
- gh release: `gh release create v3.0.0 --title "..." --notes-file docs/release-notes-3.0.0.md <apk> <aab>`
- csproj currently: ApplicationDisplayVersion 2.9.0, ApplicationVersion 11 → bump to 3.0.0 / 12.
- Repo: maen1977/maen-accounting-2026, main branch.

## Patterns (for new code)
- Row class pattern (FinancialPlanRow.cs): [Table("name")], PrimaryKey string Id, UserId, UpdatedAtUtcTicks, Version=1, DeviceId, IsDeleted. ObligationRow: Title, Category, AmountMinor, StartDateTicks, Cycle="monthly", IsActive, PaidOccurrencesJson. DepositRow: Title, OwnerName, AmountMinor, DepositDateTicks, Kind, IsWithdrawn, Notes.
- SavingsGoalRow has: GoalId, UserId, Title, Category, TargetMinor, SavedMinor(?), StartDateTicks, DeadlineTicks, IsDeleted. (Verify actual fields before use.)
- PlanningRepository pattern: GetRowsAsync(userId) via database.Table<T>().ToListAsync(); UpsertRowAsync(userId, row); ToRow static FromModel helpers in rows.
- UserDatabaseFactory: Add CreateTableAsync<NewRow> after SavingsGoalRow (line ~44); add migration action [9] = EnsureXAsync in migrationActions; add private static method; SchemaMigrationCatalog.All contains catalog entries (add Migration 9 "entry-memos-recurring").
- Core Models (Models/*.cs): ProfitEntry constructor: (id, userId, entryDate, salesMinor, costMinor, expensesMinor, notes, isDeleted, createdAt, updatedAt, version, deviceId, Category: ). FinancialPlan, Obligation records in PersonalFinancialPlanCalculator.cs (FinancialPlan(income, spendingLimit, savingsTarget, limits); PlanCategoryLimit(Category, MonthlyLimitMinor)).
- Money.Format(long minor). DateOnly DayNumber. UiText: static Get("Tnnn"), Format("Tnnn", arg).
- SettingsPage.xaml has OnExportClicked (T057). SettingsPage has viewmodel SettingsViewModel (find in Views/SettingsPage.xaml.cs).
- MainStateViewModel.SavePersonalCurrentAsync (line 754), WriteBackupAndUpdateAsync (1008), ReloadAsync (469) calls RebuildPlanningAsync (527).
- PersonalEntryPage.xaml/.xaml.cs — entry editing. PlanningPage.xaml/.xaml.cs — PlanningViewModel with ReloadAsync loads obligations+deposits.
- Core Services namespace: Maen.Accounting.Core.Services. Models: Maen.Accounting.Core.Models.

## DONE so far (update this section as work progresses)
- Migration 9 added to SchemaMigrationCatalog (entry-attachments-and-recurring-movements).
- RecurringMovementRow in FinancialPlanRow.cs (table recurring_movements). ProfitEntryRow.AttachmentBase64 column added.
- UserDatabaseFactory: CreateTableAsync<RecurringMovementRow> + EnsureAttachmentsAndRecurringAsync (migration 9: adds AttachmentBase64 + ix_recurring_user_active).
- PlanningRepository: constructor now takes DeviceIdentityService; GetRecurringAsync/UpsertRecurringAsync(both)/SoftDeleteRecurringAsync/AdvanceOccurrenceAsync.
- Core engines created: RecurringMovementCalculator (DueMovements, AdvanceOccurrence, BuildAmounts, DaysUntil; record RecurringMovement), TopCategoryAnalyticsEngine (Analyze→CategoryAnalytics[] with SharePercent/PeakMonth/LowMonth/AmountText; MonthlyTotals→MonthlyAmount[]), AuditTrailCalculator (Summarize→AuditSummary with verdict healthy/partial/pending).
- UiText keys T700-T736 added (recurring, attachment, analytics, audit).
- Core builds clean; App builds clean. Tests still 235 (not yet updated for v3.0.0).

## v3.0.0 TODO checklist (phase 3)
- [x] MainStateViewModel DONE: props (CategoryAnalytics/MonthlyAmounts/AuditSummary/RecurringMovements + Has*/TopCategory/TopCategoryText/PeakMonth/LowMonth/AuditVerdictText/AuditVerdictColor/AuditedRecordsText), RebuildRecurringAsync (applies due via UpsertManyAsync + AdvanceOccurrenceAsync; StatusMessage set via _statusMessage+OnProperty), UpsertRecurringMovementAsync, DeleteRecurringMovementAsync, RebuildAnalyticsAndAuditAsync (Task.Run analytics + personal sync), called from ReloadAsync after RebuildPlanningAsync and RebuildRecurringAsync in RebuildPlanningAsync. RaisePlanningProperties extended.
- [x] PlanningPage.xaml: recurring card added (T700-T702,T704,T705,T711,T714,T715; inputs RecurringTitleInput/RecurringAmountInput/RecurringCategorySelection/CycleSelection/KindSelection/RecurringStartDate; list RecurringItems with Title/SummaryText/NextText + delete button OnDeleteRecurringClicked using T460 + DangerButton/SecondaryButton styles; InverseBooleanConverter available)
- [x] PlanningPage.xaml.cs DONE: props + LoadRecurringAsync + AddRecurringAsync + DeleteRecurringMovementAsync + RecurringItem record + handlers in page + NonNullConverter added to Converters.cs (App namespace: Maen.Accounting.App; static Instance)
- [x] ReportsPage.xaml DONE: analytics card after savings trend (T724-T728; binds TopCategoryText/PeakMonthLabel/PeakMonthAmountText/LowMonthLabel/LowMonthAmountText/CategoryAnalytics items: Category/PeriodText/AmountText/ShareText — MUST add wrapper props in MainStateViewModel: PeakMonthLabel, PeakMonthAmountText, LowMonthLabel, LowMonthAmountText, and add PeriodText+ShareText to CategoryAnalytics record)
- [ ] PlanningPage.xaml.cs: add PlanningViewModel props (OBSOLETE — done) (RecurringTitleInput="", RecurringAmountInput="", RecurringCategories["", ..existing categories], RecurringCycleSelections=[T706,T707,T708], RecurringKindSelections=[T703,T704], RecurringCycleSelection default T706, RecurringKindSelection default T704, RecurringStartDate=Today, RecurringItems list of records) + OnAddRecurringClicked (long.Parse(AmountInput), kind "income" if T703 else "expense", cycle "monthly"/"weekly"/"yearly") + OnDeleteRecurringClicked + Rebuild recurring items in ReloadAsync from MainStateViewModel.RecurringMovements. T460 = "حذف" delete key (check).
- [ ] ReportsPage: add category analytics card (T724-T729) binding MainStateViewModel CategoryAnalytics
- [ ] SettingsPage: add audit card (T730-T735) binding AuditSummary
- [ ] DONE Reports/Settings/Planning UI cards + Core CategoryAnalytics.PeriodText/ShareText + MainStateViewModel PeakMonthLabel/PeakMonthAmountText/LowMonthLabel/LowMonthAmountText + NonNullConverter. Build 0 errors.
- [x] PersonalEntryPage.xaml DONE: attachment section after notes (T721/T722/T723; HasAttachment/AttachmentImage via InverseBooleanConverter; OnAttachPhotoClicked/OnRemoveAttachmentClicked handlers need XAML.cs + MainStateViewModel props)
- [ ] PersonalEntryPage.xaml.cs: add handlers using FileResult via FilePicker (pick max 1 photo, read all bytes → Convert.ToBase64 → set AttachmentBase64). MainStateViewModel needs: AttachmentBase64 string prop + HasAttachment + AttachmentImage (FileImageSource from bytes via stream). ProfitEntry record: constructor has 15 params (entryId,userId,date,sales,cost,expenses,notes,isDeleted,createdAt,updatedAt,version,deviceId,amountMinor,movementType,category,wallet,counterparty = 17). ProfitEntryRow.AttachmentBase64 column exists but FromModel/ToModel don't map it → add param with default "" (keep ComputeProfitEntryHash unchanged — attachment excluded from hash, fine).
- [ ] PERFORMANCE DONE? NO — still pending: check ledger CollectionView virtualization (optional quick check).
- [ ] ~40 tests V300EngineTests.cs (engines + UI-agnostic logic)
- [ ] Bump csproj version 3.0.0 / build 12, docs/release-notes-3.0.0.md, commit, tag, push, gh release
- [ ] PersonalEntryPage: attachment field (HasAttachment toggle + AttachmentBase64 string stored via ProfitEntry.AttachmentBase64 param in row; note already exists). Add AttachmentBase64 to ProfitEntry record? — row has column but ToModel doesn't map it; ProfitEntry record needs AttachmentBase64 param too + FromModel. VERIFY before.
- [ ] PlanningPage: recurring list card (T700-T713): list + add dialog (title, category, amount, kind income/expense, cycle monthly/weekly/yearly, start date) + delete.
- [ ] ReportsPage: category analytics card (T724-T729).
- [ ] SettingsPage: audit card (T730-T735).
- [ ] Performance: virtualization check CollectionView on ledger page.
- [ ] ~40 tests in V300EngineTests.cs
- [ ] Bump version 3.0.0/12, docs/release-notes-3.0.0.md, commit, tag, push, gh release
