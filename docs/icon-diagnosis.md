# Maen Accounting icon diagnosis

## Observed behavior

The source icon and the Android-generated foreground layer both contain the complete navy-and-gold MA accounting mark. The Android build therefore is not blank because the source PNG is empty.

## Confirmed causes to address

- `App.CreateWindow` starts with `SplashPage`, which displays `maen_logo.png` inside the page. This is separate from the operating-system window icon.
- The application does not currently set a Windows `AppWindow` title-bar icon explicitly.
- The project uses `WindowsPackageType=None`; on Windows this is an unpackaged app, so a desktop shortcut is not automatically created and its icon cannot be controlled solely by the in-app splash image.
- The shared MAUI `MauiIcon` resource correctly supplies Android launcher resources, but installed devices may cache the previous launcher icon until the old app is uninstalled or the launcher cache refreshes.

## Planned fix

Add an explicit Windows `.ico` asset with multiple sizes, include it in the Windows output, set the WinUI `AppWindow` icon at startup, and add a visible branded header/title region in the first application page so the MA mark is present inside the window as well as in the splash screen. Keep the existing shared MAUI icon for Android launcher and Windows package metadata.
