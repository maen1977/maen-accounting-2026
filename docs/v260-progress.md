# v2.6.0 Progress State (keep updating)

Full plan: docs/v260-plan.md. Last release: v2.5.0 (commit a872a47 pushed).

## DONE
1. 3 new Core engines compiled OK:
   - PersonalAnnualReportCalculator.cs: Build(entries, year) -> PersonalAnnualReport(Year, Months[12] PersonalMonthlySnapshot{Year,Month,IncomeMinor,SpendingMinor,NetMinor}, TotalIncomeMinor, TotalSpendingMinor, TotalNetMinor, BestMonth, WorstMonth) + static YearOverYearIncomeChangePercent(currentMonths, previousMonths).
   - PersonalCategoryReportCalculator.cs: Build(entries, DateOnly month) -> CategoryReport(Year, Month, Breakdowns CategoryBreakdown{Category,EntriesCount,SpentMinor,Rank}, TotalSpendingMinor) with SharePercentFor(breakdown).
   - SavingsTrendCalculator.cs: Build(entries, asOfDate, trailingMonths, FinancialPlan?) -> SavingsTrend(ReferenceYear, ReferenceMonth, Points SavingsTrendPoint{Year,Month,IncomeMinor,SpendingMinor,SavedMinor,TargetMinor,IsOnTarget}[trailingMonths], TotalSavedMinor, MonthsOnTargetEligible, MonthsOnTarget, ConsecutiveOnTargetStreak).
2. UiText.cs keys T464-T483 added (T463 already existed). NEW keys used in bindings: T348 (need verify!), T349, T351 — CHECK these exist; also Money.Format/Money.ToDecimal, PersonalPlanProgress.ExpectedIncomeMinor/ExpectedSpendingLimitMinor/ExpectedSavingsTargetMinor, PlanCategoryLimit, PaletteColor (need create!), CategoryBreakdownItem + SavingsTrendPointItem VM classes (need create).
3. ReportsPage.xaml: added 3 new card sections (annual report grid T464/T465/T466/T467/T468/T469/T470; category breakdown T471 with CategoryBreakdownItems; savings trend T474 with SavingsTrendPoints) + export button changed: first grid now uses T478 (Export Report) -> OnExportPersonalCsvClicked. Existing export CSV (business) button was replaced — MUST restore business CSV export (T376 key) in BusinessExperience section or keep only new one. Actually replaced the T376 button; business CSV export still available? The business export uses same ExportAsync path via OnExportCsvClicked -> BuildReportCsv (business reports). DECISION NEEDED: restore second export button under business grid or add it there.
4. ReportsPage.xaml.cs: added OnExportPersonalCsvClicked handler calling _state.ExportPersonalReportCsvAsync() + ShareFileRequest.
5. MainStateViewModel.cs added: fields _annualReport/_yearOverYearChangePercent/_categoryReport/_savingsTrend + ObservableCollection<CategoryBreakdownItem> _categoryBreakdownItems + _savingsTrendPoints; properties HasAnnualReport, AnnualIncomeText, AnnualSpendingText, AnnualNetText, AnnualNetColor, BestMonthText/WorstMonthText (use T348 format {0} month {1} amount!), YearOverYearText (T481/T483/T351), YearOverYearColor, HasCategoryBreakdown, CategoryBreakdownItems, HasSavingsTrend, TrendStreakText (T349 format {0} count {1} label), SavingsTrendPoints, YearMonthName helper; RebuildAnnualReport(entries) called in ReloadAsync after RebuildReport and before RebuildPlanningAsync; ExportPersonalReportCsvAsync() (writes annual-{year}.csv to FileSystem.CacheDirectory with T021/T465/T466/T467 headers + totals row, escapes csv); RaiseAnnualReportProperties() called inside RaiseSummaries(); ToPlanModelFromProgress.

## STATUS (latest)
- App builds Debug 0 errors. CategoryBreakdownItem/SavingsTrendPointItem/PaletteColor created in ReportDetailItemViewModels.cs. T484 added. BestMonth/WorstMonth use FormatMonthLabel helper. ExportPersonalReportCsvAsync writes CacheDirectory annual-{year}.csv.
- UI: ReportsPage has annual grid + category breakdown (8 items) + savings trend + export button replaced T376 with T478 — need to consider restoring business CSV export button (currently only personal export). Personal tabbed? ReportsPage is inside business + personal? Actually reports exist in both experiences via MainStateViewModel; check PersonalTabbedPage has Reports tab.

## STATUS (updated 2)
- All tests pass: 144 total (118 old + 26 new V260EngineTests). Release build 0 errors.
- Business overdue details implementation in progress: DashboardPage.xaml financial position card needs expandable overdue list (T480/T482). BusinessViewModel has OverdueSalesText/OverduePurchasesText from _financialPosition (BusinessFinancialSummary). Need: add OverdueSalesDetail/OverduePurchasesDetail (Invoice[]) to BusinessViewModel + UI CollectionView in DashboardPage under the 5-metric grid, toggle via HasOverdueInvoices bool.
- BusinessFinancialSummaryCalculator.Summarize(contacts, invoices, payments, asOf) — check if it already exposes overdue invoice lists; if not, filter in BusinessViewModel: invoices with Status==Posted && DueDate < asOf && unpaid amount > 0 (unpaid = TotalMinor - payments toward that invoice). Simplify: overdue = Posted invoices due before asOf with outstanding minor > 0 where outstanding = TotalMinor - sum(payments for that invoice, CustomerReceipt reduces sales invoices, SupplierPayment reduces purchase invoices).
- Remaining then: csproj 2.6.0 + versionCode 8, docs/release-notes-2.6.0.md, git commit+push, publish Release, gh release v2.6.0 with signed apk/aab.

## STATUS (updated 3 — critical discovery)
- CRITICAL: DashboardPage.xaml.cs BindingContext = MainStateViewModel ONLY (no BusinessViewModel in ctor). The T413 financial position card binds HasFinancialPosition/ReceivableNetText/OverdueSalesText etc. — MainStateViewModel does NOT have these properties! In v2.5.0 the card would never show. FIX: add proxy properties in MainStateViewModel: _businessSummary (BusinessFinancialSummary?) field, RebuildBusinessFinancialPosition() called in ReloadAsync (invoices+payments all, contacts all), HasFinancialPosition/ReceivableNetText/PayableNetText/NetPositionText/NetPositionColor/OverdueSalesText/OverduePurchasesText + OverdueSalesInvoices/OverduePurchasesInvoices (ObservableCollection<OverdueInvoiceItem>) + HasOverdueInvoices + IsOverdueExpanded + ToggleOverdueExpanded. Then DashboardPage.xaml.cs OnToggleOverdueClicked handler is fine.
- RebuildOverdueInvoices moved into MainStateViewModel (same logic as BusinessViewModel version, paymentByContact). OverdueInvoiceItem class stays in BusinessViewModel.cs (accessible). Invoice has NO IsDeleted property (Status==Voided instead). Payment has no IsDeleted.
- T485 = "اليوم"/"today", T486 = "متأخر {0} يوم"/"{0} days overdue" (need to add to UiText).
- OnToggleOverdueClicked: private void in DashboardPage.xaml.cs => _state.ToggleOverdueExpanded().
- Then: business export button restored (done), tests 144 pass (done), Release builds (done).
- Remaining: add T485/T486, MainStateViewModel proxy + RebuildBusinessFinancialPosition in ReloadAsync + Raise properties, build, re-test, then release v2.6.0.

## PENDING
1. Create CategoryBreakdownItem and SavingsTrendPointItem viewmodel classes (in Views or ViewModels namespace; CategoryBreakdownItem: CategoryText, SummaryText (T472/T473: "{0} entries, {1}% of spending"), SpentText (Money.Format), ShareProgress, CategoryColor (Color)); SavingsTrendPointItem: MonthText (short month name), SavedText (Money.Format w/ sign), StatusText (T475/T476), SavedProgress (0..1 clamped against maxAbsSaved in VM, passed to ctor), TrendColor (#137A53 if IsOnTarget else #64748B... use SavedMinor>=0?).
2. Create PaletteColor.ForRank(rank) utility (colors: #C8A45D gold, #48D597 green, #5D9BD3 blue, #D98FBF pink, #8B7FD4 purple, #E0A84C amber, #6EC8C8 teal, #C2413A red) — maybe static class in Views or Infrastructure.
3. Verify T348, T349, T351 exist in UiText (grep). If not: add (e.g. T348 = ("{0}: {1}", "{0}: {1}"), T349 = ("{0} شهر متتالي على الهدف", "{0} consecutive months on target"), T351 = ("لا تغيير", "No change")).
4. Build Debug; fix errors. Add AnnualReportCsvExport button restore for business? Optional: add second export button T376 inside IsBusinessExperience grid OR in header show both (keep current).
5. Business dashboard overdue detail (optional but in plan): DashboardPage.xaml financial position card add expandable overdue list (T480/T482) — BusinessViewModel needs OverdueSales/Purchases detail lists (Invoice records with DueDate < asOfDate, Status==Posted, unpaid). Implement if time permits.
6. Tests: tests/Maen.Accounting.Core.Tests/V260EngineTests.cs — 25+ tests (annual 12 months/totals/best-worst/YoY; category grouping/ranking/share; trend points/target/streak).
7. csproj 2.6.0 versionCode 8. docs/release-notes-2.6.0.md. commit+push main. gh release v2.6.0 with signed apk/aab from src/Maen.Accounting.App/bin/Release/net10.0-android/publish/com.maen.accounting-Signed.{apk,aab}.
   gh command: gh release create v2.6.0 -R maen1977/maen-accounting-2026 --title "Maen Accounting v2.6.0" --notes-file docs/release-notes-2.6.0.md apk aab

## Build env
PATH=/home/ubuntu/.dotnet:/home/ubuntu/android-sdk/cmdline-tools/latest/bin:/home/ubuntu/android-sdk/platform-tools:$PATH; JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64; ANDROID_HOME=ANDROID_SDK_ROOT=/home/ubuntu/android-sdk. test: dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj (118 pass). App: dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Debug.

## Key patterns
- ObservableObject in Maen.Accounting.App.Infrastructure (SetProperty<T>, OnPropertyChanged). NO record inheritance.
- Money.TryParse(text,out minor), Money.Format(minor), Money.ToDecimal(minor) -> decimal (verify method name exists! used Money.ToDecimal in MainStateViewModel already line ~862 so OK).
- Ticks are DateTime ticks: new DateTime(ticks, DateTimeKind.Utc) for DB rows.
- ProfitEntry props: SalesMinor/CostMinor/ExpensesMinor/EffectiveAmountMinor/NetProfitMinor/IsIncome/IsOutflow/MovementType/Category/Wallet/Counterparty/Notes/EntryDate/IsDeleted.
- MainStateViewModel uses _entries via Entries (ObservableCollection<ProfitEntryItemViewModel>) and Models() => Entries.Select(item => item.Model).
- Existing report CSV: ReportCsvExporter.Build needs exactly 10 headers (T378-T387).
- XAML styles: CardBorder, SectionTitle, MutedLabel, TitleLabel, HeroCard, PrimaryButton, SecondaryButton, DangerButton; colors: PrimaryDark, Danger (#C2413A?), gold #C8A45D, green #48D597/#137A53, navy #0B172A; namespace local=Maen.Accounting.App with Tr markup.
- CategoryRegistryItem/Palette/PlanCategoryLimit — check CategoryRegistryCalculator for palette color source (it assigns colors) to reuse for CategoryBreakdownItem.
