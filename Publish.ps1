param([ValidateSet('win-x64')][string]$Runtime = 'win-x64', [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$dotnet = & "$PSScriptRoot\RuntimePath.ps1"
$packageName = if ($FrameworkDependent) { 'TokNotch-Windows-win-x64-requires-dotnet10' } else { "TokNotch-Windows-$Runtime" }
$destination = Join-Path $PSScriptRoot "publish\$packageName"
if (Test-Path -LiteralPath $destination) { throw 'The publish directory already exists. Move it aside before creating a fresh package.' }
Push-Location $PSScriptRoot
try {
    $runtimeArguments = if ($FrameworkDependent) { @('--self-contained', 'false') } else { @('-r', $Runtime, '--self-contained', 'true') }
    & $dotnet publish src/TokNotch.UI/TokNotch.UI.csproj -c Release @runtimeArguments -o $destination --nologo -p:RestoreConfigFile="$PSScriptRoot\NuGet.Config" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    # .NET runtime redistribution notices accompany the self-contained runtime.
    foreach ($package in $(if (!$FrameworkDependent) { @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64') })) {
        $packageRoot = Join-Path $PSScriptRoot ".packages\$package"
        $version = Get-ChildItem -LiteralPath $packageRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if (!$version) { throw "Missing runtime license package: $package" }
        foreach ($name in @('LICENSE.txt','THIRD-PARTY-NOTICES.TXT')) {
            $notice = Get-ChildItem -LiteralPath $version.FullName -Recurse -File | Where-Object Name -IEQ $name | Select-Object -First 1
            if (!$notice -and $package -eq 'microsoft.windowsdesktop.app.runtime.win-x64') {
                $notice = Get-Item -LiteralPath (Join-Path $PSScriptRoot "LICENSES\dotnet-WPF-$name") -ErrorAction SilentlyContinue
            }
            if (!$notice) { throw "Missing $package/$name" }
            Copy-Item -LiteralPath $notice.FullName -Destination (Join-Path $destination "Licenses\$package-$name")
        }
    }
    Copy-Item -LiteralPath README.md -Destination $destination
    New-Item -ItemType Directory -Force artifacts | Out-Null
    Compress-Archive -Path "$destination\*" -DestinationPath "artifacts\$packageName.zip"
} finally { Pop-Location }

