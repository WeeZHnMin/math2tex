#requires -Version 5.1
<#
.SYNOPSIS
    Uninstall Math2Tex from the per-user install location.

.DESCRIPTION
    Removes:
      - Running process
      - Autostart registry entry (HKCU\...\Run\Math2Tex)
      - Desktop shortcut
      - Install directory (%LocalAppData%\Math2Tex)

    Preserves:
      - User settings, history, debug log at %AppData%\Math2Tex\
        (use -Purge to also delete these)
#>

param(
    [switch]$Purge
)

$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "Math2Tex"
$dataDir    = Join-Path $env:APPDATA "Math2Tex"
$shortcut   = Join-Path ([Environment]::GetFolderPath("Desktop")) "Math2Tex.lnk"
$runKey     = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

# 1. Stop running process
$running = Get-Process Math2Tex -ErrorAction SilentlyContinue
foreach ($p in $running) {
    Write-Host "  Stopping Math2Tex (PID $($p.Id))..."
    try { $p | Stop-Process -Force } catch { Write-Warning "  $_" }
}
Start-Sleep -Milliseconds 300

# 2. Remove autostart
if (Get-ItemProperty -Path $runKey -Name "Math2Tex" -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name "Math2Tex" -ErrorAction SilentlyContinue
    Write-Host "  Removed autostart entry"
}

# 3. Desktop shortcut
if (Test-Path $shortcut) {
    Remove-Item $shortcut -Force
    Write-Host "  Removed desktop shortcut"
}

# 4. Install dir
if (Test-Path $installDir) {
    Remove-Item -Recurse -Force $installDir
    Write-Host "  Removed $installDir"
}

# 5. Purge user data if requested
if ($Purge -and (Test-Path $dataDir)) {
    Remove-Item -Recurse -Force $dataDir
    Write-Host "  Purged user data $dataDir"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
if (-not $Purge -and (Test-Path $dataDir)) {
    Write-Host "User data kept at: $dataDir (re-run with -Purge to delete)"
}
