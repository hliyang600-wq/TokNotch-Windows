param([switch]$Native)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\Build.ps1"
$dotnet = & "$PSScriptRoot\RuntimePath.ps1"
Push-Location $PSScriptRoot
try {
    foreach ($project in @('tests/TokNotch.Tests', 'tests/DataTests', 'tests/SingleInstanceTests')) {
        & $dotnet run --project $project -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw "Checks failed: $project" }
    }
    if ($Native) {
        New-Item -ItemType Directory -Force artifacts | Out-Null
        & $dotnet src/TokNotch.UI/bin/Release/net10.0-windows/TokNotchWindows.dll --validate
        if ($LASTEXITCODE -ne 0) { throw 'Native window checks failed.' }
    }
} finally { Pop-Location }
