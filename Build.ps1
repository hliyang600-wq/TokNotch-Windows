$ErrorActionPreference = 'Stop'
$dotnet = & "$PSScriptRoot\RuntimePath.ps1"
Push-Location $PSScriptRoot
try {
    & $dotnet build TokNotchWindows.sln -c Release --nologo -p:RestoreConfigFile="$PSScriptRoot\NuGet.Config"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $dotnet build tests/DataTests/DataTests.csproj -c Release --nologo -p:RestoreConfigFile="$PSScriptRoot\NuGet.Config"
    if ($LASTEXITCODE -ne 0) { throw 'Data test build failed.' }
    & $dotnet build tests/SingleInstanceTests/SingleInstanceTests.csproj -c Release --nologo -p:RestoreConfigFile="$PSScriptRoot\NuGet.Config"
    if ($LASTEXITCODE -ne 0) { throw 'Single-instance test build failed.' }
} finally { Pop-Location }
