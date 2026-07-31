#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
dotnet workload restore src/Maen.Accounting.App/Maen.Accounting.App.csproj
dotnet restore tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj
dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj -c Release --no-restore
dotnet publish src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Release
