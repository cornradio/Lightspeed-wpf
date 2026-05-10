# Lightspeed WPF Single-File Pack Script

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish"
)

$projectDir = $PSScriptRoot
$csprojPath = Join-Path $projectDir "Lightspeed-wpf\Lightspeed-wpf.csproj"

if (-not (Test-Path $csprojPath)) {
    Write-Host "Error: Project file not found at $csprojPath" -ForegroundColor Red
    exit 1
}

$publishDir = Join-Path $projectDir $OutputDir

Write-Host "Building single-file executable..." -ForegroundColor Yellow

dotnet publish $csprojPath `
    -r win-x64 `
    -p:PublishSingleFile=True `
    --self-contained false `
    --output $publishDir

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $publishDir "Lightspeed-wpf.exe"
    
    if (Test-Path $exePath) {
        $size = (Get-Item $exePath).Length / 1MB
        Write-Host ""
        Write-Host "Build successful!" -ForegroundColor Green
        Write-Host "Output: $exePath" -ForegroundColor Cyan
        Write-Host "Size: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan
        Write-Host "Done!" -ForegroundColor Green
    } else {
        Write-Host "Error: Exe file not found after build" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}