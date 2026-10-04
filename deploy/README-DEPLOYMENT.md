# Supplier Portal — IIS Deployment Guide

Target: **IIS site `bp.dipark.com`**, files at **`C:\Websites\SupplierPortal`**.

The deployment ZIP (`SupplierPortal-deploy.zip`) contains the published app plus:
- `Deploy-IIS.ps1` — creates the IIS app pool + site and sets permissions
- `appsettings.Local.json.template` — connection-string template
- this guide

---

## Running the PowerShell scripts

Windows blocks unsigned scripts by default (`... is not digitally signed`). Run them by
bypassing the execution policy for that process only — this does **not** change the machine policy:

```powershell
powershell -ExecutionPolicy Bypass -File .\Deploy-IIS.ps1
# or, before running normally:
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

If a script was extracted from a downloaded ZIP, also clear the "blocked" flag once:
`Unblock-File .\*.ps1`.

---

## 1. Server prerequisites (once per server)

1. **Windows Server with IIS** enabled (Web Server role), including *IIS Management Scripts and Tools*
   (needed for the PowerShell `WebAdministration` module).
2. **.NET 8 ASP.NET Core Hosting Bundle** — **required.** It installs the .NET 8 runtime **and** the
   ASP.NET Core Module (ANCM) that IIS uses to host the app.
   Download: <https://dotnet.microsoft.com/download/dotnet/8.0> → *Hosting Bundle*.
   After installing, run `net stop was /y && net start w3svc` (or reboot) so IIS picks it up.
3. **Network access** from this server to the SQL Server (e.g. `UATWEB01`, database `BP`).
4. **DNS**: `bp.dipark.com` must resolve to this server's IP (internal DNS or hosts file for testing).

> Verify the runtime after install: `dotnet --list-runtimes` should list
> `Microsoft.AspNetCore.App 8.0.x`.

---

## 2. Copy the files

Extract the contents of `SupplierPortal-deploy.zip` (the published app) into:

```
C:\Websites\SupplierPortal
```

`SupplierPortal.Web.dll`, `web.config`, the `wwwroot` folder, etc. should sit directly under that path.

---

## 3. Set the connection string

Copy `appsettings.Local.json.template` to **`C:\Websites\SupplierPortal\appsettings.Local.json`**
and edit the connection string (and, optionally, the default admin / DetailedErrors).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=UATWEB01;Database=BP;User Id=bpuser;Password=***;TrustServerCertificate=True;MultipleActiveResultSets=True;"
  }
}
```

This file is git-ignored by design — real credentials never go in source control.
On first request the app **automatically applies EF Core migrations and seeds** master data
(so no manual DB step is required, as long as the DB is reachable and the login can create/alter it).

---

## 4. Create the IIS site

From an **elevated PowerShell** prompt in `C:\Websites\SupplierPortal`:

```powershell
powershell -ExecutionPolicy Bypass -File .\Deploy-IIS.ps1
```

This creates:
- App pool **SupplierPortal** — *No Managed Code*, Integrated, AlwaysRunning, no idle timeout.
- Site **bp.dipark.com** → `C:\Websites\SupplierPortal`, HTTP binding on port 80 (host header `bp.dipark.com`).
- Writable `doc\` (uploaded documents) and `keys\` (Data Protection) folders, with Modify rights
  granted to `IIS AppPool\SupplierPortal`.

### Add HTTPS (recommended for production)

Import your TLS certificate into `LocalMachine\My`, then re-run with its thumbprint:

```powershell
.\Deploy-IIS.ps1 -CertThumbprint "AB12CD34...EF"
```

(or add the HTTPS binding + certificate manually in IIS Manager → *Bindings*).

---

## 5. Browse

Open `http://bp.dipark.com` (or `https://…` once the cert is bound). First load triggers
migrate + seed, then the login page appears.

Default administrator on a fresh database (change immediately by creating a real admin via
**Users → Add User**, which auto-deactivates the default one):

- Email: `admin@supplierportal.local`
- Password: `Admin@12345`

---

## 6. Email (supplier notifications & OTP)

Notification and OTP emails are sent via **SQL Server Database Mail** using the profile named **`BP`**.
Ensure that profile exists and is configured on the SQL Server instance. If it isn't, the app still
runs — only the "send email" actions report a failure.

---

## Manual IIS steps (if you prefer not to run the script)

1. IIS Manager → **Application Pools → Add** → name `SupplierPortal`, .NET CLR version **No Managed Code**, Integrated.
2. **Sites → Add Website** → name `bp.dipark.com`, physical path `C:\Websites\SupplierPortal`,
   app pool `SupplierPortal`, host name `bp.dipark.com`.
3. Grant `IIS AppPool\SupplierPortal` **Modify** on `C:\Websites\SupplierPortal\doc` and `\keys`:
   ```cmd
   icacls "C:\Websites\SupplierPortal\doc"  /grant "IIS AppPool\SupplierPortal:(OI)(CI)M" /T
   icacls "C:\Websites\SupplierPortal\keys" /grant "IIS AppPool\SupplierPortal:(OI)(CI)M" /T
   ```

---

## Updating the app later

1. Stop the site (or the app pool).
2. Overwrite the files under `C:\Websites\SupplierPortal` with the new publish output
   (keep your `appsettings.Local.json`, `doc\`, and `keys\`).
3. Start the site. Pending EF migrations apply automatically on the next request.

> A convenient way to overwrite while the app is running is to drop an `app_offline.htm`
> in the folder first (IIS stops the app), copy files, then delete `app_offline.htm`.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| **HTTP 500.19** | `web.config` unreadable or ANCM missing → install the **Hosting Bundle**. |
| **HTTP 500.30 / 500.31 / 502.5** | App failed to start — usually a bad connection string or missing runtime. Set `DetailedErrors: true` temporarily and check **stdout logs**. |
| **See startup errors** | In `web.config`, set `stdoutLogEnabled="true"` and `stdoutLogFile=".\logs\stdout"`, create a writable `logs\` folder, reproduce, then read the log. Turn it back off afterwards. |
| **Users logged out after a recycle** | Ensure the `keys\` folder exists and is writable by the app pool (the script handles this). |
| **Uploads fail** | `doc\` folder must be writable by `IIS AppPool\SupplierPortal`. |
| **Cannot reach DB** | Verify network/firewall to the SQL Server and that the SQL login has rights to create/migrate the `BP` database. |
