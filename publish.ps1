# Publish Le Dormeur as a self-contained single-file Windows executable.
# Usage: .\publish.ps1
# Optional: .\publish.ps1 -Runtime win-x64 -Configuration Release

param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
Set-Location $root

$project = Join-Path $root "src\LeDormeur\LeDormeur.csproj"
if (-not (Test-Path $project)) {
    Write-Error "Project not found: $project"
    exit 1
}

Write-Host "Publishing 'Le Dormeur' ($Configuration / $Runtime) -> $Output ..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $Output

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

$exe = Join-Path $root (Join-Path $Output "LeDormeur.exe")
if (Test-Path $exe) {
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 2)
    Write-Host "Done: $exe ($sizeMb MB)" -ForegroundColor Green
} else {
    Write-Host "Publish finished (check folder: $Output)" -ForegroundColor Yellow
}
