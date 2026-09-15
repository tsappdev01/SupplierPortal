const g = require("./docgen.js");
const { para, h1, h2, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Administration Guide", [
  ["Document", "Administration Guide"],
  ["Audience", "Administrators"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Administrator Role"));
kids.push(para("Administrators have full access to configuration and all portal data. Administration is reached from the Settings section of the navigation menu: Business Unit, Manage Users, Supplier Fields, Customer Fields, Document Types, and Audit Log. Register and Upload actions and all Admin pages are restricted to Administrators server-side."));

kids.push(h1("2. Manage Users"));
kids.push(h2("2.1 Create / edit a user"));
kids.push(num("Settings › Manage Users › Add user (or edit an existing row)."));
kids.push(num("Set the display name, e-mail/login, department and designation."));
kids.push(num("Set the Role — Administrator, Normal User or Leasing Manager."));
kids.push(num("Grant Portal access — Supplier, Customer, Landlord (and Broker) as required."));
kids.push(num("Assign one or more Business Units; the user is scoped to these. Set the company business unit if used for branding."));
kids.push(num("Set Active on/off. Save."));
kids.push(h2("2.2 Status filter & lifecycle"));
kids.push(bullet("The user list filters by status and defaults to Active. Pending registrations await approval; deactivating a user revokes their access — an active session ends within about two minutes."));
kids.push(bullet("Authentication source is Local or Active Directory; AD users validate against the directory, local users against a stored password."));

kids.push(h1("3. Business Units"));
kids.push(bullet("Create business units with a Code and Name. Upload a Logo (full-size) and thumbnail — these brand the UI, e-mails and documents for that unit."));
kids.push(bullet("Optionally set a Domain for per-business-unit self-service URLs."));

kids.push(h1("4. Field & Tab Settings"));
kids.push(h2("4.1 Supplier Fields"));
kids.push(bullet("For each supplier field, control the display label, wizard step, order, whether it is Mandatory, and whether it is Visible to the supplier on the self-service form. You can also hide (remove) a field and show/hide whole tabs."));
kids.push(bullet("Fields hidden from suppliers are protected server-side — a supplier cannot change them even by tampering with the page."));
kids.push(h2("4.2 Customer Fields"));
kids.push(bullet("Configure the customer (KYC) field set: section, group, order, field kind, and the Business/Customer control (editable/read-only) and required/visible flags."));
kids.push(h2("4.3 Document Types (Customer KYC)"));
kids.push(bullet("Maintain the KYC document checklist: document name, requirement note, display order, Required flag and Active flag. Required documents must be uploaded before a customer can submit. Turn off Active to hide a type."));

kids.push(h1("5. Lookups (Reference Data)"));
kids.push(para("Dropdown values (tax organisation types, supplier types, countries, payment methods/terms, currencies, prefixes, statuses, VAT/e-invoicing categories, licensing authorities, etc.) are held as reference data keyed by category. Keep these current; changes appear in the app within a few minutes (values are cached)."));

kids.push(h1("6. Audit Log"));
kids.push(bullet("Settings › Audit Log lists actor, action, entity and details for logins, record create/update, activations, notifications and break-glass activations. Use it to trace who changed what and when."));
kids.push(bullet("Supplier Activity shows supplier-originated (self-service) field changes for review."));

kids.push(h1("7. Self-Service Link Management"));
kids.push(bullet("Sending a notification issues a fresh, time-limited link (14 days) and a one-time passcode to the supplier/customer. Completing the review invalidates outstanding links."));
kids.push(bullet("Administrators can send an admin test link to preview the self-service experience without affecting the requestor workflow."));
kids.push(bullet("A supplier gets at most five OTP sends and five verify attempts per link before a new link is required."));

kids.push(h1("8. E-mail Configuration"));
kids.push(bullet("E-mail is sent via SMTP when configured, otherwise via SQL Server Database Mail. On Azure SQL, SMTP must be configured (Database Mail is unavailable). See the Configuration Document for the Email:Smtp:* settings."));
kids.push(bullet("Logos are embedded in e-mails per business unit; confirm branding after changing a unit's logo."));

kids.push(h1("9. Emergency Access (Break-glass)"));
kids.push(bullet("Break-glass provides time-boxed emergency Administrator access and is disabled unless a secret hash is configured. Every activation is rate-limited, constant-time verified and audit-logged. Use only per your security procedure; disable when not required."));

kids.push(h1("10. Operational Notes"));
kids.push(bullet("Change the seeded default administrator password immediately after deployment."));
kids.push(bullet("Keep all secrets (connection strings, SMTP/OCR keys, client secrets) out of committed configuration; rotate periodically."));
kids.push(bullet("Before high-volume concurrent registration, run the recommended supplier/customer-number de-duplication and unique-index migration (see the PAT Report residual items)."));
kids.push(bullet("Deactivate leavers promptly; their sessions end within about two minutes."));

write(buildDoc({ title: "Supplier Portal — Administration Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Administration Guide.docx");
