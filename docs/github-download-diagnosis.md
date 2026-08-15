# GitHub download diagnosis

Checked repository: https://github.com/maen1977/maen-accounting-2026

The repository is private, default branch `main`, and had no GitHub Releases or release assets at the time of inspection. The repository has a GitHub Actions workflow at `.github/workflows/build.yml`.

The workflow run for commit `d828a22` failed at the Windows build job with:

`NETSDK1152: Found multiple publish output files with the same relative path: obj/Release/net10.0-windows10.0.19041.0/win-x64/resizetizer/r/appicon.ico and Platforms/Windows/appicon.ico.`

The immediate fix was to remove the duplicate `Content Include="Platforms/Windows/appicon.ico"` entry from `src/Maen.Accounting.App/Maen.Accounting.App.csproj`. The subsequent commit is `7a9522e` (`fix: avoid duplicate Windows icon publish output`) and was pushed to `main`.

The new workflow run is: https://github.com/maen1977/maen-accounting-2026/actions/runs/31898898401

At the latest check, `test-core` had completed successfully and `build-android` and `build-windows` were still in progress. The earlier red result was therefore a failed GitHub Actions build, not an Android icon-content failure.
