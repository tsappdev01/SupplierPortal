# Supplier Portal (Internal) — Change Request Register

**Application:** Supplier Portal — Internal (`SupplierPortal.Web`) and DMZ "Partner Connect" self-service (`SupplierPortal.SelfService`)
**Reporting window:** 14-Sep-2026 → 07-Oct-2026
**Prepared for:** Business sign-off
**Prepared on:** 07-Oct-2026

> **Note on the window:** All change activity captured in the source-control history for the internal Supplier Portal within this window falls between 29-Sep-2026 and 07-Oct-2026. No Supplier Portal changes are recorded between 14-Sep and 29-Sep. Each change below is written as a self-contained Change Request (CR) so it can be reviewed and signed off individually.

---

## Summary of changes

| CR ID | Title | Date | Category | Status |
|-------|-------|------|----------|--------|
| CR-SUP-001 | Supplier Status (Active / Inactive) with grid toggle, filter and reporting | 06-Oct-2026 | Enhancement | Delivered |
| CR-SUP-002 | Application Administrator role (admin without Settings) | 07-Oct-2026 | Enhancement / Security | Delivered |
| CR-SUP-003 | Portal-specific sender email addresses (per Business Unit) | 30-Sep-2026 | Enhancement | Delivered |
| CR-SUP-004 | One-time-password (OTP) delivery reliability fix | 30-Sep-2026 | Defect fix | Delivered |
| CR-SUP-005 | Show masked recipient email on the in-app verification page | 30-Sep-2026 | Enhancement | Delivered |
| CR-SUP-006 | Partner Connect (DMZ) — clearer error on OTP request failure | 30-Sep-2026 | Defect fix / Supportability | Delivered |
| CR-SUP-007 | Make "Supplier Status" an admin-controllable field in Supplier Field Settings | 07-Oct-2026 | Enhancement | In progress |
| CR-SUP-008 | Load of supplier reference documents (trade licences, TRN certificates) | 30-Sep-2026 | Data load | Delivered |

---

## CR-SUP-001 — Supplier Status (Active / Inactive) with grid toggle, filter and reporting

| | |
|---|---|
| **CR ID** | CR-SUP-001 |
| **Date delivered** | 06-Oct-2026 |
| **Category** | Enhancement |
| **Modules affected** | Manage Supplier grid, Supplier service layer, Report Builder (supplier reports) |
| **Status** | Delivered |

**Business driver**
The business needs a *Supplier Status* of **Active / Inactive** that is separate from the existing workflow status, so that suppliers who should no longer be used can be disabled and hidden from day-to-day lists while remaining on file for history and reporting.

**Description of change**

- **Grid column + toggle.** The *Manage Supplier* grid now shows a **Supplier Status** column with an **Active / Inactive toggle** (green = Active). Switching the toggle immediately enables or disables the supplier. Every change is written to the **audit log**.
- **Default view hides Inactive.** A **Supplier Status filter** (*Active / Inactive / All*) was added to the filter bar. It **defaults to Active**, so inactive suppliers are hidden from the default list. Turning a supplier Inactive removes it from the Active view on save.
- **Column chooser & export.** "Supplier Status" is available in the grid column chooser (shown by default) and is included in the Print / Export column set.
- **Report Builder.** The supplier reporting view now exposes **Supplier Status** with business-friendly values **'Active' / 'Inactive'**, so it can be selected as a column and used as a filter in any supplier report.

**As-Is → To-Be**
- *As-Is:* The Active/Inactive flag existed in the database but was never shown or editable in the application; it could not be filtered or reported on.
- *To-Be:* Staff can view, toggle, filter and report on Supplier Status from the Manage Supplier screen and the Report Builder.

**Data / database impact**
- The supplier reporting view (`vSupplierReport`) was updated so the status column is presented as **'Active' / 'Inactive'**. Applied automatically on deployment; fully reversible. No new supplier data columns were required (the underlying flag already existed).

**Security / roles**
- Available to the same staff roles that manage suppliers. Toggle actions are audit-logged with the acting user.

---

## CR-SUP-002 — Application Administrator role (administrator without Settings)

| | |
|---|---|
| **CR ID** | CR-SUP-002 |
| **Date delivered** | 07-Oct-2026 |
| **Category** | Enhancement / Security |
| **Modules affected** | User roles & security, navigation menu, Home page, user create/edit, operational pages across Supplier / Customer / Landlord portals |
| **Status** | Delivered |

**Business driver**
The business wants a role that can perform all day-to-day operational work but must **not** be able to change system configuration. This lets operational leads run the portal without granting them full administrative control over Settings.

**Description of change**
A new **Application Administrator** role was introduced — a "miniature administrator".

- **What an Application Administrator CAN do (same as Administrator):** see all records within the **business units assigned to them**, view all users' records, import/export Excel, register suppliers/customers/landlords, send notifications, assign work, and use the dashboards and reports.
- **What an Application Administrator CANNOT do:** open the **Settings** area — Business Units, Manage Users, Supplier/Customer/Landlord Field Settings, Document Types, Notification Groups, Scheduled Reports, Break-Glass, and Audit Log. The Settings menu is hidden for them, and direct links to Settings pages are denied.
- **User administration.** The role dropdown on *Create User* and *Edit User* now offers **"Application Administrator"** with explanatory help text, and the Users list shows a friendly role label.

**As-Is → To-Be**
- *As-Is:* Only two operational role levels existed — full Administrator (everything, including Settings) or Normal User (scoped).
- *To-Be:* A middle tier exists — full operational access within assigned business units, but no access to Settings.

**Security / roles**
- Enforced in two layers: the Settings navigation is hidden, **and** each Settings page continues to require full Administrator rights, so access is blocked even via a direct URL.

**Scope note**
- The role is cross-portal; it applies identically in the Supplier, Customer and Landlord portals.

---

## CR-SUP-003 — Portal-specific sender email addresses (per Business Unit)

| | |
|---|---|
| **CR ID** | CR-SUP-003 |
| **Date delivered** | 30-Sep-2026 |
| **Category** | Enhancement |
| **Modules affected** | Business Unit settings, email dispatch for Supplier (and Customer / Landlord / Broker) |
| **Status** | Delivered |

**Business driver**
System-generated emails should be clearly identified by business function, so recipients recognise who the message is from (e.g. for DIP: `purchase@dipark.com` for Supplier communications).

**Description of change**

- Each **Business Unit** can now define a dedicated **sender ("From") email per portal** — Supplier, Customer, Landlord and Broker — configured in *Add / Edit Business Unit* under a new "Portal sender email addresses" section.
- When a portal sends an email, it uses that portal's address if set; otherwise it falls back to the Business Unit's general email; otherwise to the global default sender. **Existing behaviour is unchanged until an address is entered.**
- The sender shown to recipients on self-service link/contact information uses the same per-portal address.
- **Recipients of internal notifications are unchanged** — only the "From" sender address became portal-specific.

**Data / database impact**
- Four optional sender-address columns were added to the Business Unit record. Applied automatically on deployment.

---

## CR-SUP-004 — One-time-password (OTP) delivery reliability fix

| | |
|---|---|
| **CR ID** | CR-SUP-004 |
| **Date delivered** | 30-Sep-2026 |
| **Category** | Defect fix |
| **Modules affected** | Supplier self-service OTP (and Landlord, by the same fix) |
| **Status** | Delivered |

**Business driver / problem**
A supplier could open the verification link and never receive the OTP code. The system recorded the code as "sent" **before** the email actually left, so a failed send looked successful: the code was never re-tried, the error was hidden from the user, and one send was consumed from the allowed limit.

**Description of change**
The order of operations was corrected: the OTP is **sent first**, and only once delivery succeeds does the system record the code, its expiry and the send count. If sending fails, nothing is recorded, so the verification page automatically retries and the error becomes visible.

**As-Is → To-Be**
- *As-Is:* "Sent" was recorded before delivery; a silent failure left the supplier with no code and no retry.
- *To-Be:* The code is recorded only after successful delivery; failures retry and surface.

---

## CR-SUP-005 — Show masked recipient email on the in-app verification page

| | |
|---|---|
| **CR ID** | CR-SUP-005 |
| **Date delivered** | 30-Sep-2026 |
| **Category** | Enhancement (usability) |
| **Modules affected** | Supplier (and Customer) in-app verification page |
| **Status** | Delivered |

**Business driver**
When staff triggered verification from inside the portal, the page only said the code was sent "to the email address on file", without showing which address. A wrong on-file email was impossible to spot.

**Description of change**
The in-app verification page now shows the **masked recipient address** (for example `n***@g***l.com`) next to the account name, so staff can confirm the code went to the expected inbox. The DMZ Partner Connect verification pages already displayed this and are unchanged.

---

## CR-SUP-006 — Partner Connect (DMZ): clearer error on OTP request failure

| | |
|---|---|
| **CR ID** | CR-SUP-006 |
| **Date delivered** | 30-Sep-2026 |
| **Category** | Defect fix / Supportability |
| **Modules affected** | DMZ Partner Connect supplier self-service client |
| **Status** | Delivered |

**Business driver / problem**
When the Partner Connect (DMZ) supplier self-service could not request an OTP from the internal system, it showed only a generic "service is temporarily unavailable" message, hiding whether the cause was a permission issue, a mis-configuration, or a server error — making support slow.

**Description of change**
The failure message now includes the underlying status, and the detailed response is logged, so the cause can be diagnosed quickly. This brings the supplier self-service client in line with the customer self-service client, which already surfaced this detail.

---

## CR-SUP-007 — Make "Supplier Status" an admin-controllable field in Supplier Field Settings

| | |
|---|---|
| **CR ID** | CR-SUP-007 |
| **Date** | 07-Oct-2026 |
| **Category** | Enhancement |
| **Modules affected** | Supplier Field Settings (Field Manager), Manage Supplier grid, Excel export, Report Builder |
| **Status** | **In progress** |

**Business driver**
Administrators want to control the **Supplier Status** field's behaviour from *Settings → Supplier Fields*, the same way other supplier fields are controlled — in particular, to show or hide the Supplier Status column on the Manage Supplier grid.

**Description of change (planned / in progress)**

- Add **"Supplier Status"** to the Supplier Field catalogue so it appears in *Settings → Supplier Fields*.
- The field's **Visible** toggle (show/hide) controls whether the **Supplier Status column appears on the Manage Supplier grid**.
- **Excel export:** Supplier Status **remains included** in the full Excel export.
- **Import template:** Supplier Status is **excluded** from the upload/import template (it is an internal flag, not supplier-supplied).
- **Report Builder:** Supplier Status **remains available** as a selectable column and filter.

**Status note**
This change is under development and not yet released. (A build-and-release cycle, test run, and sign-off are outstanding.)

---

## CR-SUP-008 — Load of supplier reference documents

| | |
|---|---|
| **CR ID** | CR-SUP-008 |
| **Date** | 30-Sep-2026 |
| **Category** | Data / reference load |
| **Modules affected** | Project document store (`/docs`) |
| **Status** | Delivered |

**Description**
A set of supplier reference documents (trade licences, TRN/VAT registration certificates, bank-transfer details) and a supporting archive were loaded into the project document store for reference and testing. This is a document/data load only; it does not change application behaviour.

---

## Sign-off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Business Owner | | | |
| Product / Process Owner | | | |
| IT / Delivery | | | |
| QA / Test | | | |

---

*This register was generated from the application's source-control history for the stated window. Each CR corresponds to a discrete, reviewable change delivered to the Supplier Portal.*
