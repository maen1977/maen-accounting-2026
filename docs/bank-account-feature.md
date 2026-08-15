# Bank account support in Maen Accounting

## Personal accounts

Personal accounts now support a separate bank wallet. The user can enter an existing bank balance manually as a **Bank deposit**, record money leaving the bank as a **Bank withdrawal**, and review the resulting bank balance independently from the general cash balance.

Bank movements are stored in the same local-first personal database and use the existing user-selected synchronization flow. They are identified by dedicated movement types and the `bank` wallet marker, so they are excluded from the general cash wallet calculation while remaining part of the personal balance history.

The personal dashboard shows the bank balance, monthly bank deposits, and monthly bank withdrawals. The recent-movements list also displays bank deposit and withdrawal labels in Arabic or English according to the selected application language.

## Business accounts

Business accounts now include a system **Bank** asset account in the default chart of accounts. Existing business databases receive the account through an idempotent account migration.

Business receipts and supplier payments can be assigned to either **Cash** or **Bank**. The selected account is persisted in SQLite, defaults to Cash for legacy records, and is used by the journal factory when creating the balanced accounting entry. Manual journal entry can also use the Bank account directly for opening balances and transfers.

## Version

This feature is released in application version **2.2.0** with Android version code **4**.

The application remains local-first: entries are saved locally first, and cloud synchronization occurs only through the existing explicit user action.
