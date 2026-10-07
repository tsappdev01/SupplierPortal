# Customer Portal — Functional Requirements (for Business Sign-off)

**Application:** Customer Portal — Internal (`SupplierPortal.Web`) + DMZ "Partner Connect" self-service (`SupplierPortal.SelfService`)
**Department:** Business Relation (customer onboarding) · **Date:** 07-Oct-2026 · **Version:** 1.0 (sign-off)

**Purpose.** Digitise customer onboarding: staff manage customer master data; customers complete and e-sign their own data online via a secure emailed link; data is exported for Oracle Fusion. Both the internal and self-service forms render from the **same administrator-defined configuration**, so they behave identically.

---

## Functional requirements

**1. Roles & access**
- **FR-1.1** Access requires authentication and the Customer portal permission.
- **FR-1.2** **Administrator** — full access including Settings (Field Settings, Manage Tabs) and Excel upload.
- **FR-1.3** **Application Administrator** — all operational actions (list, edit, assign, notify, import/export, reports) but **no Settings**.
- **FR-1.4** **Normal users** are scoped to their assigned Business Unit(s); only an Administrator with no BU assignment sees all customers.
- **FR-1.5** Individual form tabs can be restricted to named editors (others see them read-only; the server rejects unauthorised changes).

**2. Customer registration & list (internal)**
- **FR-2.1** KPI tiles: Total, Not Submitted, Awaiting Customer Response, For Review, Completed (respect the BU filter).
- **FR-2.2** Filter by Business Unit, Customer Name (live search), Status, Priority (P1–P4), Assigned-To; group by recency (today / last week / older).
- **FR-2.3** User-selectable grid columns (remembered per user); inline change of Priority and Assigned-To; 25 per page.
- **FR-2.4** Per-row: view documents, edit, and send self-service link.
- **FR-2.5** Bulk notify selected customers or all in the current view, each with a **confirm-before-send** dialog listing recipients.
- **FR-2.6** Admins can register a customer and upload customers from Excel; any user can generate a blank or data-populated template (BU-scoped) and print a filtered report.

**3. Customer record & data model**
- **FR-3.1** Each record has an **Account (company)** section and a **Contact (primary person)** section, plus workflow attributes (Customer Unique Number, Baan Ref, Status, Priority, Business Unit, Assigned-To, audit stamps).
- **FR-3.2** Account captures legal/commercial names (incl. Arabic), organisation type, VAT structure, TRN/TIN, trade-license details, incorporation, PEP, bank (IBAN), up to two owners, payment terms and address.
- **FR-3.3** Contact captures name (incl. Arabic), email, mobile, nationality, residential status, passport (and dual passport), visa, and government/Emirates ID.
- **FR-3.4** When VAT structure = "VAT Group", a **child grid of VAT-group entities** is captured (legal name, address, e-invoicing wave, TRN, TIN, location type).
- **FR-3.5** Oracle Fusion **ERP identifiers** (Party ID, Party Number, ERP Bill-To Account) are held on the record and included in export.
- **FR-3.6** Each record carries an electronic **declaration** with a typed signature and signature image, rendered to a signed PDF on submission.

**4. Configurable fields & tabs (Administrator)**
- **FR-4.1** Per field: edit display label, help text, and (text fields) min/max length.
- **FR-4.2** **Two independent "required" switches** — Required (Internal) for staff and Required (Customer) for self-service.
- **FR-4.3** Per field: Visible switch and independent edit control for staff vs customers (Editable / Read-only / Lookup).
- **FR-4.4** Fields can be soft-removed and restored (removed fields stay in the Oracle template).
- **FR-4.5** **Conditional visibility ("Depends On")** on another Yes/No or list field, with **equals / does-not-equal**; hidden fields are cleared on save and enforced server-side. (e.g. "Trade License Issuing Body" shows only when Licensing Authority = "Others".)
- **FR-4.6** Yes/No fields render as a dropdown.
- **FR-4.7** Tabs can be renamed, reordered, shown/hidden, and restricted to named editors; changes apply to the form immediately.

**5. Documents**
- **FR-5.1** Admins define document types (name, hint, required flag, order), optionally **conditional** on a customer field value.
- **FR-5.2** Upload / view / download / remove (PDF, JPG, PNG) on both the internal and self-service forms; required types are badged.
- **FR-5.3** Completion (staff) and submission (customer) are blocked while any applicable required document is missing.

**6. Excel export / import (Oracle Fusion)**
- **FR-6.1** The template follows the agreed **129-column specification** (63 Account + 62 Contact) plus the three appended ERP identifier columns, with labels aligned to the data-collection sheet.
- **FR-6.2** The template emits **all non-removed fields** regardless of form visibility; VAT-group entities round-trip via a single combined column (a blank cell never erases existing rows).
- **FR-6.3** Download and import share the same positional contract (a populated download re-imports cleanly); import upserts by Customer Unique Number, creates new rows as "Not Submitted", and reports rejected rows.

**7. Self-service (Partner Connect)**
- **FR-7.1** The link is emailed to **both** the account Primary Email and the contact Email, each with its own 14-day token; the record moves to "Awaiting Customer Response".
- **FR-7.2** Opening a link requires **OTP** — a **6-digit** code (10-minute validity, attempt/resend capped) grants a short-lived secure session.
- **FR-7.3** The OTP is delivered to **whichever inbox opened the link** (decided authoritatively by the server); the verify page shows the **masked recipient** address.
- **FR-7.4** The self-service form is the same configurable, tabbed form with added Documents and Review & Submit steps; customer-required fields are validated **tab-by-tab on Save & Next**.
- **FR-7.5** Jumping ahead checks all earlier tabs, lands on the first incomplete tab, highlights the fields, and **moves the cursor to the first field needing input**.
- **FR-7.6** Submission requires an **electronic declaration** (typed full-name signature and a hand-drawn signature); all validation is **re-enforced server-side** on submit.
- **FR-7.7** On submit the system stores a **signed declaration PDF**, sets status to "For Review", expires all sibling links, and notifies the initiator.

**8. Email, lookups & workflow**
- **FR-8.1** Three customer emails (update-request link, OTP, completion thank-you) plus an internal submission notice; emails use a **Customer-portal sender address per Business Unit** (falling back to the BU email, then the global default) with per-BU branding.
- **FR-8.2** Controlled lists of values throughout — combined "Code – Name" country lists, coded emirate subdivisions, a 7-emirate City dropdown when the country is UAE, and defined options for all other LOV fields.
- **FR-8.3** **Status lifecycle:** Not Submitted → Awaiting Customer Response → For Review → Completed. Sending a link, customer submission, and staff "Mark completed" move the record accordingly; a completed record can be **returned to the customer** (re-issues a link). Completion expires all self-service links so a record cannot be reopened via an old link.

**Security (summary).** Staff use cookie auth + portal/role claims; the external app uses a secured API with per-request link/session credentials. Self-service saves accept only visible, customer-editable fields (identifier/workflow fields are never writable from a customer payload). OTP codes are hashed and recorded only after successful delivery. Key actions are audit-logged.

---

## Business sign-off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Business Owner (Business Relation) | | | |
| Process Owner | | | |
| IT / Delivery | | | |
| QA / Test | | | |
