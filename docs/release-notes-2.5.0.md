# Maen Accounting v2.5.0 — Grand Release

The biggest release yet for Maen Accounting. This version turns the app into a full financial-planning companion for individuals, adds a financial-position card for businesses, and ships an advanced search engine — while keeping the strict separation between personal and business accounts, full Arabic/English bilingual support, and local-first saving with explicit cloud sync.

## What's New

### Financial Planning (Personal)
- **Monthly Financial Plan**: set your expected monthly income, spending ceiling, and savings target (e.g., salary 500 — spend 350 — save 150).
- **Per-Category Spending Limits**: define a monthly budget cap for each category (food, transport, bills...) and see utilization progress bars.
- **Plan Progress Card** on the Personal Dashboard shows actual income vs. expected, spending vs. limit, and savings progress percentage.
- New **Planning & Deposits page** (3rd tab) to manage plans, recurring obligations, and manual deposits in one place.

### Recurring Obligations
- Track rent, loan installments, subscriptions, and recurring bills with daily / weekly / monthly / yearly cycles.
- Automatic schedule engine classifies upcoming dues as **overdue**, **due soon**, or **future**, and records past paid occurrences per cycle period.
- Obligation ticker on the Personal Dashboard with color-coded status.

### Bank Deposits
- Manual cash-deposit tracking: record deposits to your bank/cash holdings with date, amount, and description.

### Advanced Movement Search
- Global search bar on the Personal Dashboard: filter entries by **keyword** (notes, category, counterparty, wallet, movement type), **date range**, and **movement types**.
- Results sorted newest-first.

### Category Registry
- New card on the Personal Dashboard showing every category you actually use, with its assigned color, entry count, and last-used date. Categories are added automatically as you use them.

### Business Financial Position
- New **Financial Position card** on the Business Dashboard: receivables, payables, net position, overdue sales, and overdue purchases — computed from posted invoices and payments.
- Due-date buckets: **due soon** (within 7 days) and **overdue** for both sales and purchases.
- Top contacts by balance (top 5).

### Database & Sync
- Schema migration 5 adds `financial_plans`, `obligations`, and `deposits` tables with sync indexes.
- New `PersonalFirestoreSyncService` syncs plans, obligations, and deposits to the cloud alongside personal entries — with full conflict resolution (local wins / remote wins reporting).
- Cloud sync summary now reports personal planning entities (plans, obligations, deposits) as well as business entities, so you always know what was stored where.

### Language & Localization
- 68 new bilingual (Arabic/English) UI keys (T395–T462) covering planning, obligations, deposits, search, and the financial position card.

## Quality
- **118 passing unit tests** (71 new) covering all five new core engines:
  - `MovementSearchEngine` — keyword/date/type/category search
  - `PersonalFinancialPlanCalculator` — plan tracking and validation
  - `ObligationScheduleCalculator` — cycle scheduling and paid-occurrence detection
  - `BusinessFinancialSummaryCalculator` — receivables, payables, net position, due buckets
  - `CategoryRegistryCalculator` — automatic category registry with palette colors

## Notes
- Local-first: nothing leaves your device until you press **Sync**; the summary message tells you exactly what was uploaded locally and to the cloud.
- Personal and business data remain strictly separated — each has its own database, pages, and cloud collections.
