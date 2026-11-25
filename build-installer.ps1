# Builds the Qwen Code Manager Windows installer end-to-end.
# Usage:
#   powershell -ExecutionPolicy Bypass -File build-installer.ps1 [-Version 1.0.1] [-SkipPublish]
# Output: artifacts\installer\QwenCodeManager-Setup-x64.exe (+ .sha256, version.json)
param(
    [string]$Version,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

# --- Version: single source of truth is Directory.Build.props ---
if (-not $Version) {
    [xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
    $Version = $props.Project.PropertyGroup.Version
}
if (-not $Version) { throw 'Version not found in Directory.Build.props' }
Write-Host "==> Version: $Version"

# --- Locate Inno Setup compiler (no hardcoded user paths) ---
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    'C:\InnoSetup\ISCC.exe',
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if (-not $isccCandidates) {
    # Fallback: bundled Inno6 shipped inside pyinno_setup, if present.
    $bundled = Get-ChildItem "$env:LOCALAPPDATA\Programs\Python" -Recurse -Filter ISCC.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match 'Inno6' } | Select-Object -First 1
    if ($bundled) { $isccCandidates = @($bundled.FullName) }
}
if (-not $isccCandidates) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it or add iscc to PATH.' }
$iscc = $isccCandidates[0]
Write-Host "==> ISCC: $iscc"

# --- Icon (generated once, committed to repo) ---
$iconPath = Join-Path $root 'src\QwenCodeManager.App\Assets\app.ico'
if (-not (Test-Path $iconPath)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\make-icon.ps1')
}

# --- Publish (Release x64, self-contained: .NET runtime + Windows App SDK bundled) ---
$publishDir = Join-Path $root 'artifacts\publish\win-x64'
if (-not $SkipPublish) {
    Write-Host '==> dotnet publish (Release, win-x64, self-contained)'
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    dotnet publish (Join-Path $root 'src\QwenCodeManager.App') `
        -c Release -r win-x64 --self-contained true `
        -p:WindowsAppSDKSelfContained=true -p:Version=$Version `
        -o $publishDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}
if (-not (Test-Path (Join-Path $publishDir 'QwenCodeManager.exe'))) {
    throw "Publish output missing QwenCodeManager.exe in $publishDir"
}

# --- Compile installer ---
$outDir = Join-Path $root 'artifacts\installer'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$baseName = 'QwenCodeManager-Setup-x64'
Write-Host '==> Compiling installer'
& $iscc `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishDir" `
    "/DOutputDir=$outDir" `
    "/DOutputBase=$baseName" `
    (Join-Path $root 'installer\QwenCodeManager.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }

$installer = Join-Path $outDir "$baseName.exe"
if (-not (Test-Path $installer)) { throw "Installer not produced: $installer" }

# --- Checksum + version info ---
$hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$installer.sha256" -Value "$hash  $baseName.exe" -Encoding ascii
@{
    name      = 'Qwen Code Manager'
    version   = $Version
    arch      = 'x64'
    installer = "$baseName.exe"
    sha256    = $hash
    builtAt   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
} | ConvertTo-Json | Set-Content (Join-Path $outDir 'version.json') -Encoding utf8

Write-Host ''
Write-Host "==> DONE"
Write-Host "Installer: $installer"
Write-Host "SHA256:    $hash"
Write-Host "Size:      $([Math]::Round((Get-Item $installer).Length / 1MB, 1)) MB"
