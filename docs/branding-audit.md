# Maen Accounting branding audit

## Final identity

The official application name is **Maen Accounting** in English. The Android application ID remains `com.maen.accounting` so existing installations can receive version upgrades without changing package identity.

## Applied branding changes

- `ApplicationTitle` is `Maen Accounting`.
- The localized title key `T092` is `Maen Accounting` in both Arabic and English UI dictionaries, preventing a mixed-language product name.
- Application version is `2.1.0` and Android version code is `2`.
- `Resources/AppIcon/appicon.png` is the new navy-and-gold MA monogram icon.
- `Resources/Images/maen_logo.png` uses the same visual identity inside the application.
- `Resources/Splash/splash.png` uses the same visual identity on the startup screen.
- `tools/prepare_android_local.py` uses the `Maen Accounting` Android label.
- Branding assets were resized to 1024 x 1024 and PNG-optimized for a smaller application package.

## Platform coverage

The shared MAUI icon and splash resources are consumed by Android and Windows builds. Android was built and inspected successfully after the final branding changes. Windows targeting is conditionally enabled by the project file on Windows hosts, which is the standard MAUI configuration and preserves cross-platform restore behavior on Linux CI hosts.

## Validation

- Core test suite: **31 passed, 0 failed**.
- Android Release publish: **successful**.
- Android package label: `Maen Accounting`.
- Android version name/code: `2.1.0` / `2`.
- Android package ID: `com.maen.accounting`.

The release APK and AAB are generated under `src/Maen.Accounting.App/bin/Release/net10.0-android/publish/`.
