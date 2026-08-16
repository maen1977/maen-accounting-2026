# v2.7.0 Progress (update 4 — Phase 3 DONE)

## BusinessDocumentLifecycleService (Core) API (confirmed)
- Result struct: Ok (bool), Reason (string); Result.Success() / Result.Fail(reason). NO IsFailure/Value/ErrorMessage.
- PostInvoice(invoice), VoidInvoice(invoice, payments) [only Posted, no related payments by ContactId], DeleteInvoice(invoice, payments) [only non-Posted, no related payments], DeletePayment(payment, invoices) [CustomerReceipt only blocked if posted sales for contact], DeactivateContact(contact, invoices) [no posted invoices for contact].
- BuildNextVersion(invoice, Action<InvoiceBuilder>): builder.SetLines, AddLine, SetTaxMinor, SetIssueDate, SetDueDate, SetNumber, SetNotes, Build(). Returns invoice with Version+1, UpdatedAtUtc=now (no Type/ContactId changes).
- InvoiceStatus: Draft, Posted, Voided (Draft→posted via PostInvoice, only Draft can be posted).

## BusinessViewModel additions DONE (src/.../ViewModels/BusinessViewModel.cs)
- Fields: _invoiceSearchText/_paymentSearchText/_contactSearchText, _editingInvoice.
- Properties: InvoiceSearchText/PaymentSearchText/ContactSearchText (→ ApplyFilters), FilteredInvoices/FilteredPayments/FilteredContacts (ObservableCollections), ReconciledInvoices (ObservableCollection<ReconciledInvoiceItem>), InvoiceFormTitle (T074 or T488 Edit).
- ReloadCoreAsync ends with RebuildReconciliation(); ApplyFilters().
- SaveInvoiceAsync: if _editingInvoice → SaveEditedInvoiceAsync (BuildNextVersion via SetNumber/SetIssueDate/SetDueDate/SetTaxMinor/SetLines(new InvoiceLine(editing.Lines[0].LineId,...))) then clear inputs + OnPropertyChanged(InvoiceFormTitle).
- EditInvoice(InvoiceItemViewModel item): only Draft; populates inputs, SelectedInvoiceType, SelectedInvoiceContact, dates.
- VoidInvoiceAsync: Validate VoidInvoice, then voided = invoice with { Status = InvoiceStatus.Voided, Version+1, UpdatedAtUtc, DeviceId } → UpsertInvoiceAsync. Status T295 needed.
- DeleteInvoiceAsync: validate DeleteInvoice → _repository.DeleteInvoiceAsync(userId, invoice.InvoiceId) [TO ADD to repo]. T296.
- DeletePaymentAsync: validate DeletePayment → _repository.DeletePaymentAsync(userId, payment.PaymentId) [TO ADD].
- ToggleContactAsync: IsActive toggle (with IsActive true use DeactivateContact validation) → UpsertContactAsync(contact with { IsActive = !contact.IsActive, Version+1, ... }). T297.
- RebuildReconciliation: InvoiceReconciliationCalculator.Reconcile → ReconciledInvoiceItem.
- ApplyFilters: filters Invoices/Payments by Number/ContactText contains; Contacts by Name contains. Uses item.ModelInvoiceId/ModelPaymentId/ContactId [MUST ADD to ItemViewModels].

## ItemViewModels edits DONE — see below.
## PersonalDashboardPage.xaml filters card (T514) DONE:
- Implemented: card w/ clear button (HasMovementFilter + ClearMovementFilter), category Picker, 2 DatePickers, summary row Deposits/Spending/Net (T519/T520/T521). MainStateViewModel: HasMovementFilter, _defaultFilterFromDate (before property def), ClearMovementFilter(), OnPropertyChanged in setters/RebuildCategories. PersonalDashboardPage.xaml.cs: OnClearMovementFilterClicked.

## Business UI (Phase 2) DONE & builds OK:
- App.xaml: SmallButton style added.
- BusinessInvoicesPage.xaml/.cs: search card (InvoiceSearchText), FilteredInvoices, Edit/Void(Draft)+Delete buttons w/ confirm (T491/T492/T494/T495/T496), ReconciledInvoices card (T511).
- BusinessPaymentsPage.xaml/.cs: search card (PaymentSearchText), FilteredPayments, Delete button (T493).
- BusinessContactsPage.xaml/.cs: search card (ContactSearchText), FilteredContacts, ToggleLabel button → ToggleContactAsync.
- BusinessRepository: DeleteInvoiceAsync / DeletePaymentAsync added. UserDatabaseFactory migration [6] + SchemaMigrationCatalog new(6, ...). BusinessViewModel uses T523/T524/T525 messages. ItemViewModels: ModelInvoiceId/ModelPaymentId/IsDraft/ToggleLabel/IsActive/RemainingColor/StatusText.

## BusinessReportsPage TODO (Phase 4)
- New page BusinessReportsPage.xaml/.xaml.cs + ViewModel (BusinessRepository, auth). Report: BusinessMonthlyReportCalculator.Build(rawInvoices, rawPayments, from, to) → per-month slices (SalesMinor/PurchasesMinor/ReceiptsMinor/SupplierPaymentsMinor/NetCashMinor/PostedInvoiceCount), BestMonth/WorstMonth, totals. Register DI + add tab T503 in BusinessTabbedPage ctor. CSV export using T537/T538. Keys: T503/T531 (Business Reports), T504-T510 (Monthly Sales/Purchases/Receipts/Supplier Payments/Net Cash Flow/Best Month/Total), T539 Monthly Report.

## BusinessViewModel notes (from update 3, still relevant)
- _editingInvoice + SaveEditedInvoiceAsync uses BuildNextVersion (InvoiceBuilder: SetLines/AddLine/SetTaxMinor/SetIssueDate/SetDueDate/SetNumber/SetNotes/Build).
- PostInvoice only Draft→Posted; Void only Posted; Delete only non-Posted w/ no related payments (validate via BusinessDocumentLifecycleService).

## Build/env
- dotnet ~/.dotnet/dotnet; Android: -p:AndroidSdkDirectory=/home/ubuntu/android-sdk.
- gh logged in. Publish: dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release -p:AndroidSdkDirectory=/home/ubuntu/android-sdk (+ repeat with -p:AndroidPackageFormat=aab). Outputs bin/Release/net10.0-android/com.maen.accounting-Signed.{apk,aab}.
- csproj version: currently 2.7.0? verify (v2.6.0 was 2.6.0/versionCode 8) → bump to 2.7.0/9 when releasing.
- Tests V270EngineTests.cs (30+) for 4 engines, total currently 144 passing.
- Release: gh release create v2.7.0 with apk+aab; URL https://github.com/maen1977/maen-accounting-2026/releases/tag/v2.7.0; docs/release-notes-2.7.0.md; final message with attachments.

## Sequence of pages for business tab (BusinessTabbedPage.cs ctor): contactsPage, invoicesPage, paymentsPage — add reportsPage, Title T503.
