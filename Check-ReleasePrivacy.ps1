param([string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$root = if ($PackageDirectory) { [IO.Path]::GetFullPath($PackageDirectory) } else { $PSScriptRoot }
if ($PackageDirectory) { $files = @(Get-ChildItem -LiteralPath $root -Recurse -File) }
else {
    $names = & git -C $root ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inventory public source.' }
    $files = @($names | ForEach-Object { $path = Join-Path $root $_; if (Test-Path -LiteralPath $path -PathType Leaf) { Get-Item -LiteralPath $path } })
}
$privatePath = '(?i)\b[A-Z]:[\\/]+Users[\\/]+[^\\/\s"''<>]+'
$secret = '(?<![A-Za-z0-9_-])(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-|sp-)?[A-Za-z0-9_-]{32,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)(?![A-Za-z0-9_-])'
foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length+1).Replace('\','/')
    if ($relative -match '(^|/)(data|config|backups|artifacts|\.git|\.build-home|\.packages|bin|obj|mimo-webview|qwen-webview)/|\.(pdb|dmp|bin|jsonl|zstd|log)$') { throw "Private/generated file in release: $relative" }
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    foreach ($encoding in @([Text.Encoding]::UTF8,[Text.Encoding]::Unicode)) {
        $text = $encoding.GetString($bytes)
        if ($text -match $privatePath -or $text -match $secret) { throw "Review possible private content: $relative" }
    }
}
"PASS privacy inventory: $($files.Count) files; no credentials, user data, debugging symbols or personal user paths."
