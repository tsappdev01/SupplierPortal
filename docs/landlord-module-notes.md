# Landlord / Leasing module — design & data-sourcing notes

Living spec for the Landlord (Business Partner Portal) module. Keep in sync as decisions land.

## Business context
DIP manages leasing & sub-leasing. Tenants come to DIP for **NOC**, **Ejari registration**,
**renewal** and **cancellation**. The Landlord module establishes the **verified owner record** those
processes reference. Business maintains the **land**; the landlord updates the **owner** information.

## Data sourcing — IMPORTANT
- **Land/Property/Building information is READ-ONLY, sourced from a SQL VIEW** — do **not** create,
  seed, migrate or write it.
  - Source view: `[BP].[dbo].[Property]` with columns **`Id`, `CategoryName`, `PropertyCode`, `BuildingName`**.
  - **`Id`** is a stable per-row key → this is the **PremisesId** the app links ownership to.
  - `PropertyCode` = `{Zone}-{rest}`. **Zone ∈ {597, 598}** is a Dubai Municipality zone and is a required
    part of the complete code — a PropertyCode is incomplete without it. In the UI, Zone is a **select**
    (597/598, editable/extensible via appsettings) **combined** with the editable remainder to form the
    complete PropertyCode. Examples: `597-1000`, `598-115`, `598-1147W/H-8`.
  - `CategoryName` = usage/category (Industrial, Warehouses, Labor Accommodation, Mixed residential and
    Commercial, …). `BuildingName` = building/premises name.
  - Map land as a **view-backed EF entity** `Premises` (`ToView("Property")`, key = `Id`), never a migrated table.

## Architecture — separate bounded context
- The Landlord/Leasing module uses its **own `LeasingDbContext`** (same DB): reads the `dbo.Property` view
  and owns the `leasing.*` tables, with its own migrations history table (`__EFLeasingMigrationsHistory`).
  This isolates it from the Supplier `ApplicationDbContext` (no shared ModelSnapshot / migration conflicts).
- **App-OWNED tables (EF migrations) — the only things we write:**
  - Owner/Landlord, ownership link (Owner ↔ building, per (PropertyCode, BuildingName)),
    Signatory, LandlordDocument, registration/status, field-change + audit trail.
- Verification method is **pluggable** (email OTP now; UAE Pass later — Ejari uses UAE Pass). "Decide later."

## Model (agreed)
- **Property ↔ Building is many-to-many** — CONFIRMED from the 1,000-row `bld.xlsx` dataset:
  668 property codes, 940 building names; 99 properties have >1 building (max 49 on `598-653`);
  24 buildings span >1 property (FALCON HOUSE across 32 codes; `8WD` across 6).
- Model each **view row as a "Premises" = (PropertyCode × BuildingName)** — the atomic ownable unit.
  Property and Building are two groupings over premises; their M:N falls out of the premises rows.
- **Ownership attaches to the Premises** (Owner ↔ Premises), which supports property→many buildings,
  building→many properties, different owner per building, and co-ownership — all in one link table.
- Source has **no surrogate keys** (only text columns) and **building names are not unique**
  (FALCON HOUSE recurs across 32 codes) → the natural key of a premises is `(PropertyCode, BuildingName)`
  unless the real `dbo.Property` view exposes a stable id.
- **Zone** = first 3 chars of PropertyCode (`597`/`598`). Outliers exist: `598285` (no dash) and
  `STR-597-4899...` (×3) — decide keep/exclude.
- Units not in this view; units/tenancy come with the later Tenant/Ejari phase.
- Owner is **scoped to one Business Unit** (not a globally reusable party).
- Owner info + signatories + documents are captured **per property** (landlord selects the property, then updates).
- No banking / rent-remittance fields. **Retain all documents + a complete registration trace (audit timeline).**

## Owner fields (landlord-updated)
Agreement type (Corporate/Individual), legal name, primary contact (name/email/mobile — **business-set,
shown read-only to the landlord**), secondary contact, Emirates ID (individual) / Trade Licence + MOA
(corporate), authorised signatories (single/multiple + signing rule + order), documents.

## Workflow
Statuses: **Draft → Awaiting Landlord Response → Under Review → Approved / Rejected**.
Reviewer role: **Leasing Manager** (new role alongside Administrator / Normal User).
**Every transition notifies the landlord.** Surface in the existing notification bell + activity feed.

## Reuse
Separate Landlord module reusing shared infra (Business Units, roles, tokenized-link + OTP self-service,
notifications, DMZ portal/API pattern). Supplier module left untouched.

## Open questions (confirm before migration)
1. Ownership assigned at the **Premises = (PropertyCode, BuildingName)** level (recommended) — confirm.
2. Does the real `dbo.Property` view expose a **stable id** per row (premises id)? If not we key on
   `(PropertyCode, BuildingName)`.
3. **Zone** = first 3 chars of PropertyCode; how to handle outliers `598285` and `STR-597-4899...` (×3) —
   exclude, or map to a zone?
4. Confirm the app never writes land data (read-only view) and owns only owner/registration tables.

## Design prototype
Approved. Artifact: Business Partner Portal — Leasing & Landlord (navy/taupe DIP language).
Landlord flow: Verify → **select property** → Update owner info wizard (owner details → signatories →
documents → review → submit).

---

## Build progress

### Increment 1 — data foundation (done)
`Models/Leasing/LeasingModels.cs`, `Data/Leasing/LeasingDbContext.cs` (separate bounded context, `leasing`
schema, reads `dbo.Property` view via `ToView`), design-time factory, `LeasingInitial` migration (7
`leasing.*` tables; view excluded). `LeasingManager` added to `UserType`. Ownership keyed on the view's
`Id` column (resolved open question 2 — the real view now exposes `Id`).

### Increment 2 — Admin Land Register (done)
- `Services/Leasing/LeasingOptions.cs` — `Leasing:Zones` config (fixed LOV `597`, `598`).
- `Services/Leasing/LandRegisterService.cs` — reads the `Premises` view for a zone, overlays owner
  assignments from `leasing.LandlordPremises`. **Zone filter = `PropertyCode.StartsWith("{zone}-")`** so
  look-alikes like `598285-…` and non-zone codes (`STR-…`, resolves open question 3 by excluding them) are
  left out. Never writes land data (resolves open question 4).
- `Components/Leasing/LandRegister.razor` — page `/admin/land-register` (Administrator + LeasingManager):
  Zone tabs, summary cards (property codes / buildings / assigned / unassigned), debounced search over
  code + building, accordion of property code → buildings with owner chips coloured by landlord status.
- Nav entry "Land Register" gated by `ClaimsPrincipal.IsLeasingUser()`.
- **Local test fixture:** `scripts/local/seed-property-view.sql` stands up `dbo.Property` from `bld.xlsx`
  (1000 rows, synthesized `Id`) — DEV ONLY; the real view lives on the DIP server. Verified counts:
  Zone 597 = 336 codes / 435 buildings; Zone 598 = 328 codes / 559 buildings. Playwright-tested end to end.

### Next — Increment 3: Admin Landlords
Create owner shell scoped to a BU (business-set Name / Primary Contact / Email / Mobile) → assign premises
from the Land Register → invite (tokenized link + OTP). Then Increment 4: landlord self-service wizard +
status workflow + registration trace wired into the notification bell / activity feed.

### Increment 4 — Landlord self-service + review (done)
- `LandlordAccessTokenService` — link-token validation + 4-digit OTP (SHA-256 link hash, salted OTP hash,
  10-min validity, 5-attempt / 5-send caps), session issue/validate. **Verification is pluggable (OTP now,
  UAE Pass later).** In Development (no Database Mail profile) the OTP is surfaced on-screen so the flow is
  testable locally; Production sends via `sp_send_dbmail`.
- `LandlordDocumentService` — landlord documents stored under `doc/landlord/{id}` (content root, not
  wwwroot), metadata in `leasing.LandlordDocuments`; single-slot identity docs replace, "Other" accumulate.
- `LandlordService.SubmitAsync` — applies landlord-updated fields + signatories, records a
  `LandlordFieldChange` per changed value, moves Draft/Awaiting → **Under Review**, stamps reference
  `DIP-LR-{yyyy}-{100000+id}`, writes the Submitted trace event. Business-set core contact never overwritten.
- Pages (public layout, `prerender:false`): `/landlord/{token}` (LandlordVerify — OTP) →
  `/landlord/{token}/update?s=…` (LandlordWizard — Property → Owner Details → Signatories → Documents →
  Review → Submit). Owner-type-aware identity (Emirates ID vs Trade Licence); primary contact auto-seeded
  as locked first signatory.
- Admin review: LandlordDetail now shows Submitted Owner Information (identity, signatories, documents with
  download links via `/landlord-documents/{id}`, changed-fields table) and Approve/Reject with note.
- **Workflow proven end to end (Playwright):** create → assign → invite → OTP verify → wizard submit
  (ref DIP-LR-2026-100002) → admin sees Under Review → Approve. Every transition writes a trace event.

### Deferred to a follow-up
- Global top-bar notification bell currently covers suppliers only; landlord submissions surface via the
  Landlords list "Under Review" filter + the per-landlord registration trace. Folding landlords into the
  shared bell is a small follow-up (the bell/ActivityService are supplier-typed today).
- Landlord self-service is served from the intranet Web app for v1; moving it behind the DMZ portal + REST
  API (as done for suppliers) is the eventual hardening step. UAE Pass verification replaces OTP later.

### Update — Property view extra columns + Landlords row actions
- The `dbo.Property` view gained `LeaseNo, LandLordCode, CompanyName, ContactName, EmailAddress` (source-
  system lease/owner data, read-only). Added to the `Premises` entity, the `LandRegisterService` projection
  and search, and shown as columns in the Land Register building rows (local seed fixture updated to match).
- Landlords list now has an **Edit | Email | Review** action column. Email issues/re-sends the self-service
  invite (enabled once premises are assigned); Review is enabled only while a landlord is Under Review.
