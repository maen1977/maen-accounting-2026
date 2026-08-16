# Maen Accounting — Full Product Audit and Modernization Plan

## Executive summary

Maen Accounting has a strong functional base: separate personal and business storage, Firebase authentication, local-first SQLite persistence, Firestore synchronization for ledger entries, bilingual text resources, bank movements for personal accounts, and a business chart of accounts with cash/bank payment selection. The next development cycle should focus on turning this feature-rich prototype into a maintainable accounting product with a clearer domain boundary, safer synchronization, consistent navigation, more deliberate information hierarchy, and stronger automated verification.

The modernization will preserve the existing product decisions: **Maen Accounting** remains the official English brand, Arabic and English remain separate experiences, personal and business data remain physically separated, and local saving remains available before cloud synchronization.

## Current architecture findings

| Area | Current state | Risk or limitation | Priority |
| --- | --- | --- | --- |
| Application shell | Startup and page replacement are coordinated directly by `SessionCoordinator` and `App` | Navigation, session, onboarding, and initialization are tightly coupled; startup failures are difficult to isolate | High |
| Main state | `MainStateViewModel` owns personal CRUD, reporting, bank balance, backup, cloud sync triggers, and business summary aggregation | God-viewmodel, high regression risk, slow UI property recalculation, difficult unit testing | Critical |
| Persistence | SQLite repositories are separated by personal/business database files | Good isolation, but migration/version management is distributed across repository code | High |
| Cloud sync | `FirestoreSyncService` synchronizes `ProfitEntry` collections only and downloads the full collection before merge | Business documents, accounts, journals, and payments are not covered by the same reliable sync contract; full-collection sync will not scale | Critical |
| Domain layer | Core services contain useful validators, calculators, journal factories, and merge logic | The application layer still performs too much business calculation and display mapping | High |
| Personal UX | Dashboard is a long vertical card stack with metrics, bank summary, budget, and recent entries | Important actions and explanations compete for attention; the primary workflow is not obvious | High |
| Business UX | Business workflow is concentrated in one long page containing contacts, invoices, payments, and lists | Dense form page, weak task navigation, limited filtering and drill-down | High |
| Localization | `UiText` centralizes many labels, but some layout decisions are hardcoded for RTL | English experience can inherit Arabic layout assumptions; accessibility and pluralization are limited | High |
| Visual system | Shared colors and card styles exist in `App.xaml` | Theme tokens are incomplete; semantic colors and spacing are repeated in XAML/code | Medium |
| Authentication | Firebase REST authentication and local mode exist | Recovery and session flows need explicit state handling, error surfaces, and retry behavior | High |
| Testing | Core tests exist and have been kept green during recent work | UI, migration, sync-contract, and end-to-end coverage remain comparatively thin | Critical |
| Delivery | Android and Windows builds run through local/CI workflows | Release validation should become repeatable, artifact-based, and version-gated | High |

## Target architecture

The target is a layered application with explicit boundaries:

1. **Presentation layer**: pages, reusable controls, view models, navigation state, and accessibility/localization bindings.
2. **Application layer**: use cases such as record personal movement, transfer cash to bank, post invoice, record payment, sync workspace, export report, and restore backup.
3. **Domain layer**: money, ledgers, journal rules, account balances, due dates, movement classification, validation, and reconciliation rules.
4. **Infrastructure layer**: SQLite repositories, Firestore adapters, Firebase authentication, secure session storage, backup files, device identity, and platform services.
5. **Composition layer**: dependency injection, app startup, platform registration, feature flags, and release configuration.

The first refactoring target is to split `MainStateViewModel` into focused units without changing user-visible behavior:

| New responsibility | Suggested component |
| --- | --- |
| Personal entry editing and validation | `PersonalLedgerViewModel` plus `RecordPersonalMovementUseCase` |
| Personal dashboard metrics | `PersonalDashboardViewModel` plus `PersonalSummaryService` |
| Personal reports and filters | `PersonalReportsViewModel` plus `ReportQueryService` |
| Bank transfers and balances | `PersonalBankViewModel` plus `BankLedgerService` |
| Cloud/local state | `SyncStatusViewModel` plus `SyncWorkspaceUseCase` |
| Backup and restore | `BackupViewModel` plus `BackupService` facade |
| Business overview | `BusinessDashboardViewModel` plus `BusinessSummaryService` |

## Product experience direction

The interface should be rebuilt around tasks rather than around a single scrolling page. The primary shell should provide a clear workspace header, current mode indicator, synchronization state, and a compact navigation model. The personal workspace should emphasize **available balance, bank balance, income, spending, and quick actions**. The business workspace should emphasize **cash/bank position, receivables, payables, invoices due soon, and journal health**.

The new visual system should retain the existing navy/gold brand mark while introducing a restrained accounting palette: navy for identity and navigation, gold for attention and important financial highlights, green for positive movements, red for money out or validation errors, and neutral surfaces for data density. Color must never be the only status signal; labels, icons, and accessible contrast are required.

## Functional modernization backlog

### Personal accounts

The personal flow should support a clear account model rather than treating wallets as free text. Users should be able to define cash wallets and bank accounts, choose a source and destination for transfers, record salary and freelance income, record purchases and withdrawals, and see a reconciled balance for each account. Every movement should have date, amount, type, account, category, counterparty, note, and optional attachment metadata.

The dashboard should include a balance-by-account view, monthly cash-flow summary, spending categories, recurring income, upcoming obligations, and a searchable movement timeline. Editing and deletion must show the affected account and resulting balance before confirmation.

### Business accounts

The business flow should evolve from a single long page into workspaces for dashboard, contacts, sales invoices, purchase bills, payments, chart of accounts, journal, trial balance, reports, and settings. The current cash/bank selection should remain but be connected to a formal account selector and journal preview. Due-date workflows should support overdue, due soon, and paid states with filters.

### Cross-cutting capabilities

The product should add consistent empty states, inline validation, undo where safe, confirmation for destructive actions, import/export with a documented schema, backup health indicators, sync conflict visibility, and an audit trail for locally changed records. The user must always be able to distinguish **saved locally**, **queued for sync**, **synced**, and **sync failed**.

## Data and synchronization modernization

The database layer needs an explicit schema version and migration registry. Each migration should be idempotent, logged, and covered by tests against both a new database and a representative older database. Cloud payloads should include stable record identifiers, workspace scope, schema version, updated-at timestamp, device identifier, tombstone/deletion state, and a deterministic conflict policy.

Synchronization should become use-case based rather than collection-wide. A workspace sync should separately reconcile personal movements, business accounts, contacts, invoices, payments, journals, and settings. Pull and push operations should be incremental where possible, with retry/backoff, bounded batches, a visible queue, and a final verification summary. Cloud rules should validate ownership, workspace scope, allowed fields, and immutable identifiers.

## Security and reliability priorities

Firebase API errors must remain user-friendly without exposing sensitive implementation details. Secure storage should be used for session material, local mode must never accidentally reuse a cloud user identifier, and logs must not contain access tokens or private financial data. Backup exports should be encrypted or clearly marked as unencrypted, and restore must require an explicit confirmation with a preview of the affected workspace.

## Testing and release gates

The modernization should add tests at four levels:

| Level | Required coverage |
| --- | --- |
| Domain unit tests | Money arithmetic, movement classification, bank transfers, journal balancing, due-date status, merge rules |
| Repository tests | Fresh schema, every migration, personal/business isolation, soft deletion, persistence round trips |
| Sync contract tests | Payload serialization, ownership fields, conflict resolution, retry behavior, verification state |
| UI smoke tests | Onboarding language choice, local entry, personal bank deposit/withdrawal, business payment from bank, sign-out, restore confirmation |

A release is acceptable only when tests pass, both platform builds succeed in CI, the APK/AAB metadata is correct, the Windows package is produced, migration checks pass, and the release notes identify the schema and synchronization changes.

## Recommended implementation order

1. Establish the design tokens, app shell, navigation state, and explicit loading/error/sync states.
2. Extract focused application services and view models from `MainStateViewModel` while keeping existing screens working.
3. Introduce schema-versioned migrations and a typed account model for personal cash/bank accounts.
4. Build the personal dashboard and movement timeline around account balances and quick actions.
5. Split the business page into task-oriented sections and connect payments to formal ledger accounts.
6. Expand Firestore synchronization to all supported workspace entities with incremental sync and conflict visibility.
7. Add reports, exports, backup/restore preview, accessibility, and UI smoke tests.
8. Run Android and Windows release gates, publish a versioned GitHub Release, and update user documentation.

## Immediate first slice

The first implementation slice should be deliberately narrow but high leverage: **introduce a shared app shell with a mode-aware header, sync state, and consistent navigation; extract personal dashboard summary calculations into a dedicated service; and add schema/migration diagnostics before adding more accounting features**. This reduces future regression risk and gives the later interface work a stable foundation.

This document is the baseline for the full modernization effort. Each completed slice should be committed independently, tested, and documented so that the existing working accounting flows remain recoverable throughout the redesign.
