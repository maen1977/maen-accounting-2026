# v2.8.0 — "Maximum Elevation" Plan

Goal: Raise Maen Accounting to the highest bar across all criteria (Architecture, UI/UX, Features, Performance, Security).

## Audit findings (current v2.7.0, 11,185 lines)

### Architecture
- BusinessDocumentValidator exists but is NOT wired into BusinessViewModel save paths (only lifecycle rules used).
- No centralized input sanitization/validation helper for free-form text fields (notes, names).
- MainStateViewModel is 1,378 lines — large but MVVM-single-VM is established pattern; extract helpers only where safe.
- No data-integrity checksum on local backup files beyond legacy parser; backup export not integrity-signed.

### Security
- Firebase ApiKey hardcoded in source (Firebase convention: fine for REST API keys, but we should add App Check-like request signing note; low-cost action: keep as-is with docs, since Firebase rules are the real gate). ACTION: verify rules enforcement + add client-side tamper hash to Firestore document writes.
- No write-integrity hash (HMAC/SHA) on synced entries -> remote DB cannot detect tampered uploads; add `IntegrityHash` to entries in v280 migration + Firestore round-trip uses same hash (client verifies its own writes).
- Local SQLite has no key (sqlite-net without SQLCipher). ACTION: enable PRAGMA journal_mode=WAL for performance + integrity via checksum in backup.

### Performance
- No WAL pragma tuning; no index definitions beyond PK. ACTION: add composite indexes in migration 7 (UserId+EntryDate, UserId+Category, invoices userId+status, payments userId+isdeleted).
- Full-table scans in reports: ensure calculators filter pre-aggregated (they mostly do); Dashboard loads 6 heavy reports serially on startup — ACTION: parallelize independent loads (LoadAsync tasks run in parallel where no contention).
- BusinessReportsPage recalculates on every LoadAsync — fine.

### Features (biggest delta opportunity)
1. General Ledger journal entry posting already exists (JournalEntry models + JournalEntryValidator) but check UI exposure (AccountingPage). Verify & strengthen.
2. Cash Register / Petty Cash (الخزينة/الصندوق): daily open/close with variance detection. High-value, unique for free app.
3. Debt Aging (أعمار الديون): receivables/payables aging buckets (current, 30/60/90, overdue) for business — extension of reconciliation.
4. Break-even & margin analytics for invoices (per-invoice margin using purchase+sales prices).
5. Export/import Excel-like CSV with integrity hash for all reports.
6. Data quality dashboard: missing contacts, drafts count, unposted entries, negative amounts warnings.

### UI/UX
- App.xaml styles already rich (CardBorder, MetricCard, etc.). Add: StatusDot (success/warning/error), EmptyState illustrations via glyph, overdue-red highlights already exist (OverdueInvoiceItem).
- Add "Data Health" card on business dashboard (drafts, unsigned entries, overdue count, aging summary).
- Consistent RTL/Arabic polish: verify all new keys added with Arabic first (UiText pattern).

## v2.8.0 work items
Phase 1 (done): audit.
Phase 2: IntegrityHash service (SHA-256 HMAC-lite deterministic) + entry migration 7 + apply to repositories & Firestore writes; WAL pragma + migration indexes.
Phase 3: AgingCalculator + BreakEvenCalculator + DataQuality service in Core.
Phase 4: UI: Data Health card (personal + business dashboards), aging tab/section, parallel dashboard loads, Cash Register page (Personal tab or business? -> business: cash register with daily sessions), DataQualityPage as reports submenu.
Phase 5: tests (~30+) + version 2.8.0 build 10 + release.
