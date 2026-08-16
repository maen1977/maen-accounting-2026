# Maen Accounting v3.1.0 — "Full Spectrum" Plan

The v3.0.0 release delivered automation, attachments, category analytics, and audit transparency. The v3.1.0 wave pushes the program to the maximum across all remaining standards: budgets with smart alerts, multi-currency awareness, export-grade reporting, search/command capability, and security hardening.

## Existing Gaps Identified (v3.0.0 audit)
- Budget exists only as UI text keys (T181-T188) with no data model, engine, or persistence.
- Currency is hardcoded (single local currency, amounts in minor units); no FX awareness.
- Exports are CSV only (ReportCsvExporter / BusinessReportCsvExporter); no PDF or Excel.
- No search or quick-command capability across entries.
- No debt/savings goal integration on the personal dashboard beyond separate pages.
- Audit trail exists but no per-entry "changed on device" trace in UI.
- No idle lock / app-lock with PIN (security hardening gap).
- Recurring movements have no preview of upcoming occurrences.

## v3.1.0 Scope (8 engines + data layer + UI + tests)

### A. Budget Engine (BudgetPlannerCalculator)
- Model: MonthlyBudget (per-month cap, optional per-category caps), stored in Migration 10 table `BudgetPlans`.
- Engine computes: remaining budget, usage percent, projected end-of-month spend (average daily run-rate × days remaining), alert thresholds (80% warning, 100% exceeded).
- UI: Budget card on PlanningPage with projected-out-of-budget forecast.

### B. Multi-Currency Awareness (CurrencyProfile / CurrencyConverter)
- Model: CurrencyProfile table (Migration 10) holding user currencies (code, symbol, symbol direction) with one default.
- Engine: CurrencyConverter with fixed offline rate table (user-editable rates per user), converts between entries' currency and display currency, never online.
- ProfitEntry gains CurrencyCode column (Migration 10); reports total by display currency.

### C. Report Export Suite (PdfReportExporter)
- PDF export of personal monthly report, business invoice, and general ledger (using ReportDocument abstraction + weasyprint-compatible HTML → PDF via system wkhtmltopdf if available; fallback to simple text PDF via library).
- Implementation: generate HTML string from existing report text structures and render with a self-contained approach: use `QuestPDF` alternative unavailable → use `SkiaSharp`/`System.Drawing` too heavy → choose **weasyprint** (installed) invoked via shell with generated HTML file; cross-platform safe on build agent.
- Fallback: keep CSV exporters unchanged.

### D. Global Search Engine (MovementSearchEngine exists → extend)
- Extend existing MovementSearchEngine with: entry ID search, attachment presence filter, recurring-source filter, date range quick filters ("هذا الشهر"/"آخر 30 يوم").
- UI: search field on ReportsPage results.

### E. Upcoming Occurrences Preview (RecurringMovementCalculator)
- Add PreviewUpcoming(movements, asOf, count) returning next N occurrences per active movement.
- UI: card on PlanningPage listing next 3 due movements with days-until.

### F. App Lock (Security hardening)
- Simple PIN lock: encrypted storage of PIN hash (SHA256 + per-device salt), enforced at app resume after idle (>=5 min).
- Settings card to enable/disable and change PIN; bilingual prompts.
- Stored via SecureStorage (MAUI platform) — no database dependency.

### G. Data Quality Improvements
- Duplicate detection at save time (same amount + date + category + counterparty within window) — engine DuplicateDetector.
- Warn (never block) on potential duplicate.

### H. Audit UI Trace
- Settings audit card shows top-5 recently mutated entries (user + device + timestamp + version) already provided by AuditTrailCalculator — wire UI rows.

## Data Layer (Migration 10)
- Table `BudgetPlans` (UserId, YearMonth int, AmountMinor, PerCategoryJson nullable, IsActive).
- Columns on ProfitEntry: CurrencyCode (default "JOD"), DisplayCurrency override on user settings row.
- Table `CurrencyRates` (FromCode, ToCode, RateNumerator, RateDenominator, UpdatedAtTicks).

## Tests
- New V310EngineTests.cs covering budget run-rate projection, currency conversion round-trips and rate safety, duplicate detection window logic, upcoming occurrence preview, PIN verification, CSV→PDF smoke.
- Target total: ≥ 300 tests, all passing.

## Version
- ApplicationDisplayVersion: 3.0.0 → 3.1.0
- ApplicationVersion: 12 → 13
- Tag: v3.1.0, release with signed APK + AAB.
