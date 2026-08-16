# Maen Accounting Grand Release Plan — v2.5.0

## Vision
Make Maen Accounting the most generous free accounting program for individuals and businesses: broader menus, unique one-tap experiences, and deep accounting capability that rivals paid software — while keeping the strict personal/business separation, bilingual experience, and local-first + cloud-sync model.

## Scope for v2.5.0 (The Grand Release)

### 1. Personal ledger expansion (Core — pure C#)
- `PersonalFinancialPlanCalculator`: monthly budget plan (income vs expected spending), savings target tracking, and obligation deadlines.
- `PersonalAssetTracker`: multi-account net worth (cash wallets + bank accounts + optional other assets), reconciliation per account.
- `BusinessFinancialSummaryCalculator`: cash position, receivables, payables, due-date buckets (overdue / this week / this month), revenue vs expenses per period.
- `MovementSearchEngine`: fast keyword/date/category search over personal entries with result ranking.

### 2. Unique UX features (App layer)
- Smart Dashboard (personal): hero card (available + bank), plan progress, upcoming obligations ticker, recent timeline with search button.
- Business dashboard rework: position card, due-date buckets, top contacts by balance.
- Quick-action command palette: single screen with all frequent actions for the current mode (record salary, purchase, transfer, invoice, payment...).
- Global search bar (personal): find entries, amounts, categories.
- Category management page (personal): add/edit color-coded categories with usage counts.
- Empty states with guided first actions in every list.

### 3. Data and sync
- SQLite migration 5: `financial_plans`, `obligations`, `category_registry` (personal) + business due-date support already present.
- Firestore sync of `plans` and `obligations` for personal scope; business sync already covers contacts/invoices/payments.
- Sync summary now includes plan/obligation counters.

### 4. Testing
- `PersonalFinancialPlanCalculatorTests`, `BusinessFinancialSummaryCalculatorTests`, `MovementSearchEngineTests`.
- Migration catalog test update for migration 5.
- Target: 60+ passing core tests.

### 5. Release gates
- Core tests green → Android Debug → Release publish → GitHub commit + release v2.5.0 with APK/AAB.
