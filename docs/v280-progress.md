# v2.8.0 Progress (Maximum Elevation)

## Context
- Repo: /home/ubuntu/maen-accounting-debug (main branch, latest v2.7.0 pushed + tag v2.7.0).
- v2.7.0 released: BusinessDocumentLifecycleService, reconciliation, filters, BusinessReportsPage, 174 tests pass.
- Plan file: docs/v280-plan.md (audit findings + work items).
- Build command: `dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -p:AndroidSdkDirectory=/home/ubuntu/android-sdk`
- Tests: `dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj`
- Publish: `dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -c Release -f net10.0-android -p:AndroidSdkDirectory=/home/ubuntu/android-sdk -o bin/Release/net10.0-android/publish/`
- gh release: `gh release create v2.8.0 --title ... --notes-file docs/release-notes-2.8.0.md bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk bin/Release/net10.0-android/publish/com.maen.accounting-Signed.aab`
- Version in csproj: ApplicationDisplayVersion 2.7.0 / ApplicationVersion 9 → needs 2.8.0 / 10.

## Done in v2.8.0 so far
1. Created DataIntegrityService (Core): ComputeInvoiceHash(invoiceId,userId,version,type,totalMinor,taxMinor,status), ComputePaymentHash(paymentId,userId,version,type,amountMinor,isDeleted), ComputeProfitEntryHash(entryId,userId,version,salesMinor,costMinor,expensesMinor,isDeleted), Verify. sha256-v1 deterministic.
2. Added `IntegrityHash` column (row property + auto-populate in FromModel) to PaymentRow/InvoiceRow/ProfitEntryRow (added using Maen.Accounting.Core.Services to each).
3. Migration 7 "document-integrity-hashes" added to SchemaMigrationCatalog + UserDatabaseFactory (3 ALTER TABLE for payments/invoices/profit_entries).
4. BusinessDocumentValidator.EnsureValidInvoice/EnsureValidPayment wired into BusinessViewModel SaveInvoiceAsync, SaveEditedInvoiceAsync (now takes sanitizedNumber/sanitizedDescription params), SavePaymentAsync; InputSanitizer.SanitizeName/SanitizeNotes applied to contact/invoice/payment inputs.
5. Created InputSanitizer (Core): SanitizeName(80)/SanitizeNotes(300)/SanitizeNumber(40) removes control chars.
6. ProfitEntry record: added IntegrityHash field + MarkDeleted updates hash + WithIntegrityHash() helper.
7. FirestoreSyncService: integrityHash field in ToFirestoreFields + ParseDocument.
8. BusinessFirestoreSyncService.ParseDocument: VerifyDownloadedIntegrity<T> rejects tampered downloaded records (reflection on IntegrityHash, ComputeExpectedHash for Invoice/Payment).
9. BUILD PASSES (Core+App), all 174 existing tests pass.

## Phase 3+4 progress (UI wiring done so far):
- Created DebtAgingCalculator (Core) + DebtAgingResult/DebtAgingSummary/DebtAgingBucket; BusinessDocuments: InvoiceType.Sales=1/Purchase=2, InvoiceStatus.Draft/Posted/Voided.
- Created DataQualityChecker (Core) with DataQualityWarning(Code,Detail): codes "UncategorizedEntry", "NegativeAmountEntry" (detail=date), "DraftInvoices" (detail=count), "UnusedContact" (detail=name). CheckPersonal and CheckBusiness.
- MainStateViewModel: added ReceivablesAgingTotalText/CurrentText/OverNinetyText/PayablesAgingTotalText/DataQualityStatusText(T538 or T543)/DataQualityStatusColor/HasDataQualityWarnings/HasAgingExposure; rebuilt in RebuildBusinessFinancialPositionAsync.
- PersonalDashboardPage.xaml: added Aging card (T544-T548, before HasPlan card) + DataQuality card (T549 title, T550 subtitle) before MovementSearch card.

## T-keys to add at end of UiText.cs dictionary (after T540):
- ["T543"] = ("تنبيهات جودة البيانات: {0}", "Data quality alerts: {0}")
- ["T544"] = ("أعمار الديون", "Debt Aging")
- ["T545"] = ("المتبقي على العملاء حسب العمر", "Outstanding receivables by age")
- ["T546"] = ("حالي", "Current")
- ["T547"] = ("أكثر من 90 يومًا", "Over 90 Days")
- ["T548"] = ("إجمالي المدين", "Total Payables")
- ["T549"] = ("جودة البيانات", "Data Quality")
- ["T550"] = ("افحص الفئات المفقودة والقيم الشاذة بانتظام", "Review missing categories and anomalies regularly")

## Phase 4 complete: Aging card in BusinessReportsPage.xaml (HasAgingExposure, T544-548) + BusinessReportsViewModel aging props; PersonalDashboardPage.xaml aging+quality cards wired. T543-T550 added to UiText.cs. Build OK.

## STATUS (latest): All 195 tests pass (174 old + 21 new V280). V280 complete summary:
- Core engines: DataIntegrityService (sha256-v1 hashes for invoice/payment/entry, ComputeProfitEntryHash(entryId,userId,version,sales,cost,expenses,isDeleted) named params!), InputSanitizer (SanitizeName 80 / SanitizeNotes 300 / SanitizeNumber 40), DebtAgingCalculator (Age(invoices,payments,asOf) → DebtAgingResult {Receivables,Payables} DebtAgingSummary buckets), DataQualityChecker (CheckPersonal(entries,contacts,payments,invoices) codes: UncategorizedEntry,NegativeAmountEntry,UnusedContact; CheckBusiness codes: DraftInvoices,UnusedContact).
- Migration 7: IntegrityHash columns in invoices/payments/profit_entries (applied in UserDatabaseFactory; catalog registered).
- Rows (InvoiceRow/PaymentRow/ProfitEntryRow) auto-populate hash in FromModel on save; Firestore sync services upload+parse integrityHash; BusinessFirestoreSyncService.ParseDocument verifies hash and drops tampered rows; LegacyBackupParser stamps hash + sanitizes on import; BusinessViewModel save paths wired to BusinessDocumentValidator+InputSanitizer.
- UI: PersonalDashboardPage aging card (T544-548) + DataQuality card (T549-550 + T543); BusinessReportsPage aging section (HasAgingExposure); MainStateViewModel/BusinessReportsViewModel wired. T543-T550 added to UiText.cs. SmallButton style in App.xaml.
- Tests file: tests/.../V280EngineTests.cs (5 classes: DebtAgingCalculatorTests, DataQualityCheckerTests, DataIntegrityServiceTests, InputSanitizerTests, LegacyBackupImportHardeningTests).

## REMAINING: bump csproj DisplayVersion 2.8.0 + ApplicationVersion 10 (currently 2.7.0/9), write docs/release-notes-2.8.0.md (base on docs/release-notes-2.7.0.md style), commit+push, dotnet publish Release (bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk + .aab), gh release create v2.8.0 --notes-file docs/release-notes-2.8.0.md <apk> <aab>, deliver result.

## Phase 5 progress (security):
- LegacyBackupParser.Parse now sanitizes all text fields via InputSanitizer and stamps IntegrityHash on every imported ProfitEntry (ComputeProfitEntryHash(entryId, userId, version, sales, cost, expenses, isDeleted); signature args must be positional+named exactly as: entryId, userId, version, sales, cost, expenses, isDeleted).
- NEXT: build all, write V280EngineTests (IntegrityHash roundtrip + sanitize-on-import + aging + data quality ~28 tests), bump version 2.8.0 / versionCode 10, release-notes-2.8.0.md, commit+push, gh release create v2.8.0 --notes-file docs/release-notes-2.8.0.md bin/Release/net10.0-android/publish/com.maen.accounting-Signed.apk bin/Release/net10.0-android/publish/com.maen.accounting-Signed.aab. Note: personal backup import also needs integrity hash on upsert (rows generate hash in FromModel — already covered since rows auto-populate hash on save).

## Next: phase 3 (features) — Engines to build in Core:
- DebtAgingCalculator: buckets (current, 1-30, 31-60, 61-90, 90+) from ReconciledInvoice (uses InvoiceReconciliationCalculator.Reconcile(invoices, payments) → ReconciledInvoice(Invoice, PaidMinor, OutstandingMinor, IsFullyPaid); invoice fields: Invoice.IssueDate, TotalMinor, Type: InvoiceType.Sales|Purchase).
- DataQualityChecker: warnings list (draft invoices count, negative entries, missing-category entries, unposted journals, contacts without invoices, payments>total mismatch).
- Wire UI: DataQuality card on dashboards, aging section on BusinessReportsPage or dashboard.
- T-keys T543+ needed in UiText.cs.
- Tests V280EngineTests.cs (~25-30).
- Version bump 2.8.0 / versionCode 10 in Maen.Accounting.App.csproj; release-notes-2.8.0.md; tag v2.8.0; gh release with apk+aab.

## Remaining TODO (in order)
1. Migration 7 in SchemaMigrationCatalog.cs + UserDatabaseFactory.cs: `ALTER TABLE payments ADD COLUMN IntegrityHash TEXT NOT NULL DEFAULT '';` + invoices + profit_entries (3 ALTERs, guard via PRAGMA table_info pattern used in v6/v2).
2. BusinessDocumentValidator: currently exists in Core (ValidateInvoice/ValidatePayment) but NOT wired in BusinessViewModel save paths → wire into Invoice/Payment save (BusinessViewModel) like lifecycle rules.
3. New Core engines:
   - DebtAgingCalculator: buckets (current, 1-30, 31-60, 61-90, 90+) for receivables (sales posted) & payables (purchases posted), uses reconciled paid amounts (InvoiceReconciliationCalculator.Reconcile).
   - DataQualityChecker: drafts count, negative-amount entries, entries missing category, contacts with no invoices, unposted journals; return list of warning items (code + bilingual T-keys in UiText).
   - InputSanitizer (Core): truncate notes 300, name 80, number 40; strip control chars. Apply in BusinessViewModel/MainStateViewModel saves.
4. UI: Data Quality card on dashboards (MainStateViewModel + BusinessViewModel) + new section; aging summary card on Business dashboard/reports; parallelize heavy dashboard loads in MainStateViewModel (LoadAsync tasks in parallel, e.g. annual report + category report + trend).
5. T-keys T543+ in UiText.cs for new labels (Arabic/English).
6. Tests: V280EngineTests.cs (~25-30) for DataIntegrityService, DebtAgingCalculator, DataQualityChecker, InputSanitizer.
7. Bump version 2.8.0 / build 10; release-notes-2.8.0.md; commit/push; tag v2.8.0; gh release with apk+aab.

## Key facts
- UiText pattern: `["T543"] = ("العربية", "English"),` in UiText.cs dictionary.
- BusinessViewModel ~510 lines, uses T-keys via UiText.Get("T###"), UiText.Format("T###", args).
- MainStateViewModel 1378 lines; dashboard loads via LoadAsync (search "LoadAsync" for parallelization point).
- InvoiceReconciliationCalculator.Reconcile(invoices, payments) returns List<ReconciledInvoice>(Invoice, PaidMinor, OutstandingMinor, IsFullyPaid); HasOutstanding property.
- BusinessDocumentLifecycleService.Result type (Success/Fail). InvoiceBuilder in same class.
- ObservableObject base in App.Infrastructure.
- SmallButton style added to App.xaml in v2.7.0. Styles: PrimaryButton, SecondaryButton, SmallButton, CardBorder, MetricCard, SectionTitle, MutedLabel, ListItemBorder, HeroCard, EmptyStateBorder, TitleLabel, Danger color.
- Money.Format(minor) for display.
- Personal tab pages in PersonalTabbedPage; Business tabs in BusinessTabbedPage.
- FirebaseOptions.ApiKey hardcoded "AIzaSy..." (acceptable for Firebase REST; real gate = rules). No client-side tamper hash until now → DataIntegrityService fills gap.
- T538 = success/loaded message used in reports; T121 generic error; T080 empty/nothing.
- Migration 6 (v2.7.0): payments IsDeleted column.
- Tests project: tests/Maen.Accounting.Core.Tests, pattern files V250/V260/V270EngineTests.cs.
