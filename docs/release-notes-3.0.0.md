# Maen Accounting 3.0.0 — "Maximum Limits"

The 3.0.0 release pushes Maen Accounting beyond the previous standard in four dimensions: automation, rich data, deep analytics, and transparency — while keeping the local-first, high-security architecture intact.

## What's New

### Recurring Movements (Automation)
Personal income and expenses that repeat on a schedule — salary deposits, rent, subscriptions — are now recorded automatically. A new `RecurringMovements` table (Migration 9) stores movements with a cycle (daily / weekly / monthly / yearly), an amount, and a kind (income / expense / purchase / deposit). When the app opens, any due movement whose due date is at most `MaxMissedDays` (7 days) behind is applied to the ledger and advanced to its next occurrence; older stale occurrences are skipped so the ledger is never flooded with missed cycles.

### Photo Attachments (Rich Data)
Every personal entry can now carry a photo attachment (receipts, invoices, documents). The image is captured through a `FilePicker`, compressed/resized, Base64-encoded, and stored directly inside the local SQLite database in the new `AttachmentBase64` column on `ProfitEntry`. Attachments survive backup/restore flows and remain fully offline — nothing is uploaded anywhere.

### Category Analytics (Deep Analytics)
The Reports page now surfaces a Category Analytics card powered by the new `TopCategoryAnalyticsEngine`. For any month it ranks spending categories by amount, computes each category's share of total spending, and identifies its historical peak and low months. A companion 12-month monthly totals view shows income vs spending trends per month for quick direction sensing.

### Data Audit Trail (Transparency)
The Settings page now hosts an Audit card built by the new `AuditTrailCalculator`. It verifies integrity hashes across all stored profit entries, measures hash coverage, and summarizes ledger activity (entries added, updated, deleted per user) so the user can see exactly how their data has changed and whether it remains intact.

## Database
- **Migration 9** adds the `RecurringMovements` table and the `AttachmentBase64` column to `ProfitEntry`. The migration is applied automatically on first launch after upgrade; existing data is untouched.

## Version Bump
- `ApplicationDisplayVersion`: 2.9.0 → **3.0.0**
- `ApplicationVersion` (Android build number): 11 → **12**

## Test Coverage
- Added `V300EngineTests.cs` covering recurring-movement due/stale logic, occurrence advancement across month/year boundaries and end-of-month clamping, amount building, category analytics (ranking, share, peak/low months, income/deletion filtering, empty input), monthly totals, and audit-trail summaries.
- **Total tests: 251 — all passing.**

## What Stayed the Same
- Local-first storage, bilingual AR/EN UI, personal vs business sections, and all previously shipped engines (forecasting, health score, savings goals, business comparisons) are unchanged and fully verified by the regression suite.
