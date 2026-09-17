const g = require("./docgen.js");
const { para, h1, h2, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Administration Guide", [
  ["Document", "Administration Guide (Supplier Portal)"],
  ["Scope", "Supplier Portal only"],
  ["Audience", "Administrators"],
  ["Version", "2.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Administrator Role"));
kids.push(para("Administrators have full access to configuration and all supplier data. Administration is reached from the Settings section of the navigation menu: Business Unit, Manage Users, Supplier Fields, Audit Log, Notification Group, Scheduled Reports and Break-Glass Access. Register and Upload actions and all Admin pages are restricted to Administrators server-side."));

kids.push(h1("2. Manage Users"));
kids.push(h2("2.1 Create / edit a user"));
kids.push(num("Settings › Manage Users › Add user (or edit an existing row)."));
kids.push(num("Set the display name, e-mail/login, department and designation."));
kids.push(num("Set the Role — Administrator or Normal User."));
kids.push(num("Grant Supplier portal access."));
kids.push(num("Assign one or more Business Units; the user is scoped to these. Set the company business unit if used for branding."));
kids.push(num("Set Active on/off. Save."));
kids.push(h2("2.2 Status filter & lifecycle"));
kids.push(bullet("The user list filters by status and defaults to Active. Pending registrations await approval; deactivating a user revokes their access — an active session ends within about two minutes."));
kids.push(bullet("Authentication source is Local or Active Directory; AD users validate against the directory, local users against a stored password."));
kids.push(bullet("Public self-registration is disabled by default; administrators create all accounts here in Manage Users."));

kids.push(h1("3. Business Units"));
kids.push(bullet("Create business units with a Code and Name. Upload a Logo (full-size) and thumbnail — these brand the UI, e-mails and documents for that unit; the uploaded logo is also shown on the public self-service pages."));
kids.push(bullet("Optionally set a Domain for per-business-unit self-service URLs, and an Email Address (contact / reply-to) — the latter is shown to suppliers on the self-service pages, e.g. on the 'link expired' screen."));

kids.push(h1("4. Field & Tab Settings"));
kids.push(bullet("For each supplier field, control the display label, wizard step, order, whether it is Mandatory, and whether it is Visible to the supplier on the self-service form. You can also hide (remove) a field and show/hide whole tabs."));
kids.push(bullet("Fields hidden from suppliers are protected server-side — a supplier cannot change them even by tampering with the page."));

kids.push(h1("5. Lookups (Reference Data)"));
kids.push(para("Dropdown values (tax organisation types, supplier types, countries, payment methods/terms, currencies, prefixes, statuses, VAT/e-invoicing categories, licensing authorities, etc.) are held as reference data keyed by category. Keep these current; changes appear in the app within a few minutes (values are cached)."));

kids.push(h1("6. Audit Log"));
kids.push(bullet("Settings › Audit Log lists actor, action, entity and details for logins, record create/update, activations, notifications and break-glass activations. Use it to trace who changed what and when."));
kids.push(bullet("Supplier Activity shows supplier-originated (self-service) field changes for review."));

kids.push(h1("7. Self-Service Link Management"));
kids.push(bullet("Sending a notification issues a fresh, time-limited link (14 days) and a one-time passcode to the supplier. Completing the review invalidates outstanding links."));
kids.push(bullet("Administrators can send an admin test link to preview the self-service experience without affecting the requestor workflow."));
kids.push(bullet("A supplier gets at most five OTP sends and five verify attempts per link before a new link is required."));

kids.push(h1("8. E-mail Configuration"));
kids.push(bullet("E-mail is sent via SMTP when configured, otherwise via SQL Server Database Mail. On Azure SQL, SMTP must be configured (Database Mail is unavailable). See the Configuration Document for the Email:Smtp:* settings."));
kids.push(bullet("Logos are embedded in e-mails per business unit; confirm branding after changing a unit's logo."));

kids.push(h1("9. Notification Groups"));
kids.push(para("Settings › Notification Group defines named recipient lists (for example ALL_USERS) that are used as the recipients for Scheduled Reports. Administrator only."));
kids.push(bullet("Each group has a unique name, a set of member users, and an Active enable/disable toggle. A disabled group is kept but excluded from notifications."));
kids.push(bullet("Administrators create, edit and delete groups and pick members from a filterable user list."));
kids.push(bullet("Only the active members of an active group receive a scheduled report."));

kids.push(h1("10. Scheduled Reports"));
kids.push(para("Settings › Scheduled Reports schedules a supplier report that is generated and e-mailed as a file attachment to the active members of a selected notification group (the group itself must also be active). Administrator only."));
kids.push(h2("10.1 Configuring a schedule"));
kids.push(bullet("Report name and format — a PDF, or Excel (all fields)."));
kids.push(bullet("Frequency — Once, Daily, Weekly or Monthly."));
kids.push(bullet("Start / next-run date and time — entered and displayed in Gulf Standard Time (Asia/Dubai); the time zone is configurable."));
kids.push(bullet("Recipient notification group, and an optional Business Unit scope (otherwise all suppliers are included)."));
kids.push(h2("10.2 Running and monitoring"));
kids.push(bullet("The list shows the next run, the last-run status (Sent / Partial / Skipped / Failed) with detail, an enable/disable toggle, and a Run now button for on-demand sending."));
kids.push(bullet("A background runner fires due schedules automatically: a Once schedule disables itself after running, while a recurring schedule advances to the next occurrence."));
kids.push(bullet("Important: attachments require e-mail delivery via Microsoft Graph or SMTP. SQL Server Database Mail cannot attach in-memory files, so with only Database Mail configured a run is recorded as Failed."));

kids.push(h1("11. Emergency Access (Break-Glass)"));
kids.push(bullet("Settings › Break-Glass Access lets an Administrator set or rotate the emergency-access secret. The secret is stored only as a salted PBKDF2-SHA256 hash — never in plaintext and never in the database — is never displayed, and every set or rotate is audited."));
kids.push(bullet("The master on/off is a deployment setting (BreakGlass:Enabled), not a UI toggle. Break-glass works only when it is enabled AND a secret has been configured."));
kids.push(bullet("Break-glass provides time-boxed emergency Administrator access. Every activation is rate-limited, constant-time verified and audit-logged. Use only per your security procedure."));

kids.push(h1("12. Operational Notes"));
kids.push(bullet("Change the seeded default administrator password immediately after deployment."));
kids.push(bullet("Keep all secrets (connection strings, SMTP/OCR keys, client secrets) out of committed configuration; rotate periodically."));
kids.push(bullet("Before high-volume concurrent registration, run the recommended supplier-number de-duplication and unique-index migration (see the PAT Report residual items)."));
kids.push(bullet("Deactivate leavers promptly; their sessions end within about two minutes."));

write(buildDoc({ title: "Supplier Portal — Administration Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Administration Guide.docx");
