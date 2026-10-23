<#
.SYNOPSIS
    Pre-deploy guard: verifies an IIS site has its server-only appsettings.Local.json
    with the required config keys filled in, BEFORE the deploy runs.

.DESCRIPTION
    Runs on the web server (the UATWEB01 self-hosted agent). Given the IIS *site name*,
    it resolves the site's physical path from IIS (exactly as Deploy-Site-Local.ps1 does),
    then checks that <physicalPath>\appsettings.Local.json exists and that each required
    key is present and non-empty. A key may also be satisfied by the equivalent environment
    variable (ConnectionStrings:DefaultConnection -> ConnectionStrings__DefaultConnection) at
    Process or Machine scope, so sites configured via env vars are not failed by mistake.

    On any failure it writes an Azure DevOps ##[error] naming the exact file and key to fix,
    and exits 1 so the deploy step does not run (no silent runtime 500). On success it exits 0.

    This never reads or prints secret VALUES — only whether each key is present and non-empty.

.NOTES
    Requires the WebAdministration module (IIS Management Scripts and Tools).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]   $SiteName,
    [string[]] $RequireKeys = @(),                      # dot/colon paths, e.g. "ConnectionStrings:DefaultConnection"
    [string]   $ConfigFile  = "appsettings.Local.json"
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

Write-Host "== Config guard: '$SiteName' ==" -ForegroundColor Cyan

# --- resolve the live site from IIS (same resolution as Deploy-Site-Local.ps1) ---
$site = Get-Website | Where-Object { $_.Name -eq $SiteName }
if (-not $site) {
    Write-Host "##[error]IIS site '$SiteName' not found on this server. Check the site name (Get-Website) or create it first."
    exit 1
}
$dest = [Environment]::ExpandEnvironmentVariables($site.physicalPath)
if ([string]::IsNullOrWhiteSpace($dest)) {
    Write-Host "##[error]Could not resolve the physical path for site '$SiteName'."
    exit 1
}
$cfgPath = Join-Path $dest $ConfigFile
Write-Host "Config file   : $cfgPath"

# Reads a value at a Process- or Machine-scope environment variable (null if unset/blank).
function Get-EnvFallback([string]$key) {
    $name = $key -replace ':', '__'
    foreach ($scope in @('Process', 'Machine')) {
        $v = [Environment]::GetEnvironmentVariable($name, $scope)
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $name }
    }
    return $null
}

# Navigates a colon/dot path through the parsed JSON; returns the leaf value or $null.
function Get-JsonValue($root, [string]$key) {
    $node = $root
    foreach ($part in ($key -split '[:.]')) {
        if ($null -eq $node) { return $null }
        $node = $node.PSObject.Properties[$part] | ForEach-Object { $_.Value }
    }
    return $node
}

$problems = @()

# 1. the server-only config file must exist.
$json = $null
if (-not (Test-Path $cfgPath)) {
    $problems += "Missing '$cfgPath'. Create it on the server with the keys below (it is never deployed, so each site keeps its own)."
} else {
    try {
        $json = Get-Content -Raw -Path $cfgPath | ConvertFrom-Json
    } catch {
        $problems += "'$cfgPath' is not valid JSON: $($_.Exception.Message)"
    }
}

# 2. each required key must be present & non-empty (or satisfied by an env var).
foreach ($key in $RequireKeys) {
    $val = if ($json) { Get-JsonValue $json $key } else { $null }
    if (-not [string]::IsNullOrWhiteSpace([string]$val)) {
        Write-Host "  OK   $key (from $ConfigFile)" -ForegroundColor Green
        continue
    }
    $env = Get-EnvFallback $key
    if ($env) {
        Write-Host "  OK   $key (from env $env)" -ForegroundColor Green
        continue
    }
    $problems += "Key '$key' is missing or empty. Set it in '$cfgPath' (or env var '$($key -replace ':','__')')."
}

if ($problems.Count -gt 0) {
    Write-Host "##[error]Config guard FAILED for site '$SiteName' — deploy skipped so the app does not crash at startup:"
    foreach ($p in $problems) { Write-Host "##[error]  - $p" }
    exit 1
}

Write-Host "== Config guard passed for '$SiteName' ==" -ForegroundColor Cyan
exit 0
