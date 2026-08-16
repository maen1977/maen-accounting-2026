# Maen Accounting — Release 2.9.0 "Maximum Upgrade"

**Build:** 11 | **Version:** 2.9.0

## New Features

### 1. Named Savings Goals (الأهداف الادخارية)
- Create named goals (e.g., "Travel", "Car") with a target amount and a deadline.
- Goals track deposits in their category automatically and subtract spending in the same category.
- Live progress percentage, remaining amount, days remaining, and "on track" status.
- New **Savings Goals** tab in personal accounts.

### 2. Financial Health Score (مؤشر الصحة المالية)
- 0–100 score built from the last 3 months of movements.
- Indicators: savings rate, fixed-cost ratio, cash runway (months of income vs. spending).
- Actionable flags: `LowSavingsRate`, `HighFixedCosts`, `ShortRunway`, `SpendingWithoutIncome`.
- New **Health** card on the personal dashboard (bilingual).

### 3. 90-Day Cash Forecast (التوقع النقدي)
- Projects income and spending from the recent 90-day average, layered with scheduled recurring obligations.
- Per-month slices (income / spending / obligations / net) plus the worst-month warning.
- New **Forecast** card on the Planning page.

### 4. Business Comparisons (مقارنات الشركات)
- Month-over-MoM (MoM) and Year-over-Year (YoY) deltas for sales and net cash.
- Trend arrows and percentages in the monthly report table, plus a comparisons card on the business reports page.

## Platform & Security
- **Migration 8** creates the `savings_goals` SQLite table and registers it in the sync document.
- `InputSanitizer` and `DataIntegrityService` (SHA-256 hashes with Firestore sync verification) now cover savings-goal save paths and the legacy backup parser.
- Bilingual UI (AR/EN) fully extended with ~50 new UiText keys (T600–T656).

## Testing
- 235 passing unit tests (195 prior + 40 new covering SavingsGoal, FinancialHealth, CashForecast, and BusinessComparison engines).

## Build
```
dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -p:AndroidSdkDirectory=/home/ubuntu/android-sdk
```
