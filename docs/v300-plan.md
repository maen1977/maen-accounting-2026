# v3.0.0 Plan — Raise the Program by Every Standard

Current state: v2.9.0 released (235 tests). Gaps identified for world-class status:

1. **Recurring personal movements (auto-schedule)** — salary deposits & rent already planned, but no "recurring personal entries" engine that auto-records periodic income/spending. → `RecurringMovementCalculator` engine + migration 9 + recurring tab entries auto-applied on reload + UI in PlanningPage.
2. **Memo/attachment support on entries** — users want proof (receipt photos) & notes on movements. Photo = base64 blob in SQLite (local-first) + notes text. → `EntryMemo` column set (memo text, has_photo flag, photo blob) + migration 9 + PersonalEntryPage capture via MediaPicker.
3. **Spending-by-category analytics** — category trend chart data (spend over months per category) already exists partially; add a `TopCategoryAnalyticsEngine` with share/percent-of-total, best+worst months → card on ReportsPage.
4. **SettingsPage audit & integrity summary** — record counts, verified hashes, last sync time (per v2.9.0 plan item 8). → SettingsViewModel audit section.
5. **Performance** — async paging for ledger list; cache summary in AppPreferences after reload; Task.WhenAll already done. Add lazy-load of entries (>500 rows virtualization already in CollectionView; ensure `ItemsUpdatingScrollMode="KeepItemsInView"`).
6. **Security** — input sanitizer + integrity already; add `AuditTrailCalculator` (count of edits/deletes per period) visible in Settings; export CSV integrity hash line.
7. **UX polish** — empty-state illustrations (BrandHeaderView HasStatus already), confirmation dialogs on delete, success toast on save; category registry colors already exist.
8. **Tests** — ~40 new tests; total ≥ 275. Version 3.0.0 Build 12.

## Scope decision
- Engines (Core): RecurringMovementCalculator, TopCategoryAnalyticsEngine, AuditTrailCalculator.
- Data: migration 9 (memos table: memo_id, entry_id, note_text, has_photo, photo_blob), RecurringMovementRow (recurring_movement_id, title, category, amount_minor, kind(income/expense), start_date, cycle(monthly/weekly), next_occurrence).
- UI: memo on PersonalEntryPage; recurring movements in PlanningPage (add/delete/list); audit card in SettingsPage; analytics card on ReportsPage.
- ~30 T-keys (T700-T735).
