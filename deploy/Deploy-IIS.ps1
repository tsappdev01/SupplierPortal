<#
.SYNOPSIS
    Creates (or updates) the IIS application pool and website for the Supplier Portal.

.DESCRIPTION
    - Creates an IIS application pool set to "No Managed Code" (required for ASP.NET Core,
      which runs out-of-process/in-process via the ASP.NET Core Module, not the .NET CLR).
    - Creates the website bound to the given host header, pointing at the physical path.
    - Ensures the writable 'doc' (uploaded supplier documents) and 'keys' (Data Protection)
      folders exist and grants the app pool identity Modify rights on them.
    - Optionally adds an HTTPS binding if a certificate thumbprint is supplied.

    Run from an elevated (Administrator) PowerShell prompt on the web server.

.EXAMPLE
    .\Deploy-IIS.ps1

.EXAMPLE
    .\Deploy-IIS.ps1 -CertThumbprint "AB12CD34...EF"
#>
[CmdletBinding()]
param(
    [string]$SiteName       = "bp.dipark.com",
    [string]$HostName       = "bp.dipark.com",
    [string]$PhysicalPath   = "C:\Websites\SupplierPortal",
    [string]$AppPoolName    = "SupplierPortal",
    [int]   $HttpPort       = 80,
    [int]   $HttpsPort      = 443,
    [string]$CertThumbprint = "",  # optional; if set, an HTTPS binding + SNI mapping is added
    # UAT single-server layout: the supplier self-service portal is served by this same app under a
    # second host header. Set to "" to skip it (e.g. when the DMZ SelfService app is deployed separately).
    [string]$SelfServiceHostName       = "bps.dipark.com",
    [string]$SelfServiceCertThumbprint = ""   # optional; falls back to -CertThumbprint if a shared/wildcard cert covers both
)

$ErrorActionPreference = "Stop"

# --- must be elevated ---
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) { throw "Please run this script from an elevated (Administrator) PowerShell session." }

Import-Module WebAdministration

Write-Host "== Supplier Portal IIS deployment ==" -ForegroundColor Cyan
Write-Host "Site: $SiteName  Host: $HostName  Path: $PhysicalPath  AppPool: $AppPoolName"

# --- physical path + writable subfolders ---
New-Item -ItemType Directory -Force -Path $PhysicalPath        | Out-Null
New-Item -ItemType Directory -Force -Path "$PhysicalPath\doc"  | Out-Null
New-Item -ItemType Directory -Force -Path "$PhysicalPath\keys" | Out-Null

# --- application pool (No Managed Code = ASP.NET Core) ---
if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Host "Created app pool '$AppPoolName'." -ForegroundColor Green
} else {
    Write-Host "App pool '$AppPoolName' already exists - updating settings."
}
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""      # "No Managed Code"
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedPipelineMode  -Value 0        # Integrated
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode            -Value 1        # AlwaysRunning
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)  # don't idle-stop

# --- website ---
if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    New-Website -Name $SiteName -PhysicalPath $PhysicalPath -ApplicationPool $AppPoolName `
                -Port $HttpPort -HostHeader $HostName | Out-Null
    Write-Host "Created website '$SiteName' (http://${HostName}:$HttpPort)." -ForegroundColor Green
} else {
    Write-Host "Website '$SiteName' already exists - updating path/app pool/binding."
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath    -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    if (-not (Get-WebBinding -Name $SiteName -Protocol http -HostHeader $HostName -Port $HttpPort -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $SiteName -Protocol http -Port $HttpPort -HostHeader $HostName
    }
}

# --- optional HTTPS binding ---
if ($CertThumbprint -ne "") {
    if (-not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $HostName -Port $HttpsPort -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $HostName -SslFlags 1  # SNI
    }
    $cert = Get-Item "Cert:\LocalMachine\My\$CertThumbprint"
    New-Item -Path "IIS:\SslBindings\!$HttpsPort!$HostName" -Value $cert -SSLFlags 1 -ErrorAction SilentlyContinue | Out-Null
    Write-Host "Added HTTPS binding on port $HttpsPort using cert $CertThumbprint." -ForegroundColor Green
} else {
    Write-Host "No -CertThumbprint supplied; skipping HTTPS binding (add it later for production)." -ForegroundColor Yellow
}

# --- second host header (UAT): the supplier self-service portal shares this same app ---
if ($SelfServiceHostName -ne "") {
    if (-not (Get-WebBinding -Name $SiteName -Protocol http -HostHeader $SelfServiceHostName -Port $HttpPort -ErrorAction SilentlyContinue)) {
        New-WebBinding -Name $SiteName -Protocol http -Port $HttpPort -HostHeader $SelfServiceHostName
        Write-Host "Added HTTP binding for $SelfServiceHostName." -ForegroundColor Green
    }
    $ssThumb = if ($SelfServiceCertThumbprint -ne "") { $SelfServiceCertThumbprint } else { $CertThumbprint }
    if ($ssThumb -ne "") {
        if (-not (Get-WebBinding -Name $SiteName -Protocol https -HostHeader $SelfServiceHostName -Port $HttpsPort -ErrorAction SilentlyContinue)) {
            New-WebBinding -Name $SiteName -Protocol https -Port $HttpsPort -HostHeader $SelfServiceHostName -SslFlags 1  # SNI
        }
        $ssCert = Get-Item "Cert:\LocalMachine\My\$ssThumb"
        New-Item -Path "IIS:\SslBindings\!$HttpsPort!$SelfServiceHostName" -Value $ssCert -SSLFlags 1 -ErrorAction SilentlyContinue | Out-Null
        Write-Host "Added HTTPS binding for $SelfServiceHostName using cert $ssThumb." -ForegroundColor Green
    } else {
        Write-Host "No cert for $SelfServiceHostName; added HTTP only (add HTTPS later)." -ForegroundColor Yellow
    }
}

# --- permissions: app pool identity needs Modify on doc + keys (uploads & auth keys) ---
$identity = "IIS AppPool\$AppPoolName"
foreach ($p in @("$PhysicalPath\doc", "$PhysicalPath\keys")) {
    & icacls $p /grant "${identity}:(OI)(CI)M" /T | Out-Null
}
Write-Host "Granted '$identity' Modify on doc\ and keys\." -ForegroundColor Green

# --- start ---
Restart-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Done. Browse to http://$HostName (ensure DNS resolves to this server)." -ForegroundColor Cyan
Write-Host "Reminder: create $PhysicalPath\appsettings.Local.json with the SQL connection string before first request." -ForegroundColor Yellow

# Return success explicitly so a non-zero $LASTEXITCODE left by an external tool (e.g. icacls) is not
# misreported as a failed step. Real failures above use `throw`.
exit 0
