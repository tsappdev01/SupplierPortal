# Supplier Portal for Dubai Investments Entities

Internal supplier master-data management portal. .NET 8 Blazor Web App
(Interactive Server render mode) backed by SQL Server via EF Core.

## Stack

- .NET 8 LTS, Blazor Web App (Interactive Server) — targets .NET 8 (not 9/10)
  for compatibility with Visual Studio 2022; `global.json` pins the SDK
- EF Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`), code-first migrations
- Cookie authentication with custom `User`/`BusinessUnit` tables (not ASP.NET Core Identity)
- ClosedXML (Excel import), CsvHelper (CSV export), QuestPDF (PDF export, Community license)

## Project layout

```
src/SupplierPortal.Web/
  Program.cs               DI, auth, EF Core, seeding, /export/{format} endpoints
  Data/                     ApplicationDbContext, DbSeeder, Migrations, LOV/master-data seed files
  Models/                   BusinessUnit, User, Supplier, SupplierFormModel, LookupValue, Bank, enums
  Services/                 AuthService, BusinessUnitService, UserService, SupplierService,
                             ExcelImportService, ExportService
  Components/
    Account/                Login, Register, AccessDenied (static SSR pages)
    Admin/                  BusinessUnits, Users (Administrator only)
    Supplier/                Upload, Dashboard, Register, Edit, SupplierForm (shared 5-section UI)
    Layout/                  MainLayout, NavMenu (sidebar, role-aware), AccountLayout
```

## Business Units

The tenancy/scoping entity is a **Business Unit** (not "Company"): every
`User` and `Supplier` belongs to one. Administrators manage them at
**Business Units → Add Business Unit** (Name + a short Code, e.g. "DIPD" for
"Dubai Investments Park Development" — auto-generated from the name's
initials if left blank). Business Units also double as the lookup source for
a supplier's Procurement/Client/Bill-to BU assignment.

## Supplier Registration Form

**Register Supplier** (`/supplier/register`) presents the full Oracle Fusion
supplier field set across 5 sections (Business/Organization, Address,
Contact, Business Unit & Site Assignment, Payment/Banking), matching the
Dubai Investments mockup design. The same `SupplierForm` component is reused
by **Edit** (`/supplier/edit/{id}`) for existing records.

- Supplier Number is system-generated on creation as `{BusinessUnitCode}-{6-digit sequence}`
  (e.g. `DIPD-000123`) and shown read-only.
- Required fields on this form (Supplier Name, Tax Organization Type,
  Taxpayer Country, Trade License Number, Address Name, Address Line 1,
  City, Province, Country, Supplier Site, Procurement BU, Client BU, Payment
  Method, Account Country Code, Account Number) are enforced by a separate
  `SupplierFormModel` view-model — the underlying `Supplier` database columns
  stay nullable so bulk-imported historical data (often incomplete) remains
  valid. Bank Name/Branch Name are marked conditionally required (informational
  only, not hard-enforced).
- "Save as Draft" and "Submit for Approval" both save the record as Active —
  there's no separate draft/approval workflow yet.

## First-time setup

1. **Connection string** — real UAT credentials must never be committed. Create
   `src/SupplierPortal.Web/appsettings.Local.json` (gitignored) with:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=UATWEB01;Database=BP;User Id=bpuser;Password=bpuser;TrustServerCertificate=True;MultipleActiveResultSets=True;"
     }
   }
   ```

2. **Apply migrations** (run from a machine that can reach `UATWEB01` — this
   cannot be done from a sandbox without network access to it):

   ```bash
   cd src/SupplierPortal.Web
   dotnet tool install --global dotnet-ef   # if not already installed
   dotnet ef database update
   ```

   Alternatively, the app applies pending migrations automatically on startup
   (`DbSeeder.SeedAsync` calls `Database.MigrateAsync()`), so simply running
   the app against a reachable database is enough.

3. **Run**:

   ```bash
   cd src/SupplierPortal.Web
   dotnet run
   ```

## Default administrator

On first run (when no non-default Administrator exists yet), a default admin
account is seeded:

- Email: `admin@supplierportal.local`
- Password: `Admin@12345`

These can be overridden before first run via `appsettings.Local.json`:

```json
{
  "DefaultAdmin": {
    "Username": "admin",
    "Email": "admin@supplierportal.local",
    "Password": "Admin@12345"
  }
}
```

Once a **real** Administrator account is created via **Users → Add User**
(with User Type = Administrator), the default admin account is automatically
deactivated.

## User flow

1. A new user self-registers at **/account/register** with just an email and
   password. The account is inactive until an administrator links it.
2. An Administrator goes to **Users → Add User** and either:
   - enters an email that matches a pending self-registration — this
     completes that account (Username, User Type, Business Unit), or
   - creates a brand-new user directly with Username, Email, Password, User
     Type and Business Unit Name.
3. Once linked/created, the user can log in at **/account/login**.

## Supplier data

- **Business Units → Add Business Unit**: Administrators add business units first.
- **Upload Supplier Data**: upload an `.xlsx` file. If a sheet literally named
  "Supplier Data Template" exists (the official Oracle Fusion template ships
  Instructions/Supplier Data Template/Sheet1/LOV Details sheets), that sheet
  is used; otherwise the sheet with the most recognized headers is picked.
  Header matching strips the template's `*` (required) and `(LOV)` decorations,
  so `"*Tax Organization Type (LOV)"` matches the same field as a plain
  `"Tax Organization Type"` header. Recognizes the full field set described
  above (Business/Organization, Address, Contact, BU & Site Assignment,
  Payment/Banking) by header name, including Procurement/Client/Bill-to BU
  (matched against Business Unit names) and boolean columns (Yes/True/1 =
  checked). A Branch Name must match one of the selected Bank's branches.
  - Only **Supplier Number, Supplier Name, Tax Registration Number, Address
    Line 1, and City** are required — a row missing any of these is rejected
    and reported with the row number and reason.
  - An unrecognized value in any other (non-required) LOV field — e.g. a
    Payment Terms value that isn't in the seeded list — doesn't reject the
    row; that field is left blank and reported as a separate "imported with
    blanks" warning so real-world data with free-text variants still imports.
- **Supplier Dashboard**: view, edit, deactivate, print, and export (CSV/PDF)
  supplier records, plus stat tiles (Total/Active/Inactive). Normal users see
  only their own business unit's data; Administrators can filter by business
  unit or view all. CSV export includes the full field set; PDF export uses a
  curated subset so columns stay legible on a landscape page.

## Master data

Lookup values are seeded on first run from the Dubai Investments supplier LOV
export (`Data/CountrySeedData.cs`, `Data/SupplierMasterSeedData.cs`,
`Data/BankSeedData.cs`, `Data/BusinessUnitSeedData.cs`), keyed by category:

- In `LookupValues`: Tax Organization Type (9), Supplier Type (17),
  Province (7, UAE emirates), VAT Code (8, code + description), Payment
  Method (3), Payment Terms (9), Prefix (3), Currency (259, code +
  description), Country (251, with phone dialing code), Business
  Relationship and Communication Method (no source list was given for these
  two — seeded with common Oracle Fusion defaults, editable in the DB)
- Business Units (9, e.g. "Dubai Investments Park Development") are a real
  table (`BusinessUnits`), not a `LookupValues` category, since they're also
  the tenancy/scoping entity for Users and Suppliers
- Banks and Branches are relational (`Banks`/`BankBranches` tables) since
  each branch belongs to exactly one bank — 48 banks, 112 branches

All of the above are editable directly in the database after first run;
there's no in-app admin UI for managing lookup values yet.

## Detailed error logs

By default (`appsettings.json`), `DetailedErrors` is `false` and logging is at
`Information`/`Warning`. Running with `ASPNETCORE_ENVIRONMENT=Development`
picks up `appsettings.Development.json`, which turns on:

- `DetailedErrors: true` — Blazor circuit exceptions (component/render errors)
  show the real message and stack trace in the browser's error banner instead
  of the generic "An unhandled error has occurred."
- `Logging:LogLevel:Default: Debug` and
  `Microsoft.EntityFrameworkCore.Database.Command: Information` — server
  console logs include generated SQL statements.

To get the same verbosity on a non-Development host (e.g. UAT running as
`Staging`/`Production`), add the same two keys to `appsettings.Local.json`:

```json
{
  "DetailedErrors": true,
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Turn these back off once you're done — detailed errors expose internal
exception details to anyone who triggers one.

## Notes

- Any machine with the .NET 8 SDK (or Visual Studio 2022 with the ASP.NET and
  web development workload) and network access to the SQL Server can build,
  migrate, and run this project. Open `BPPortal.sln` directly in Visual Studio.
- `appsettings.Local.json` and `bin/`/`obj/` are gitignored — never commit
  real database credentials.
