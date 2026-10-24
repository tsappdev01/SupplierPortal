<#
.SYNOPSIS
    Deploys a published ASP.NET Core app into an EXISTING IIS site on this server (in-place update).

.DESCRIPTION
    Intended to run on the web server itself (e.g. the UATWEB01 self-hosted Azure DevOps agent).
    Given the IIS *site name*, it resolves the site's physical path and application pool from IIS,
    so paths never have to be hard-coded in the pipeline. It then:

      1. Drops an app_offline.htm into the site (IIS unloads the app and releases file locks).
      2. Stops the application pool (belt-and-braces so no file is held open).
      3. Mirrors the publish output into the site folder with robocopy /MIR, but PRESERVES
         server-only state that must survive a deployment:
            - appsettings.Local.json   (real connection string / secrets - git-ignored)
            - doc\                      (uploaded documents)
            - keys\                     (Data Protection keys - losing these logs everyone out)
            - logs\                     (stdout logs)
      4. Starts the application pool and removes app_offline.htm.
      5. Optionally warms the site up with a request.

    The published web.config IS overwritten (it is regenerated on every publish and is not host-specific).

.NOTES
    Requires: IIS + "IIS Management Scripts and Tools" (WebAdministration module), and the
    .NET 8 ASP.NET Core Hosting Bundle installed on the server. Run elevated.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $SiteName,     # IIS site name, e.g. usupplierportal.dubaiinvestments.com
    [Parameter(Mandatory = $true)] [string] $SourcePath,   # folder containing the publish output (dll, web.config, wwwroot)
    [string] $WarmupUrl = "",                              # optional URL to hit after start (e.g. https://host/)
    [string[]] $PreserveFiles = @("appsettings.Local.json"),
    [string[]] $PreserveDirs  = @("doc", "keys", "logs")
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

Write-Host "== Deploy '$SiteName' ==" -ForegroundColor Cyan

# --- resolve the live site from IIS ---
$site = Get-Website | Where-Object { $_.Name -eq $SiteName }
if (-not $site) { throw "IIS site '$SiteName' not found on this server. Check the site name (Get-Website) or create it first." }

$dest    = [Environment]::ExpandEnvironmentVariables($site.physicalPath)
$appPool = $site.applicationPool
if ([string]::IsNullOrWhiteSpace($dest)) { throw "Could not resolve the physical path for site '$SiteName'." }
Write-Host "Physical path : $dest"
Write-Host "App pool      : $appPool"

if (-not (Test-Path $SourcePath)) { throw "Publish source '$SourcePath' does not exist." }
if (-not (Test-Path "$SourcePath\web.config")) {
    Write-Warning "No web.config found in '$SourcePath' - is this an ASP.NET Core publish output folder?"
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
foreach ($d in $PreserveDirs) { New-Item -ItemType Directory -Force -Path (Join-Path $dest $d) | Out-Null }

$offline = Join-Path $dest "app_offline.htm"

try {
    # 1. app_offline - IIS stops routing to the app and releases the managed assemblies.
    Set-Content -Path $offline -Encoding UTF8 -Value @"
<!doctype html><html><head><meta charset="utf-8"><title>Updating...</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif;padding:3rem;color:#1F3864">
<h1>We'll be right back</h1><p>This site is being updated. Please try again in a moment.</p></body></html>
"@

    # 2. stop the app pool (wait until actually stopped).
    if ((Get-WebAppPoolState -Name $appPool).Value -ne "Stopped") {
        Stop-WebAppPool -Name $appPool
        for ($i = 0; $i -lt 30 -and (Get-WebAppPoolState -Name $appPool).Value -ne "Stopped"; $i++) { Start-Sleep -Milliseconds 500 }
    }
    Write-Host "App pool stopped." -ForegroundColor Yellow

    # 3. mirror publish output, excluding the files/dirs that must survive a deploy.
    #    /MIR = mirror (purges stale files), but excluded items are never touched.
    $xf = @("app_offline.htm") + $PreserveFiles
    $xd = $PreserveDirs | ForEach-Object { Join-Path $dest $_ }
    $roboArgs = @($SourcePath, $dest, "/MIR", "/R:2", "/W:2", "/NFL", "/NDL", "/NP", "/NJH")
    $roboArgs += "/XF"; $roboArgs += $xf
    $roboArgs += "/XD"; $roboArgs += $xd
    & robocopy @roboArgs | Out-Null
    $rc = $LASTEXITCODE
    # robocopy exit codes: 0-7 are success (bits for copied/extra/mismatch); >=8 is a real failure.
    if ($rc -ge 8) { throw "robocopy failed with exit code $rc." }
    Write-Host "Files mirrored (robocopy rc=$rc)." -ForegroundColor Green
}
finally {
    # 4. always bring the app back up, even if the copy failed.
    if (Test-Path $offline) { Remove-Item $offline -Force -ErrorAction SilentlyContinue }
    if ((Get-WebAppPoolState -Name $appPool).Value -ne "Started") { Start-WebAppPool -Name $appPool }
    Write-Host "App pool started." -ForegroundColor Green
}

# 5. optional warmup (first hit triggers EF migrate/seed on the internal app).
if ($WarmupUrl) {
    try {
        $r = Invoke-WebRequest -Uri $WarmupUrl -UseBasicParsing -TimeoutSec 120
        Write-Host "Warmup $WarmupUrl -> HTTP $($r.StatusCode)" -ForegroundColor Green
    } catch {
        Write-Warning "Warmup request to $WarmupUrl failed: $($_.Exception.Message)"
    }
}

Write-Host "== Deploy '$SiteName' complete ==" -ForegroundColor Cyan
