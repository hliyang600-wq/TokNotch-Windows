$ErrorActionPreference = 'Stop'
$localSdk = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localSdk) { $localSdk; return }
$installedSdk = Get-Command dotnet -ErrorAction SilentlyContinue
if ($installedSdk) { $installedSdk.Source; return }
throw 'Install .NET SDK 10.0.401 (or a later 10.0.4xx patch), or place it in .tools/dotnet.'
