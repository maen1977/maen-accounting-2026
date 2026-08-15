# Maen Accounting

**Maen Accounting** is a bilingual .NET MAUI accounting application for Android and Windows. It provides separate personal and business accounting paths, local-first storage, Firebase authentication, and optional Firestore synchronization.

## Highlights

- Personal accounting for salary deposits, daily or freelance income, purchases, withdrawals, transfers, and debt payments.
- Business accounting with income, expenses, invoices, independent invoice due dates, and financial summaries.
- Physically separate SQLite databases for personal and business data.
- Local-first saving with explicit cloud synchronization and backup status.
- Firebase Authentication REST integration and Firestore synchronization.
- Arabic and English interfaces without mixing localized UI text.
- Professional navy-and-gold **MA** application identity across the app icon, splash screen, Android package, and Windows MAUI resources.

## Project structure

| Path | Purpose |
| --- | --- |
| `src/Maen.Accounting.App` | .NET MAUI application for Android and Windows |
| `src/Maen.Accounting.Core` | Shared accounting models and business logic |
| `tests/Maen.Accounting.Core.Tests` | Automated core test suite |
| `firestore.rules` | Firestore access rules for business and personal entries |
| `docs/branding-audit.md` | Final branding changes and validation record |

## Build commands

The repository uses .NET SDK 10 and the Android workload. On an Android-capable development host:

```bash
dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj
dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj \
  -f net10.0-android -c Release
```

Windows builds use the `net10.0-windows10.0.19041.0` target on a Windows host with the MAUI Windows workload installed.

## Current release identity

| Property | Value |
| --- | --- |
| Application title | `Maen Accounting` |
| Android package ID | `com.maen.accounting` |
| Application version | `2.1.0` |
| Android version code | `2` |
| Supported targets | Android and Windows |

The Android Release validation completed with **31 passing tests**, a successful publish, and an APK label of `Maen Accounting`.
