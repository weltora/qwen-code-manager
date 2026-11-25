# Builds demo data in an isolated sandbox (QCM_DATA_DIR + QWEN_HOME), launches the app
# and captures docs/screenshots/*.png. No real user data is read or published.
param(
    [string]$ExePath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\publish\win-x64\QwenCodeManager.exe'),
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs\screenshots')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Write-Utf8([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, $utf8) }

$sandbox = Join-Path $env:TEMP 'QCM-Demo'
$lad = Join-Path $sandbox 'data'
$qhome = Join-Path $sandbox 'qwen-home'
$demo = Join-Path $sandbox 'Projects'
if (Test-Path $sandbox) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Force -Path $lad, $qhome, $demo, $OutDir | Out-Null

# --- demo QWEN_HOME ---
Write-Utf8 (Join-Path $qhome 'QWEN.md') "# Demo QWEN.md`nThis is demo data for screenshots."
New-Item -ItemType Directory -Force -Path (Join-Path $qhome 'skills\pdf'), (Join-Path $qhome 'skills\review'), (Join-Path $qhome 'skills\xlsx'), (Join-Path $qhome 'memories') | Out-Null

# --- demo projects ---
$novela = Join-Path $demo 'NoveLA'
$cyber = Join-Path $demo 'cyberpunk-universe'
$atelier = Join-Path $demo 'atoll'
New-Item -ItemType Directory -Force -Path "$novela\app\src\main", $cyber, $atelier | Out-Null
Write-Utf8 (Join-Path $novela 'settings.gradle.kts') 'rootProject.name = "NoveLA"'
Write-Utf8 (Join-Path $novela 'app\src\main\AndroidManifest.xml') '<manifest/>'
Write-Utf8 (Join-Path $cyber 'package.json') '{"name":"cyberpunk-universe","dependencies":{"react":"^18","vite":"^5"}}'
Write-Utf8 (Join-Path $atelier 'Atoll.csproj') '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>'

git init -q -b main $novela
Write-Utf8 (Join-Path $novela 'reader.kt') 'class ReaderShell'
git -C $novela add . ; git -C $novela -c user.name=demo -c user.email=demo@example.com commit -qm init
Write-Utf8 (Join-Path $novela 'reader.kt') "class ReaderShell { val v = 2 }"
Write-Utf8 (Join-Path $novela 'notes.txt') 'todo'
git -C $novela remote add origin https://github.com/demo/novela.git
git init -q -b main $cyber
Write-Utf8 (Join-Path $cyber 'app.tsx') 'export const App = () => null'
git -C $cyber add . ; git -C $cyber -c user.name=demo -c user.email=demo@example.com commit -qm init
git -C $cyber remote add origin https://github.com/demo/cyberpunk-universe.git

# --- demo sessions in isolated QWEN_HOME ---
function Add-Session([string]$projectPath, [string]$title, [double]$startEpoch, [int]$ageMinutes, [string]$prompt) {
    $slug = $projectPath.ToLowerInvariant() -replace '[:\\/]', '-'
    $chats = Join-Path $qhome "projects\$slug\chats"
    New-Item -ItemType Directory -Force -Path $chats | Out-Null
    $id = [Guid]::NewGuid().ToString()
    $runtime = @{ schema_version = 1; pid = 999999; session_id = $id; work_dir = $projectPath; started_at = $startEpoch; qwen_version = '0.28.4' } | ConvertTo-Json
    Write-Utf8 (Join-Path $chats "$id.runtime.json") $runtime
    $lines = @(
        (@{ role = 'user'; content = $prompt } | ConvertTo-Json -Compress),
        (@{ role = 'assistant'; content = 'Working on it.' } | ConvertTo-Json -Compress)
    )
    $jsonl = Join-Path $chats "$id.jsonl"
    Write-Utf8 $jsonl ($lines -join "`n")
    (Get-Item $jsonl).CreationTimeUtc = [DateTimeOffset]::FromUnixTimeSeconds([long]$startEpoch).UtcDateTime
    (Get-Item $jsonl).LastWriteTimeUtc = [DateTime]::UtcNow.AddMinutes(-$ageMinutes)
    $titleObj = @{ version = 1; displayName = $title; source = 'prompt'; updatedAt = [DateTime]::UtcNow.AddMinutes(-$ageMinutes).ToString('yyyy-MM-ddTHH:mm:ss.fffZ') } | ConvertTo-Json
    Write-Utf8 (Join-Path $chats "$id.title.json") $titleObj
}
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
Add-Session $novela 'Интеграция ReaderShell и навигации' ($now - 6300) 12 'Продолжи интеграцию ReaderShell'
Add-Session $novela 'EPUB parser: метаданные глав' ($now - 90000) 1500 'Исправь парсер метаданных EPUB'
Add-Session $novela 'UI: Material 3, темы и стили' ($now - 170000) 2900 'Обнови темы Material 3'
Add-Session $cyber 'Карта квестов: кластеризация маркеров' ($now - 80000) 1200 'Добавь кластеризацию маркеров на карту'
Add-Session $cyber 'Импорт сохранений билдов' ($now - 200000) 4000 'Сделай импорт сохранений'

# --- manager storage: settings + projects ---
$cfg = Join-Path $lad 'QwenCodeManager\config'
New-Item -ItemType Directory -Force -Path $cfg | Out-Null
$settings = @{ version = 1; theme = 'Dark'; accentColor = 'Teal'; gitHubCoverLookup = $false; coverCacheDays = 14; onboardingCompleted = $true; defaultLaunchMode = 0 } | ConvertTo-Json
Write-Utf8 (Join-Path $cfg 'settings.json') $settings
$projects = @{ version = 1; projects = @(
    @{ id = 'demo-novela'; name = 'NoveLA'; path = $novela; description = 'Android-приложение для чтения новелл, ранобэ и книг'; gitHubUrl = 'https://github.com/demo/novela'; defaultLaunchMode = 0; addedAtUtc = [DateTime]::UtcNow.AddDays(-9).ToString('o') },
    @{ id = 'demo-cyber'; name = 'Cyberpunk Universe'; path = $cyber; description = 'Web-планировщик билдов и квестов'; gitHubUrl = 'https://github.com/demo/cyberpunk-universe'; defaultLaunchMode = 0; addedAtUtc = [DateTime]::UtcNow.AddDays(-5).ToString('o') },
    @{ id = 'demo-atoll'; name = 'Atoll'; path = $atelier; description = '.NET desktop utility'; defaultLaunchMode = 1; addedAtUtc = [DateTime]::UtcNow.AddDays(-2).ToString('o') }
) } | ConvertTo-Json -Depth 5
Write-Utf8 (Join-Path $cfg 'projects.json') $projects

# --- launch with isolated env ---
$env:QCM_DATA_DIR = $lad
$env:QWEN_HOME = $qhome
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 9

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Cap {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[Win32Cap]::SetProcessDPIAware() | Out-Null
$proc.Refresh()
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { throw 'Main window not found' }
[Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 900

function Capture([string]$file) {
    $rect = New-Object Win32Cap+RECT
    # DWMWA_EXTENDED_FRAME_BOUNDS = 9: exact visible bounds, no invisible resize border
    [Win32Cap]::DwmGetWindowAttribute($hwnd, 9, [ref]$rect, [Runtime.InteropServices.Marshal]::SizeOf($rect)) | Out-Null
    $w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Output "SHOT: $file ($w x $h)"
    return $rect
}

$rect = Capture (Join-Path $OutDir 'projects.png')

function Click([int]$dx, [int]$dy) {
    [Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 300
    [Win32Cap]::SetCursorPos($rect.Left + $dx, $rect.Top + $dy) | Out-Null
    Start-Sleep -Milliseconds 150
    [Win32Cap]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 60
    [Win32Cap]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Seconds 2
}

# Sessions page (left nav)
Click 90 139
[Win32Cap]::SetCursorPos($rect.Right + 40, $rect.Bottom + 40) | Out-Null
Start-Sleep -Milliseconds 500
Capture (Join-Path $OutDir 'sessions.png') | Out-Null

# Memory page (left nav)
Click 90 181
[Win32Cap]::SetCursorPos($rect.Right + 40, $rect.Bottom + 40) | Out-Null
Start-Sleep -Milliseconds 500
Capture (Join-Path $OutDir 'memory.png') | Out-Null

# back to Projects, select Cyberpunk Universe for a second detail shot
Click 90 95
Click 300 320
[Win32Cap]::SetCursorPos($rect.Right + 40, $rect.Bottom + 40) | Out-Null
Start-Sleep -Seconds 2
Capture (Join-Path $OutDir 'project-detail.png') | Out-Null

$proc.CloseMainWindow() | Out-Null
Start-Sleep -Seconds 2
if (-not $proc.HasExited) { $proc.Kill() }
Remove-Item Env:QCM_DATA_DIR -ErrorAction SilentlyContinue
Remove-Item Env:QWEN_HOME -ErrorAction SilentlyContinue
Write-Output "SANDBOX: $sandbox (kept for inspection)"
