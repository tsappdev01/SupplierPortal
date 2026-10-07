# Customer Portal — Functional Requirements Specification (for Business Sign-off)

**Application:** Customer Portal — Internal (`SupplierPortal.Web`) and DMZ "Partner Connect" self-service (`SupplierPortal.SelfService`)
**Department served:** Business Relation (customer onboarding — customer code "CST")
**Document status:** For business sign-off
**Prepared on:** 07-Oct-2026

> **Scope of this document.** This specification describes the **complete, as-built functionality** of the Customer Portal from inception, written as numbered functional requirements with acceptance criteria so the business can review and sign off each capability. It is derived from the delivered application. Where behaviour is conditional or role-restricted, that is called out explicitly.

---

## 1. Document control

| Item | Detail |
|------|--------|
| Version | 1.0 (sign-off draft) |
| Author | Delivery team |
| Reviewers | Business Relation (business owner), IT/Delivery, QA |
| Related document | *Supplier Portal (Internal) — Change Request Register* |

### Revision history
| Ver | Date | Description |
|-----|------|-------------|
| 1.0 | 07-Oct-2026 | Initial consolidated functional specification for sign-off |

---

## 2. Purpose and scope

### 2.1 Purpose
The Customer Portal digitises customer onboarding for the Business Relation department. It lets internal staff register and manage customer master data, and lets customers complete and sign off their own data securely online via the external "Partner Connect" application, with the data mapped for export to Oracle Fusion.

### 2.2 In scope
- Internal management of customer records (registration, editing, assignment, prioritisation, notification, documents, reporting).
- An administrator-configurable data model (fields, tabs, validation, conditional visibility, required rules, documents).
- Customer self-service data capture via a secure emailed link with one-time-password (OTP) verification and an electronic declaration/signature.
- Bulk Excel export and import aligned to the Oracle Fusion data exchange (129-column specification plus ERP identifiers).
- Email notifications with per–Business-Unit branding and sender addresses.

### 2.3 Out of scope (for this document)
- The Supplier and Landlord portals (covered separately), the underlying infrastructure/DevOps pipeline, and the Oracle Fusion-side import itself.

### 2.4 Architecture context (informational)
The solution is two applications over one shared customer database:
- **Internal portal (`SupplierPortal.Web`)** — staff-facing, cookie-authenticated.
- **DMZ "Partner Connect" (`SupplierPortal.SelfService`)** — customer-facing, holds no database of its own; it reaches customer data only through a secured API using link + session credentials.
Both render the customer form from the **same administrator-defined field configuration**, so the internal and self-service forms behave identically.

---

## 3. User roles and access control

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-3.1** | Access to the Customer Portal is restricted to authenticated staff who hold the Customer portal permission. | A user without the Customer portal claim cannot open any Customer Portal page. |
| **FR-CUST-3.2** | **Administrator** has full access, including Settings (Field Settings, Manage Tabs) and the data upload. | Only an Administrator sees the "Field Settings" tile, the Field Manager page, the Register and Upload buttons, and the upload page. |
| **FR-CUST-3.3** | **Application Administrator** has full operational access (list, edit, assign, notify, import/export, reports) **but no access to Settings**. | An Application Administrator can register, edit, notify and upload customers but cannot open Field Settings / Manage Tabs; the Settings menu is hidden and direct links are denied. |
| **FR-CUST-3.4** | **Normal users** are scoped to the Business Unit(s) assigned to them. | A user assigned to specific BUs sees only customers in those BUs across the list, filters, export and templates. Only an Administrator with no BU assignment sees all customers. |
| **FR-CUST-3.5** | Individual form tabs may be restricted to named editors. | When a tab is marked "Editable by" specific users, other users see it read-only and the server rejects any attempt to change its fields. |

---

## 4. Customer registration and list management (internal)

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-4.1** | A landing hub offers "Customer Registrations" and (for admins) "Field Settings". | Both tiles navigate correctly; "Field Settings" is hidden from non-administrators. |
| **FR-CUST-4.2** | The registrations screen shows KPI tiles: Total, Not Submitted, Awaiting Customer Response, For Review, Completed. | Counts reflect the active Business Unit filter. |
| **FR-CUST-4.3** | The list can be filtered by Business Unit, Customer Name (live search), Status, Priority (P1–P4) and Assigned-To (incl. "Unassigned"). | Each filter narrows the grid immediately. |
| **FR-CUST-4.4** | Records are grouped by recency: Recently Updated (≤1 day), Updated Last Week (1–7 days), Everything Else (>7 days). | Records appear under the correct tab by last-update age. |
| **FR-CUST-4.5** | Grid columns are user-selectable and the choice is remembered. | The column chooser persists between sessions for that user/browser. Default columns: Customer Number, Priority, Customer Name, Full Name, Contact Email, Status, Assigned To, Actions. Paged 25 per page. |
| **FR-CUST-4.6** | Priority and Assigned-To can be changed inline in the grid. | Changing either saves immediately without opening the record. |
| **FR-CUST-4.7** | Each row offers View Documents, Edit, and Send self-service link. | The actions open the documents modal, the edit form, and send a link respectively. |
| **FR-CUST-4.8** | The documents modal previews images/PDFs and allows download of a single file, "all images as one PDF", or "all as a zip", with progress indication. | Each download produces the correct file; progress is shown while streaming. |
| **FR-CUST-4.9** | Staff can notify selected customers, or all customers in the current view, in bulk. | A confirmation dialog lists the recipient addresses and the number skipped for a missing email before anything is sent. |
| **FR-CUST-4.10** | Administrators can register a new customer and upload customers from Excel. | "Register" creates a draft and opens the edit form; "Upload" opens the import page (both admin-only). |
| **FR-CUST-4.11** | Staff can generate a blank or data-populated Excel template, scoped to their Business Unit(s). | The generated workbook matches the agreed template layout (see §8). |
| **FR-CUST-4.12** | Staff can print a filtered report with selectable columns across all records. | The printed report contains every matching record, laid out for the chosen columns. |

---

## 5. Customer record and data model

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-5.1** | A customer record holds an **Account (company)** section and a **Contact (primary person)** section. | Both sections are captured and persisted against the customer. |
| **FR-CUST-5.2** | Workflow attributes are maintained per record: Customer Unique Number, Baan Ref, Status, Priority, Business Unit, Assigned-To, created/updated audit stamps. | These are set/maintained by the system and staff actions, not by the customer. |
| **FR-CUST-5.3** | Account data includes legal/commercial names (incl. Arabic), organisation type, **VAT structure**, TRN/TIN, trade-license details, incorporation/establishment, PEP flag, bank (IBAN), up to two owners (nationality/passport/share %), payment terms and full address. | All listed fields can be captured, stored and displayed. |
| **FR-CUST-5.4** | Contact data includes name (incl. Arabic), contact email and mobile, nationality, residential status, passport (and dual passport), visa, government/Emirates ID and demographic details. | All listed fields can be captured, stored and displayed. |
| **FR-CUST-5.5** | When VAT structure = "VAT Group", a **child grid of VAT-group entities** is captured (legal name, address, e-invoicing wave, TRN, TIN, business-location type). | The grid is shown only for VAT Group and each entity is stored with its order. |
| **FR-CUST-5.6** | **Oracle Fusion ERP identifiers** — Party ID, Party Number, ERP Bill-To Account — are held on the record. | These values are retained and included in the export (see §8). |
| **FR-CUST-5.7** | Each record carries an electronic **declaration/acknowledgement** with a typed signature and a signature image. | On customer submission these are stored and rendered into a signed PDF document. |

---

## 6. Configurable fields (Field Manager) and tabs — *Administrator only*

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-6.1** | Administrators can configure each field's **display label**, help text and (for text fields) min/max length. | Changes are reflected on the customer forms and the export template. |
| **FR-CUST-6.2** | Each field has **two independent "required" switches**: Required (Internal) for staff and Required (Customer) for self-service. | A field can be mandatory for the customer but optional for staff, or vice-versa; validation honours the right switch per context. |
| **FR-CUST-6.3** | Each field has a **Visible** switch and independent **edit control** for staff vs customers (Editable / Read-only / Lookup). | Hidden fields do not render; read-only fields render disabled for the applicable audience. |
| **FR-CUST-6.4** | Fields can be **removed (soft) and restored**. | A removed field disappears from the forms but is retained for the Oracle template; it can be restored. |
| **FR-CUST-6.5** | Fields support **conditional visibility ("Depends On")** on another Yes/No or list field, with an **equals / does-not-equal** operator. | The dependent field shows only when the rule is satisfied; when hidden it is cleared on save; the rule is enforced identically on the staff form, the self-service form, and server-side on submit. |
| **FR-CUST-6.6** | Yes/No fields render as a dropdown (— Select — / Yes / No). | All Yes/No fields present a three-option dropdown. |
| **FR-CUST-6.7** | "Trade License Issuing Body" is shown only when "Licensing Authority" = "Others". | The field appears only for Licensing Authority = Others (implemented as a Depends-On rule). |
| **FR-CUST-6.8** | Administrators can manage **tabs**: rename, reorder, show/hide, and restrict editing to named users. | Tab changes take effect on the customer form immediately; tabs sharing a title merge into one step. |

---

## 7. Document management

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-7.1** | Administrators define document types with a name, a requirement hint, a required flag and an order. | Document types appear as a checklist on the record and self-service form. |
| **FR-CUST-7.2** | A document type can be **conditional** on a customer field value ("Depends On"). | The document appears only when its controlling field matches; it is mandatory only when required *and* applicable. |
| **FR-CUST-7.3** | Documents can be uploaded, viewed, downloaded and removed (PDF/JPG/PNG), with progress indication. | Each operation works from the internal form and the self-service form; required types are badged. |
| **FR-CUST-7.4** | Completion/submission is blocked while any applicable required document is missing. | The user cannot complete (staff) or submit (customer) with a required document outstanding. |

---

## 8. Excel export and import template (Oracle Fusion exchange)

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-8.1** | The upload/download template follows an agreed **129-column specification** (63 Account + 62 Contact columns) aligned to the data-collection sheet. | Header labels and order match the agreed sheet. |
| **FR-CUST-8.2** | The **ERP identifier columns** (Party ID, Party Number, ERP Bill-To Account) are appended after Payment Terms for Oracle Fusion. | These three columns are present in both export and the import template. |
| **FR-CUST-8.3** | The template emits **all non-removed fields** regardless of form visibility (for the Oracle exchange). | Only removed fields are omitted from the template. |
| **FR-CUST-8.4** | The template handles the **VAT Group entities** as a single combined column that splits back into rows on import. | Multiple entities round-trip; a blank cell on re-upload does not erase existing VAT-group rows. |
| **FR-CUST-8.5** | Download and upload use the **same positional contract**, so a downloaded, populated template re-imports cleanly. | Automated round-trip test confirms every column and value survives download→upload with correct type conversion. |
| **FR-CUST-8.6** | Import matches existing customers by **Customer Unique Number** (upsert); new rows are created as "Not Submitted". | Existing records update in place; new records are created; Legal Name is required; rejected rows are reported. |

---

## 9. Customer self-service (Partner Connect)

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-9.1** | The self-service link is emailed to **both** the account Primary Email and the contact Email, each with its own secure token. | Each distinct address receives its own link; the record moves to "Awaiting Customer Response". Links are valid for 14 days. |
| **FR-CUST-9.2** | Opening a link requires **OTP verification**: a **6-digit** code is emailed and must be entered to proceed. | The code is 6 digits, valid for 10 minutes, with limited attempts and resends; entering it grants a short-lived secure session. |
| **FR-CUST-9.3** | The OTP is delivered to **whichever inbox opened the link** — primary link → primary email; contact link → contact email — decided authoritatively by the server. | Routing is correct even when the external app does not forward the recipient tag (the server uses the token's stored recipient). |
| **FR-CUST-9.4** | The verification page shows the **masked recipient address** the code was sent to. | The page displays e.g. `n***@g***l.com`, matching the inbox that opened the link. |
| **FR-CUST-9.5** | The self-service form is the same configurable, tabbed form as the internal form, with added **Documents** and **Review & Submit** steps. | Fields, order, visibility and conditional rules match the administrator configuration. |
| **FR-CUST-9.6** | **Mandatory fields are validated tab-by-tab on "Save & Next"** (not only at the end). | The customer cannot advance past a tab with missing/invalid required fields. |
| **FR-CUST-9.7** | Jumping ahead (including to Review) checks **all earlier tabs**, lands the user on the first incomplete tab, highlights the offending fields, and **moves the cursor to the first field needing input**. | The first invalid field is focused and flagged; a message summarises what is missing. |
| **FR-CUST-9.8** | Submission requires an **electronic declaration**: a typed full-name signature **and** a hand-drawn signature. | Submit is blocked until both are provided; the declaration text is the UAE compliance wording. |
| **FR-CUST-9.9** | On submission, the system stores a **signed declaration PDF**, sets status to **"For Review"**, consumes the link, **expires all sibling links**, and notifies the initiator. | A completed record cannot be reopened via an old link; the initiator receives a submission notice. |
| **FR-CUST-9.10** | All field-level validation is **re-enforced server-side on submit** (required, lengths, date logic for licences/passports/IDs, VAT-group completeness, required documents). | A crafted or incomplete payload is rejected by the server regardless of the browser. |

---

## 10. Email and notifications

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-10.1** | The system sends three customer emails: the update-request link, the OTP code, and a completion thank-you; plus an internal submission notice to the initiator. | Each email is sent at the correct point in the workflow using branded templates. |
| **FR-CUST-10.2** | Every staff-initiated send (single, bulk, notify-all, return-to-customer) is **confirmed first**, listing recipients. | No email is sent without the confirmation dialog. |
| **FR-CUST-10.3** | Emails are sent from a **Customer-portal sender address per Business Unit**, falling back to the BU's general address, then the global default. | Where configured, customer emails show the Customer-portal sender (e.g. `businessrelation@...`); otherwise the fallback applies with no change to existing behaviour. |
| **FR-CUST-10.4** | Email branding (name and logo) is resolved per Business Unit. | Emails carry the correct BU branding. |

---

## 11. Lookups and reference data

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-11.1** | Country fields present a combined "Code – Name" list (e.g. "AE – United Arab Emirates"), storing the code. | The dropdown shows the full list; the code is stored with the name alongside. |
| **FR-CUST-11.2** | Passport Issue Country Code uses the full coded "Code – Name" list. | The field offers the complete coded country list. |
| **FR-CUST-11.3** | Country Subdivision uses coded emirate values (AUH/DXB/SHJ/UAQ/FUJ/AJM/RAK) shown by name. | The list shows emirate names and stores the code. |
| **FR-CUST-11.4** | City/Emirate becomes a 7-emirate dropdown when the country is UAE, otherwise free text. | Behaviour switches based on the selected country. |
| **FR-CUST-11.5** | All other list-of-value fields (account type, VAT structure, organisation type, e-invoicing wave, licensing authority, payment terms, contact demographics, etc.) are provided as controlled lists. | Each field offers its defined options. |

---

## 12. Workflow / status lifecycle

The customer record moves through:

**Not Submitted** → **Awaiting Customer Response** → **For Review** → **Completed**

| FR ID | Requirement | Acceptance criteria |
|-------|-------------|---------------------|
| **FR-CUST-12.1** | A new/registered or imported record starts as "Not Submitted". | Newly created records show this status. |
| **FR-CUST-12.2** | Sending a self-service link moves the record to "Awaiting Customer Response". | Status updates on send. |
| **FR-CUST-12.3** | A customer submission moves the record to "For Review". | Status updates on submit. |
| **FR-CUST-12.4** | Staff "Mark completed" (after full validation incl. required documents) moves it to "Completed", expires self-service links, and sends a thank-you. | Status updates; old links stop working; email is sent. |
| **FR-CUST-12.5** | A completed record can be **returned to the customer**, which re-issues a link and sets it back to "Awaiting Customer Response". | A fresh link is sent and the status reverts. |

---

## 13. Non-functional / security (summary)

- **Authentication & authorisation:** staff via cookie auth and portal/role claims; the external app via a secured client-credentials API plus per-request link/session headers.
- **Mass-assignment protection:** on self-service save, only visible, customer-editable fields are written; workflow and identifier fields can never be set from a customer payload.
- **OTP hardening:** codes are hashed, time-limited, attempt- and resend-capped; the "sent" state is recorded only after successful delivery so failures retry and surface.
- **Auditing:** record changes and key actions are written to the audit log with the acting user.
- **Data exchange:** the import/export template is positionally stable and round-trip tested for the Oracle Fusion exchange.

---

## 14. Assumptions and notes

- The OTP code is **6 digits** (any older reference to a 4-digit code is superseded by this specification).
- The import/export template deliberately includes **all non-removed fields** regardless of on-screen visibility, because it serves the Oracle Fusion data exchange rather than the interactive form.
- "All data within assigned Business Units" means an Application Administrator sees every record in the BUs assigned to them; an Administrator with **no** BU assignment sees all records.

---

## 15. Business sign-off

By signing below, the business confirms the functional requirements in this document accurately describe the required and delivered behaviour of the Customer Portal.

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Business Owner (Business Relation) | | | |
| Process Owner | | | |
| IT / Delivery | | | |
| QA / Test | | | |

---

*This specification was consolidated from the delivered Customer Portal application and its source-control history. It describes functionality as built; items marked in the related Change Request Register as "in progress" are not part of this baseline.*
