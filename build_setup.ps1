# TAY System Optimizer - Setup Build Automation Script
# This script builds the self-contained Release and compiles the installer.exe automatically.

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host " TAY System Optimizer - Setup Builder    " -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Stop any running TAY processes to prevent file locking
Write-Host "Checking for active TAY processes..." -ForegroundColor Cyan
Get-Process -Name "TAY" -ErrorAction SilentlyContinue | Stop-Process -Force

# Step 1: Clean build and publish folders to prevent size bloat
Write-Host "`n[1/3] Cleaning stale files and publishing self-contained release..." -ForegroundColor Yellow
$publishDir = Join-Path $PSScriptRoot "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\"
if (Test-Path $publishDir) {
    Write-Host "Deleting stale publish folder: $publishDir" -ForegroundColor Cyan
    Remove-Item -Recurse -Force $publishDir
}

Write-Host "Running dotnet clean..." -ForegroundColor Cyan
dotnet clean -c Release -r win-x64 -p:Platform=x64

Write-Host "Running dotnet publish..." -ForegroundColor Cyan
dotnet publish -c Release -r win-x64 -p:Platform=x64 --self-contained true

# Step 2: Resolve ISCC.exe (Inno Setup Compiler)
Write-Host "`n[2/3] Resolving Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow

$isccPath = ""
$commonPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
    "C:\Program Files\Inno Setup 5\ISCC.exe"
)

# Check PATH first
$pathIscc = Get-Command iscc -ErrorAction SilentlyContinue
if ($pathIscc) {
    $isccPath = $pathIscc.Source
} else {
    # Check common folders
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $isccPath = $path
            break
        }
    }
}

if (-not $isccPath) {
    Write-Host "Inno Setup compiler (ISCC.exe) was not found on your system." -ForegroundColor Cyan
    Write-Host "Attempting to install Inno Setup via Windows Package Manager (winget)..." -ForegroundColor Yellow
    
    try {
        # Run winget silently to install Inno Setup
        $process = Start-Process -FilePath "winget" -ArgumentList "install --id JRSoftware.InnoSetup -e --silent --accept-source-agreements --accept-package-agreements" -NoNewWindow -PassThru -Wait
        Write-Host "winget installation command finished. Re-checking paths..." -ForegroundColor Cyan
    } catch {
        Write-Host "winget installation attempt failed." -ForegroundColor Red
    }
    
    # Re-check PATH
    $pathIscc = Get-Command iscc -ErrorAction SilentlyContinue
    if ($pathIscc) {
        $isccPath = $pathIscc.Source
    } else {
        # Check common folders again
        foreach ($path in $commonPaths) {
            if (Test-Path $path) {
                $isccPath = $path
                break
            }
        }
    }
    
    if (-not $isccPath) {
        Write-Host "Inno Setup could not be resolved automatically." -ForegroundColor Red
        Write-Host "Please download and install it manually from: https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
        Write-Error "Inno Setup (ISCC.exe) is required to compile installer.iss."
    }
    
    Write-Host "Inno Setup successfully installed and resolved!" -ForegroundColor Green
}

Write-Host "Found ISCC.exe at: $isccPath" -ForegroundColor Green

# Step 3: Compile installer.iss
Write-Host "`n[3/3] Compiling Inno Setup Script..." -ForegroundColor Yellow
$issPath = Join-Path $PSScriptRoot "installer.iss"

if (-not (Test-Path $issPath)) {
    Write-Error "Could not find installer.iss in the workspace root!"
}

# Run ISCC
& $isccPath $issPath

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host " BUILD COMPLETE!                         " -ForegroundColor Green
Write-Host " Setup generated: Output/TAY_Setup.exe   " -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
