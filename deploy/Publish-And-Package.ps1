<#
.SYNOPSIS
    Builds the Supplier Portal IIS deployment package (publish -> trim -> zip).

.DESCRIPTION
    Produces a ready-to-deploy ZIP containing the Release publish output plus the
    deployment helpers (Deploy-IIS.ps1, README-DEPLOYMENT.md, appsettings.Local.json.template).

    Steps:
      1. dotnet publish -c Release (framework-dependent, win-x64).
      2. Trim runtimes/ to win-x64 (+ shared 'win') only - the payload targets Windows x64 IIS.
      3. Drop dev-only / debug files (appsettings.Development.json, *.pdb).
      4. Copy the deploy helpers into the package.
      5. Zip to dist\SupplierPortal-deploy.zip.

    Requires the .NET 8 SDK on the build machine (dotnet on PATH). This does NOT need to run
    on the web server - build here, copy the ZIP to the server, follow README-DEPLOYMENT.md.

.EXAMPLE
    .\Publish-And-Package.ps1

.EXAMPLE
    .\Publish-And-Package.ps1 -Configuration Release -Runtime win-x64 -SelfContained
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [switch]$SelfContained,                 # if set, bundles the .NET runtime (no Hosting Bundle needed; larger zip)
    [string]$ZipPath                        # output path; defaults to <repo>\dist\SupplierPortal-deploy.zip
)

$ErrorActionPreference = "Stop"

# --- resolve paths relative to this script (repo\deploy\) ---
$deployDir  = $PSScriptRoot
$repoRoot   = Split-Path $deployDir -Parent
$project    = Join-Path $repoRoot "src\SupplierPortal.Web\SupplierPortal.Web.csproj"
$stageDir   = Join-Path $repoRoot "dist\publish"
$distDir    = Join-Path $repoRoot "dist"
if (-not $ZipPath) { $ZipPath = Join-Path $distDir "SupplierPortal-deploy.zip" }

if (-not (Test-Path $project)) { throw "Project not found at $project" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "The .NET SDK ('dotnet') is not on PATH." }

Write-Host "== Publish & package Supplier Portal ==" -ForegroundColor Cyan
Write-Host "Project: $project"
Write-Host "Config:  $Configuration   Runtime: $Runtime   SelfContained: $($SelfContained.IsPresent)"

# --- clean staging ---
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

# --- 1. publish ---
$scArg = if ($SelfContained) { "--self-contained" } else { "--no-self-contained" }
& dotnet publish $project -c $Configuration -r $Runtime $scArg -o $stageDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)." }

# --- 2. trim runtimes to the target platform (framework-dependent only; self-contained is already RID-specific) ---
$runtimesDir = Join-Path $stageDir "runtimes"
if ((-not $SelfContained) -and (Test-Path $runtimesDir)) {
    Get-ChildItem $runtimesDir -Directory |
        Where-Object { $_.Name -notin @($Runtime, "win") } |
        Remove-Item -Recurse -Force
}

# --- 3. drop dev-only / debug files ---
Get-ChildItem $stageDir -Recurse -Include *.pdb | Remove-Item -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stageDir "appsettings.Development.json") -Force -ErrorAction SilentlyContinue
# never ship real local secrets
Remove-Item (Join-Path $stageDir "appsettings.Local.json") -Force -ErrorAction SilentlyContinue

# --- 4. copy deploy helpers into the package ---
foreach ($f in @("Deploy-IIS.ps1", "README-DEPLOYMENT.md", "README-UAT.md", "appsettings.Local.json.template")) {
    Copy-Item (Join-Path $deployDir $f) (Join-Path $stageDir $f) -Force
}

# --- 5. zip ---
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $ZipPath -CompressionLevel Optimal

$sizeMB = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Package created: $ZipPath  ($sizeMB MB)" -ForegroundColor Green
Write-Host "Copy it to the server and follow README-DEPLOYMENT.md." -ForegroundColor Cyan

# Return success explicitly so a non-zero $LASTEXITCODE left by an external tool (e.g. dotnet) is not
# misreported as a failed step. Real failures above use `throw`.
exit 0
