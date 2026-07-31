$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet --info
    dotnet workload restore src/Maen.Accounting.App/Maen.Accounting.App.csproj
    dotnet restore tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj
    dotnet test tests/Maen.Accounting.Core.Tests/Maen.Accounting.Core.Tests.csproj -c Release --no-restore
    dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-android -c Debug
    if ($IsWindows) {
        dotnet build src/Maen.Accounting.App/Maen.Accounting.App.csproj -f net10.0-windows10.0.19041.0 -c Debug
    }
}
finally {
    Pop-Location
}
