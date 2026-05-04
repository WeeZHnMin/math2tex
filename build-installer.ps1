#requires -Version 5.1
<#
.SYNOPSIS
    One-shot installer build: publish + Inno Setup compile.

.DESCRIPTION
    1. Runs `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
       into ./publish
    2. Invokes Inno Setup compiler (ISCC.exe) on setup.iss
    3. Output:  installer/Math2Tex-Setup-X.Y.Z.exe

    Inno Setup is required. Install via:
        winget install JRSoftware.InnoSetup
    Or download:  https://jrsoftware.org/isdl.php
#>

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $here

# Resolve dotnet
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnet = $cmd.Source } else {
        Write-Error "dotnet SDK not found"
        exit 1
    }
}

# Resolve ISCC (Inno Setup compiler)
$iscc = $null
$candidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
)
foreach ($c in $candidates) {
    if (Test-Path $c) { $iscc = $c; break }
}
if (-not $iscc) {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { $iscc = $cmd.Source }
}
if (-not $iscc) {
    Write-Error "Inno Setup not installed. Run:  winget install JRSoftware.InnoSetup"
    exit 1
}

# Stop any running instance (publish writes to bin/obj which may be locked)
Get-Process Math2Tex -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Stopping running Math2Tex (PID $($_.Id))..."
    try { $_ | Stop-Process -Force } catch { }
}
Start-Sleep -Milliseconds 500

# 1. Publish
Write-Host ""
Write-Host "[1/2] Publishing..." -ForegroundColor Cyan
$publishDir = Join-Path $here "publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& $dotnet publish -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Error "publish failed"; exit 1 }

# 2. Compile installer
Write-Host ""
Write-Host "[2/2] Compiling installer with Inno Setup..." -ForegroundColor Cyan
$installerDir = Join-Path $here "installer"
if (-not (Test-Path $installerDir)) { New-Item -ItemType Directory -Path $installerDir | Out-Null }
& $iscc "setup.iss" | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed"; exit 1 }

$exe = Get-ChildItem $installerDir -Filter "Math2Tex-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($exe) {
    Write-Host ""
    Write-Host "Done." -ForegroundColor Green
    Write-Host "  Installer: $($exe.FullName)"
    Write-Host "  Size     : $([math]::Round($exe.Length/1MB, 1)) MB"
} else {
    Write-Warning "Installer compiled but Math2Tex-Setup-*.exe not found in $installerDir"
}
