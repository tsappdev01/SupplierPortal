const g = require("./docgen.js");
const { para, h1, h2, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Business Requirements Document (Updated)", [
  ["Document", "Business Requirements Document (BRD) — Updated"],
  ["Product", "Supplier Portal"],
  ["Scope", "Supplier Portal only"],
  ["Version", "2.0 (updated)"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Introduction"));
kids.push(h2("1.1 Purpose"));
kids.push(para("This document defines the business requirements for the Supplier Portal — a Dubai Investments platform that digitises supplier onboarding and master-data maintenance. It is the updated baseline reflecting the current, implemented system including the public self-service (DMZ) capability, UAE e-invoicing data capture, and the security hardening completed during acceptance testing. It covers the Supplier Portal only."));
kids.push(h2("1.2 Background"));
kids.push(para("Supplier master data must be collected accurately and compliantly (including UAE e-invoicing / VAT attributes) and reviewed before it enters the ERP. The portal replaces manual spreadsheet collection with a controlled, auditable workflow, allowing internal staff to request data and suppliers to complete it themselves through a secure self-service link."));
kids.push(h2("1.3 Scope"));
kids.push(bullet([g.t("In scope: ", { bold: true }), g.t("supplier onboarding and maintenance; public self-service data collection with OTP; document capture with OCR assistance; a signed declaration; review/approval workflow; role-based administration; Excel import/export; e-mail notifications; audit logging.")]));
kids.push(bullet([g.t("Out of scope: ", { bold: true }), g.t("direct ERP posting/integration (data is exported for the ERP team); accounts payable / invoicing; procurement transactions.")]));

kids.push(h1("2. Stakeholders & Users"));
kids.push(table(["Role", "Description"],
 [["Administrator","Full access: manage users, business units, supplier field settings, lookups, and all supplier data; run break-glass."],
  ["Normal User (staff)","Business-unit-scoped access to the suppliers assigned to them or their department; request and review submissions."],
  ["Supplier (external)","Completes their own data via a secure self-service link (no account); verifies by OTP; uploads documents; signs the declaration."]],
 [2200, 7300]));

kids.push(h1("3. Business Objectives"));
kids.push(num("Collect complete, accurate supplier master data, including UAE e-invoicing and VAT attributes, first time."));
kids.push(num("Let external parties self-serve securely, reducing manual re-keying and e-mail exchanges."));
kids.push(num("Enforce review and approval before data is accepted, with a full audit trail."));
kids.push(num("Scope data visibility by business unit and ownership; protect sensitive/locked fields."));
kids.push(num("Provide a compliant, signed declaration and a durable record of what each supplier attested to."));
kids.push(num("Operate safely on the public internet (DMZ) with strong authentication and no direct database exposure."));

kids.push(h1("4. Functional Requirements"));
kids.push(h2("4.1 Supplier onboarding & maintenance"));
kids.push(bullet("Register a supplier (admin) capturing organisation, tax/VAT, trade licence, address, contacts, site/business-unit assignment, and payment/banking details across a multi-step form."));
kids.push(bullet("Configurable fields: each field's label, wizard step, order, mandatory flag and supplier-visibility are administrable; tabs can be shown/hidden."));
kids.push(bullet("Mandatory-field validation and e-mail format validation; supplier number generated per business unit."));
kids.push(bullet("Assign a supplier to a user and/or department; bulk-assign department; dashboard filters by business unit, status, priority, assignee and recency."));
kids.push(bullet("Trade Licence and VAT certificate upload with OCR-assisted extraction of TRN / licence number / DUNS and partners."));
kids.push(bullet("Workflow statuses: Not Submitted → Awaiting Supplier Response → For Review → Completed (or Rejected)."));
kids.push(h2("4.2 Self-service (public)"));
kids.push(bullet("Staff send a branded self-service link; the supplier verifies with a one-time passcode (OTP) sent to the e-mail on file."));
kids.push(bullet("The supplier completes only the fields made visible to them; locked/hidden fields are protected server-side."));
kids.push(bullet("Document upload (trade licence, VAT certificate, bank confirmation letter, other)."));
kids.push(bullet("A declaration must be acknowledged and signed (typed name plus a hand-drawn signature) before submission; the system stores a signed declaration PDF with the supplier's documents."));
kids.push(bullet("Merged Address & Contact tab in the supplier wizard; VAT-registered suppliers must provide a 15-digit TRN and upload a VAT Certificate; a structured VAT Group entity table captures each group entity with TRN (15-digit) and TIN (10-digit) length validation."));
kids.push(bullet("Where the VAT-certificate address differs from the trade licence, an alternate VAT-certificate address (Address Line 1, Country, City/Emirate) is captured; Trade Licence upload is mandatory; and a completion e-mail is sent to the supplier on submission."));
kids.push(bullet("On submission the record moves to review, the requestor is notified, and the link is invalidated to prevent re-submission."));
kids.push(h2("4.3 Review, notifications & audit"));
kids.push(bullet("Reviewers see supplier-made changes highlighted for acknowledgement; confirmation is requested before sending notification e-mails."));
kids.push(bullet("All logos and communications are branded per business unit; every workflow action is audit-logged."));
kids.push(h2("4.4 Import / export & printing"));
kids.push(bullet("Import suppliers from an Excel template (duplicates detected by supplier number; re-import preserves app-managed fields)."));
kids.push(bullet("Export to the ERP/Oracle-Fusion Excel format and PDF; print any tab's full list with a chosen set of columns."));
kids.push(bullet("Download a supplier's documents individually or as a zip named by supplier number and name, with each file prefixed by its document type."));
kids.push(h2("4.5 Notifications, scheduled reports & emergency access"));
kids.push(bullet("Notification Groups: administrators maintain named recipient lists (for example ALL_USERS) of portal users, each with an enable/disable (Active) flag; disabled groups are retained but excluded from notifications."));
kids.push(bullet("Scheduled Reports: administrators schedule a supplier report (PDF or Excel) to be e-mailed as a file attachment to the active members of an active notification group, on a chosen date/time and frequency (Once / Daily / Weekly / Monthly), with optional business-unit scope; a \"Run now\" action is supported and the last-run status is shown."));
kids.push(bullet("Break-Glass emergency access: administrators can set or rotate an emergency-access secret from Settings; it is stored only as a salted hash (never in plaintext or the database), and the master switch is a deployment setting."));
kids.push(h2("4.6 Administration"));
kids.push(bullet("Manage users (roles, business units, active status), business units (branding + contact e-mail), departments, supplier field settings, lookups, notification groups, scheduled reports, and the audit log."));

kids.push(h1("5. Non-Functional Requirements"));
kids.push(table(["Area", "Requirement"],
 [["Security","Public self-registration is disabled by default (accounts are created by administrators); public self-service must not expose the database; strong OTP + token model; role/business-unit-scoped access; protected fields; break-glass emergency access secret stored only as a salted hash; document uploads cannot execute as script; SSRF-safe external fetches; security response headers; login lockout, idle lockout and active-session revocation."],
  ["Compliance","Capture UAE e-invoicing / VAT attributes; retain a signed declaration; full audit trail."],
  ["Integrations / e-mail","Outbound e-mail, including scheduled-report file attachments, is sent via Microsoft Graph or SMTP."],
  ["Availability / resilience","Retry Azure SQL transient faults; best-effort integrations (OCR, e-mail) never block a save; blob storage falls back to DB/disk."],
  ["Performance","Reference data cached; short-lived DB contexts; paginated lists."],
  ["Usability","Guided multi-step forms; per-field help; progress indicators on long operations; branded, accessible UI in light/dark."],
  ["Portability","Runs on-premises (SQL Server) or on Azure (Azure SQL, Blob, Entra, SMTP)."]],
 [2000, 7500]));

kids.push(h1("6. Assumptions & Constraints"));
kids.push(bullet("External parties access only their own record via a time-limited link and OTP; no external user accounts are created."));
kids.push(bullet("On Azure SQL, e-mail must be sent via SMTP (Database Mail is unavailable)."));
kids.push(bullet("Supplier numbers should be unique; a data-cleanup and unique-index migration is recommended before high-volume concurrent registration."));
kids.push(bullet("ERP posting is handled downstream from the exported data."));

kids.push(h1("7. Acceptance Criteria"));
kids.push(bullet("All functional requirements above are demonstrable; the solution builds cleanly and unit tests pass."));
kids.push(bullet("Security controls verified (see the PAT Report): no critical/high findings outstanding on the public self-service surface."));
kids.push(bullet("A supplier can be requested, complete and sign their data by self-service, and be reviewed and completed by staff, with the signed declaration retained."));

write(buildDoc({ title: "Supplier Portal — Updated BRD", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Updated BRD.docx");
