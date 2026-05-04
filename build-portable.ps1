#requires -Version 5.1
<#
.SYNOPSIS
    Build a portable (zip) distribution of Math2Tex.

.DESCRIPTION
    Produces:  portable\Math2Tex-Portable-X.Y.Z.zip
    A user can extract anywhere and double-click Math2Tex.exe — no install,
    no .NET required, no admin.

    Settings/history still go to %AppData%\Math2Tex\ (user-specific).
#>

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $here

$version    = "1.0.0"
$publishDir = Join-Path $here "publish"
$portableDir = Join-Path $here "portable"
$zipName    = "Math2Tex-Portable-$version.zip"
$zipPath    = Join-Path $portableDir $zipName

# Resolve dotnet
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnet = $cmd.Source } else {
        Write-Error "dotnet SDK not found"
        exit 1
    }
}

# Stop any running instance
Get-Process Math2Tex -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  Stopping running Math2Tex (PID $($_.Id))..."
    try { $_ | Stop-Process -Force } catch { }
}
Start-Sleep -Milliseconds 500

# 1. Publish
Write-Host ""
Write-Host "[1/2] Publishing (Release, self-contained, single-file)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& $dotnet publish -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Error "publish failed"; exit 1 }

# 2. Zip
Write-Host ""
Write-Host "[2/2] Creating zip archive..." -ForegroundColor Cyan
if (-not (Test-Path $portableDir)) { New-Item -ItemType Directory -Path $portableDir | Out-Null }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

# Stage as a folder named "Math2Tex" so users get a clean folder when they unzip
$staging = Join-Path $env:TEMP ("Math2Tex-stage-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $staging | Out-Null
$stageRoot = Join-Path $staging "Math2Tex"
Copy-Item -Path $publishDir -Destination $stageRoot -Recurse

# Add a small README inside
$readme = @"
Math2Tex (Portable)
===================

Double-click Math2Tex.exe to run. No install needed. .NET runtime is bundled.

User data location:
  %AppData%\Math2Tex\
    backend-settings.json
    history.json
    debug.log

To remove: delete this folder. To purge user data, also delete %AppData%\Math2Tex\.
"@
$readme | Out-File -FilePath (Join-Path $stageRoot "README.txt") -Encoding utf8

Compress-Archive -Path $stageRoot -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item -Recurse -Force $staging

$zipInfo = Get-Item $zipPath
Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Zip   : $zipPath"
Write-Host "  Size  : $([math]::Round($zipInfo.Length/1MB, 1)) MB"
Write-Host ""
Write-Host "Distribute the zip. Recipients extract anywhere and double-click Math2Tex.exe."
