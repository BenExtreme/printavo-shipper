<#
.SYNOPSIS
    Builds Printavo Shipper into a single, self-contained .exe that can be
    copied to any Windows 11 PC and run directly — no .NET install required
    on the target machine.

.EXAMPLE
    .\publish.ps1
    Produces "publish\Printavo Shipper.exe"
#>

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\PrintavoShipper.App\PrintavoShipper.App.csproj"
$output = Join-Path $root "publish"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $dotnet = "dotnet"
}

& $dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:DebugType=none

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed (exit code $LASTEXITCODE)."
}

# Single-file publish still drops a couple of non-essential companion files
# (e.g. an empty .pdb placeholder some SDK versions still emit) — clean
# those up so the output folder really is just the one exe to hand off.
Get-ChildItem $output -File | Where-Object { $_.Extension -ne ".exe" } | Remove-Item -Force

# Staff-facing file name. Renamed after publishing rather than via
# AssemblyName, which XAML pack URIs depend on — a single-file bundle runs
# fine under any file name.
$exe = Join-Path $output "Printavo Shipper.exe"
Move-Item (Join-Path $output "PrintavoShipper.App.exe") $exe -Force
$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Published: $exe ($sizeMb MB)" -ForegroundColor Green
Write-Host "Copy this single file to any Windows 11 (x64) PC and run it directly."
