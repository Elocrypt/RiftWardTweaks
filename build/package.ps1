<#
.SYNOPSIS
    Packages RiftWardTweaks into a release zip ready for ModDB upload.

.PARAMETER Configuration
    Build configuration. Default: Release.

.PARAMETER Version
    Version string for the zip filename (e.g. "0.2.0").
    If omitted, reads from modinfo.json.

.EXAMPLE
    ./build/package.ps1 -Configuration Release -Version 0.2.0
#>
param(
    [string]$Configuration = "Release",
    [string]$Version
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$binDir = Join-Path $repoRoot "/src/RiftWardTweaks/bin/$Configuration"
$distDir = Join-Path $repoRoot "build/dist"

# Read version from modinfo.json if not provided.
if (-not $Version) {
    $modinfo = Get-Content (Join-Path $repoRoot "modinfo.json") -Raw | ConvertFrom-Json
    $Version = $modinfo.version
    Write-Host "Version from modinfo.json: $Version"
}

# Verify the build output exists.
$dllPath = Join-Path $binDir "RiftWardTweaks.dll"
$modinfoPath = Join-Path $binDir "modinfo.json"

if (-not (Test-Path $dllPath)) {
    Write-Error "Build output not found at '$dllPath'. Run 'dotnet build -c $Configuration' first."
    exit 1
}

if (-not (Test-Path $modinfoPath)) {
    Write-Error "modinfo.json not found at '$modinfoPath'. Check CopyToOutputDirectory in csproj."
    exit 1
}

# Create dist directory.
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# Stage files in a temp directory matching the mod folder structure.
$stageName = "RiftWardTweaks"
$stageDir = Join-Path ([System.IO.Path]::GetTempPath()) "riftwardtweaks-package-$([System.Guid]::NewGuid().ToString('N'))"
$modDir = Join-Path $stageDir $stageName

New-Item -ItemType Directory -Path $modDir -Force | Out-Null

# Copy required files.
Copy-Item $dllPath $modDir
Copy-Item $modinfoPath $modDir

# Copy modicon if it exists.
$modiconSrc = Join-Path $repoRoot "modicon.png"
if (Test-Path $modiconSrc) {
    Copy-Item $modiconSrc $modDir
}

# Copy resources if they exist.
$resourcesSrc = Join-Path $binDir "resources"
if (Test-Path $resourcesSrc) {
    Copy-Item -Recurse $resourcesSrc $modDir
}

# Create the zip.
$zipName = "RiftWardTweaks_${Version}.zip"
$zipPath = Join-Path $distDir $zipName

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$modDir/*" -DestinationPath $zipPath -CompressionLevel Optimal

$zipSize = (Get-Item $zipPath).Length
Write-Host ""
Write-Host "Packaged: $zipPath ($([math]::Round($zipSize / 1024, 1)) KB)"
Write-Host "Contents:"
Get-ChildItem $modDir -Recurse -File | ForEach-Object {
    Write-Host "  $($_.FullName.Replace($modDir, ''))"
}

# Cleanup.
Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue