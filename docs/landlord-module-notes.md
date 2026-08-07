# Landlord / Leasing module — design & data-sourcing notes

Living spec for the Landlord (Business Partner Portal) module. Keep in sync as decisions land.

## Business context
DIP manages leasing & sub-leasing. Tenants come to DIP for **NOC**, **Ejari registration**,
**renewal** and **cancellation**. The Landlord module establishes the **verified owner record** those
processes reference. Business maintains the **land**; the landlord updates the **owner** information.

## Data sourcing — IMPORTANT
- **Land/Property/Building information is READ-ONLY, sourced from a SQL VIEW** — do **not** create,
  seed, migrate or write it.
  - Known source view: `[BP].[dbo].[Property]` with columns **`CategoryName`, `PropertyCode`, `BuildingName`**.
  - `PropertyCode` = `{Zone}-{rest}`, Zone ∈ {**597**, **598**} (zones fixed, from appsettings).
    Examples: `598-115`, `598-1147W/H-8`, `597-970`, `597-184`. The `rest` part is alphanumeric.
  - `CategoryName` = usage/category (Industrial, Warehouses, Labor Accommodation, Mixed residential and
    Commercial, …).
  - `BuildingName` = building/premises name.
  - Map land as a **keyless / view-backed EF entity** (`ToView("Property")`), never a migrated table.
- **App-OWNED tables (EF migrations) — the only things we write:**
  - Owner/Landlord, ownership link (Owner ↔ building, per (PropertyCode, BuildingName)),
    Signatory, LandlordDocument, registration/status, field-change + audit trail.
- Verification method is **pluggable** (email OTP now; UAE Pass later — Ejari uses UAE Pass). "Decide later."

## Model (agreed)
- Hierarchy in the view: **Zone (code prefix) → PropertyCode → BuildingName**. (Units not in this view;
  units/tenancy come with the later Tenant/Ejari phase.)
- **One owner → many properties/buildings**; **each building can have a different owner**; **co-ownership**
  allowed → ownership recorded at **building** granularity via a link.
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
1. Does the view expose a **stable unique key** per row (a Building/Property id), or do we key ownership on
   **(PropertyCode, BuildingName)**?
2. Is `dbo.Property` **one row per building** (PropertyCode + BuildingName)? Any separate views for units /
   tenancies / an owner master to read?
3. Zone derived as the `PropertyCode` prefix before the first `-` — correct?
4. Confirm the app never writes land data (read-only view) and owns only owner/registration tables.

## Design prototype
Approved. Artifact: Business Partner Portal — Leasing & Landlord (navy/taupe DIP language).
Landlord flow: Verify → **select property** → Update owner info wizard (owner details → signatories →
documents → review → submit).
