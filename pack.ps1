# Lightspeed WPF Pack Script
# Builds packages:
#   1) framework-dependent (small, requires .NET 9 Desktop Runtime)
#   2) self-contained (includes .NET runtime)
#
# Examples:
#   .\pack.ps1                          # both
#   .\pack.ps1 -Kind FrameworkDependent # only FD (no self-contained)
#   .\pack.ps1 -Kind SelfContained      # only self-contained
#   .\pack.ps1 -NoSelfContained         # alias of FrameworkDependent

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish",

    [ValidateSet("All", "FrameworkDependent", "SelfContained")]
    [string]$Kind = "All",

    # Shortcut: only framework-dependent (skip self-contained)
    [switch]$NoSelfContained
)

$ErrorActionPreference = "Stop"

if ($NoSelfContained) {
    if ($Kind -ne "All" -and $Kind -ne "FrameworkDependent") {
        Write-Host "Error: -NoSelfContained conflicts with -Kind $Kind" -ForegroundColor Red
        exit 1
    }
    $Kind = "FrameworkDependent"
}

$projectDir = $PSScriptRoot
$csprojPath = Join-Path $projectDir "Lightspeed-wpf\Lightspeed-wpf.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Host "Error: Project file not found at $csprojPath" -ForegroundColor Red
    exit 1
}

# Read version from csproj
[xml]$csproj = Get-Content $csprojPath
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { $version = "0.0.0" }

$publishRoot = Join-Path $projectDir $OutputDir
$fdDir = Join-Path $publishRoot "framework-dependent"
$scDir = Join-Path $publishRoot "self-contained"
$fdZip = Join-Path $publishRoot "Lightspeed-wpf-v$version-$Runtime-framework-dependent.zip"
$scZip = Join-Path $publishRoot "Lightspeed-wpf-v$version-$Runtime-self-contained.zip"

$buildFd = ($Kind -eq "All" -or $Kind -eq "FrameworkDependent")
$buildSc = ($Kind -eq "All" -or $Kind -eq "SelfContained")

Write-Host "Pack Kind: $Kind" -ForegroundColor DarkGray

# Stop running app so publish can overwrite exe
$processName = "Lightspeed-wpf"
$running = Get-Process -Name $processName -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Killing running instances of $processName..." -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

# Clean previous outputs
if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
if ($buildFd) { New-Item -ItemType Directory -Path $fdDir -Force | Out-Null }
if ($buildSc) { New-Item -ItemType Directory -Path $scDir -Force | Out-Null }

function Invoke-Publish {
    param(
        [string]$Label,
        [string]$OutDir,
        [bool]$SelfContained
    )

    Write-Host ""
    Write-Host "==> Publishing $Label ..." -ForegroundColor Yellow

    $publishArgs = @(
        "publish", $csprojPath,
        "-c", $Configuration,
        "-r", $Runtime,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "--output", $OutDir
    )

    if ($SelfContained) {
        $publishArgs += @(
            "--self-contained", "true",
            "-p:EnableCompressionInSingleFile=true",
            "-p:PublishTrimmed=false"
        )
    } else {
        $publishArgs += @("--self-contained", "false")
    }

    Write-Host ("    dotnet " + ($publishArgs -join " ")) -ForegroundColor DarkGray
    & dotnet @publishArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Label (exit $LASTEXITCODE)"
    }

    $exe = Join-Path $OutDir "Lightspeed-wpf.exe"
    if (-not (Test-Path $exe)) {
        throw "Exe not found after publish: $exe"
    }

    # Drop leftover satellite / pdb clutter if any (keep exe + needed files)
    Get-ChildItem $OutDir -File -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force

    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
    Write-Host "    EXE: $exe ($sizeMb MB)" -ForegroundColor Cyan
    return [double]$sizeMb
}

function New-ZipFromDir {
    param(
        [string]$SourceDir,
        [string]$ZipPath
    )
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Compress-Archive -Path (Join-Path $SourceDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal
    $zipMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
    Write-Host "    ZIP: $ZipPath ($zipMb MB)" -ForegroundColor Cyan
    return $zipMb
}

try {
    $fdSize = $null
    $scSize = $null
    $fdZipSize = $null
    $scZipSize = $null

    if ($buildFd) {
        $fdSize = Invoke-Publish -Label "framework-dependent (small, needs .NET 9)" -OutDir $fdDir -SelfContained $false
    }
    if ($buildSc) {
        $scSize = Invoke-Publish -Label "self-contained (includes .NET)" -OutDir $scDir -SelfContained $true
    }

    Write-Host ""
    Write-Host "==> Creating zip packages ..." -ForegroundColor Yellow
    if ($buildFd) {
        $fdZipSize = New-ZipFromDir -SourceDir $fdDir -ZipPath $fdZip
    }
    if ($buildSc) {
        $scZipSize = New-ZipFromDir -SourceDir $scDir -ZipPath $scZip
    }

    Write-Host ""
    Write-Host "Build successful! v$version" -ForegroundColor Green
    Write-Host ("-" * 60)
    if ($buildFd) {
        Write-Host ("Framework-dependent : {0,8} MB exe / {1,8} MB zip" -f $fdSize, $fdZipSize) -ForegroundColor Cyan
        Write-Host "  Requires: .NET 9 Desktop Runtime ($Runtime)"
        Write-Host "  Folder : $fdDir"
        Write-Host "  Zip    : $fdZip"
    }
    if ($buildFd -and $buildSc) { Write-Host "" }
    if ($buildSc) {
        Write-Host ("Self-contained       : {0,8} MB exe / {1,8} MB zip" -f $scSize, $scZipSize) -ForegroundColor Cyan
        Write-Host "  Includes: .NET runtime (no install needed)"
        Write-Host "  Folder : $scDir"
        Write-Host "  Zip    : $scZip"
    }
    Write-Host ("-" * 60)
    Write-Host "Done!" -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
