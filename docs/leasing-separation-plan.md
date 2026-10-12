# Landlord (Leasing) separation — execution plan (Option B)

**Goal:** Extract the Landlord portal and its self-service into **fully separate, independently-runnable apps** within the same solution/repo, isolated from the Supplier, Customer and Broker portals.

**Decisions (agreed 2026-10-11):**
- **Separate database** for Landlord — its own identity (Users/roles), Business Units, Lookups and reference data; Property via the shared `dbo.Property` view (cross-db synonym/link or sync).
- **Deployment:** two new IIS sites on UATWEB01 (Landlord internal + Landlord DMZ), extending the UAT pipeline.
- **Projects:** `SupplierPortal.Leasing.Web` (internal), `SupplierPortal.Leasing.SelfService` (DMZ), `SupplierPortal.Shared` (extracted core). Namespaces `SupplierPortal.Leasing.*`.

**Estimated effort:** ~15–20+ developer-days. Executed in phases; the solution builds green at every checkpoint.

---

## Target solution layout
```
SupplierPortal.Shared          (RCL)  — shared core: identity/BU/lookup models + services, auth,
                                        email infra, audit, layout + shared admin components, wwwroot
SupplierPortal.Contracts              — self-service DTOs (existing)
SupplierPortal.Web             (web)  — Supplier + Customer internal portal (Leasing removed)
SupplierPortal.SelfService     (web)  — Supplier + Customer DMZ (Leasing removed)
SupplierPortal.Leasing.Web     (web)  — Landlord internal portal  [new]  -> Leasing DB
SupplierPortal.Leasing.SelfService(web)— Landlord DMZ self-service [new]  -> Leasing API
SupplierPortal.Api             (web)  — (existing internal API)
SupplierPortal.Tests                  — tests (Leasing tests retargeted to Shared/Leasing)
```

## Phases

- [x] **Phase 1 — Scaffold.** Create `SupplierPortal.Shared`, `SupplierPortal.Leasing.Web`, `SupplierPortal.Leasing.SelfService`; add to solution; set references; solution builds green. *(done)*

- [ ] **Phase 2 — Extract shared core into `SupplierPortal.Shared`.**
  Move the framework pieces Landlord depends on and that are reusable: models (`User`, `BusinessUnit`, `LookupValue`, `Enums`/`PortalType`), auth (`AuthService`, `ClaimsPrincipalExtensions`, `AppClaims`), email (`EmailDispatcher`, `EmailBrandingService`), `AuditService`, and `UserService`/`BusinessUnitService`/`LookupService` refactored against a DbContext **abstraction** (so each app binds its own context). Shared Blazor: `MainLayout`, nav shell, `LoadingIndicator`, and the shared admin pages (Users, Business Units, Lookups, field-config base) + shared `wwwroot`. Keep `SupplierPortal.Web` compiling by referencing Shared.

- [ ] **Phase 3 — Landlord database + DbContext.**
  A `LeasingAppDbContext` that owns the identity/BU/lookup tables **and** the `leasing.*` tables in the **separate Leasing database**; migrations + seeding (admin user, BUs, lookups). Resolve Property access from the separate DB (synonym/view over `dbo.Property`, or a read sync).

- [ ] **Phase 4 — Landlord internal app (`SupplierPortal.Leasing.Web`).**
  Move `Components/Leasing`, `Services/Leasing`, `Models/Leasing`, `LandlordFieldSettings`, `LandlordBell`; stand up Program.cs (auth, DI, layout, nav, routing) against the Leasing DB.

- [ ] **Phase 5 — Landlord DMZ app (`SupplierPortal.Leasing.SelfService`).**
  Move `LandlordVerify`/`LandlordForm` + `LandlordApiClient`; host the Landlord self-service API (OTP/UAE PASS/Circularo) in `Leasing.Web`; mirror the existing DMZ pattern.

- [ ] **Phase 6 — Strip Leasing from the current apps.**
  Remove Leasing components/services/models/data, `LandlordSelfServiceEndpoints`, nav + Program.cs DI, and `LeasingDbContext` from `SupplierPortal.Web`/`.SelfService`. Supplier/Customer unaffected.

- [ ] **Phase 7 — Data migration.**
  Move existing `leasing.*` data and the needed identity/BU/lookup subset into the new Leasing database (scripts); verify Property access.

- [ ] **Phase 8 — Tests.**
  Retarget/duplicate Leasing-related tests (PDF, contact validation, field mapping) to the new projects; whole solution green.

- [ ] **Phase 9 — Deployment.**
  Two new IIS sites on UATWEB01 + extend `azure-pipelines-uat.yml` to publish/deploy `Leasing.Web` and `Leasing.SelfService`.

## Open sub-decisions (to confirm as we reach them)
- Property data from the separate DB: **cross-database synonym/view** vs **periodic read-sync** (affects Phase 3/7).
- Shared reference data (Business Units, Lookups): seed independently in the Leasing DB, or one-time copy + thereafter maintained separately.
- Whether the two Landlord apps share one database or the DMZ stays database-free calling `Leasing.Web` (recommended: DMZ stays database-free, as today).

---

## Status (2026-10-12): code-complete on `claude/leasing-phase4-wip`

All phases built and green (46 tests). Both Landlord apps compile; the internal
host reaches DB-migrate and the DMZ app serves HTTP 200 (smoke-tested).

Deliverables: `SupplierPortal.Shared` (framework + `IAppDbContext` seam),
`SupplierPortal.Leasing.Web` (internal, own DB), `SupplierPortal.Leasing.SelfService`
(DMZ), the `AddSharedFrameworkTables` migration, `LeasingDbSeeder`, appsettings
templates, `deploy/Leasing-DB-Setup.sql`, `deploy/Leasing-Data-Migration.sql`, and
the extended `azure-pipelines-uat.yml` (two new IIS sites).

### Deploy runbook (UATWEB01 + SQL Server — outside the sandbox)
1. Create the **Landlord database** (e.g. `BP_Landlord`) on the SQL instance.
2. Create the **two IIS sites** on UATWEB01 (App Pool = No Managed Code), set
   `landlordSiteName` / `landlordDmzSiteName` in `azure-pipelines-uat.yml` to match.
3. Put the filled **`appsettings.Local.json`** on each site (from the templates):
   the Landlord DB connection string, and a matching `ApiSecurity:Clients[0].ClientSecret`
   (Leasing.Web) ⇔ `Api:ClientSecret` (DMZ); set `Api:BaseUrl` to the internal site.
4. Run the pipeline (or publish manually) so EF creates the schema on first start.
5. Run **`deploy/Leasing-DB-Setup.sql`** against the Landlord DB (`@MainDb = BP`)
   — the `Property` + `MaintainTenantUAEPassEmails` synonyms.
6. Run **`deploy/Leasing-Data-Migration.sql`** against the Landlord DB
   (`@MainDb = BP`) to copy existing landlord + reference/identity data.
   (Requires SQL Server 2017+ for STRING_AGG.)
7. Functional-test: sign in to the internal portal, open a registration; open the
   DMZ link + OTP flow. Change the seeded admin password.
8. Merge `claude/leasing-phase4-wip` into the deploy branch (coordinated release:
   the main app drops Landlord as the two new apps go live).
