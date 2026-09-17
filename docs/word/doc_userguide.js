// Generates "Supplier Portal - Internal User Guide.docx" — a task-oriented guide for internal staff
// (procurement / business-application users and administrators) of the internal admin site at
// https://supplierportal.dubaiinvestments.com. Run: NODE_PATH set to the docx node_modules, then
//   node doc_userguide.js
const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, num, table, titlePage, tocPage, buildDoc, write, t } = g;
const C = g.colors;

const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const K = [];
const step = (s) => num(s, "steps");
const note = (label, text) => para([t(label + " ", { bold: true, color: C.NAVY2 }), t(text)], { after: 120 });

// ---------- Title + TOC ----------
K.push(...titlePage("Supplier Portal", "Internal User Guide", [
  ["Document", "Internal User Guide (admin site)"],
  ["Portal", "https://supplierportal.dubaiinvestments.com"],
  ["Audience", "Procurement & Business Application team (internal users and administrators)"],
  ["Version", "1.0"],
  ["Date", today],
]));
K.push(...tocPage());

// ---------- 1. Introduction ----------
K.push(h1("1. Introduction"));
K.push(para("The Supplier Portal is Dubai Investments' central system for collecting, reviewing and maintaining supplier master data across the group's business units. It has two connected sides:"));
K.push(bullet([t("The internal site", { bold: true }), t(" (this guide) — where your team registers suppliers, uploads data, invites suppliers to confirm their details, reviews what they submit, and manages configuration.")]));
K.push(bullet([t("The supplier self-service site", { bold: true }), t(" — a public, secure page where a supplier opens an emailed link, verifies with a one-time code, reviews and updates their own details, and signs a declaration. Their submission flows back to the internal site for review.")]));
K.push(para("This guide covers the internal site. It is task-oriented: each section describes what you see and the steps to complete a task."));
K.push(note("Who can do what:", "Most day-to-day work (viewing, assigning, notifying, editing) is available to normal users. The Settings area is limited to Administrators."));

// ---------- 2. Signing in ----------
K.push(h1("2. Signing In"));
K.push(step("Open https://supplierportal.dubaiinvestments.com in your browser."));
K.push(step("Sign in with your organisation account (Microsoft single sign-on) or, where enabled, your portal email and password."));
K.push(step("After signing in you land on the Home launcher; open Supplier Portal ▸ Manage Supplier to begin."));
K.push(h3("Good to know"));
K.push(bullet([t("Automatic sign-out: ", { bold: true }), t("for security the portal signs you out after a period of inactivity. Simply sign in again to continue.")]));
K.push(bullet([t("Appearance: ", { bold: true }), t("the top bar has a light/dark theme toggle and a control to collapse the left navigation so wide tables use the full width.")]));
K.push(bullet([t("Access is by role and business unit: ", { bold: true }), t("you see the suppliers and business units your account is granted. If something you expect is missing, contact an administrator.")]));

// ---------- 3. Getting around ----------
K.push(h1("3. Getting Around"));
K.push(para("The dark navigation panel on the left groups the portal's areas:"));
K.push(table(["Menu", "What it is"], [
  ["Home", "The launcher showing the portals your account can access."],
  ["Supplier Portal ▸ Manage Supplier", "The main workspace: the supplier list, filters, and all supplier actions."],
  ["Settings ▸ Business Unit", "Create and edit business units (name, code, logo, contact e-mail). Administrators only."],
  ["Settings ▸ Manage Users", "Create users, set roles and business-unit access, and handle pending registrations. Administrators only."],
  ["Settings ▸ Supplier Fields", "The Supplier Field Manager — choose which fields the supplier sees, which are mandatory, and their order. Administrators only."],
  ["Settings ▸ Customer Fields / Document Types", "Configuration for the customer module and document categories. Administrators only."],
  ["Settings ▸ Audit Log", "A searchable record of key actions (who did what, when). Administrators only."],
], [3400, 6200]));

// ---------- 4. Supplier lifecycle ----------
K.push(h1("4. The Supplier Lifecycle"));
K.push(para("Every supplier has a status that shows where it is in the data-gathering process, and an optional priority for internal triage."));
K.push(h2("4.1 Status"));
K.push(table(["Status", "Meaning"], [
  ["Not Submitted", "The record exists (added manually or by upload) but the supplier has not yet confirmed their details."],
  ["Awaiting Supplier Response", "The supplier has been invited (notified) and their secure link is open; you are waiting for them to submit."],
  ["For Review / Pending changes", "The supplier submitted changes that differ from the stored record; these await your review (shown by a bell next to the supplier name)."],
  ["Completed", "The supplier confirmed and submitted their details (with a signed declaration); no further action is required from them."],
], [3000, 6600]));
K.push(h2("4.2 Priority"));
K.push(para([t("Priority is an internal label (", { }), t("P1", { bold: true }), t(" highest, through ", {}), t("P4", { bold: true }), t(") you can set on each supplier to help your team focus. It is never shown to the supplier. You can filter the list by priority and the list sorts by priority first (P1 → P2 → P3 → P4, then unset).")]));

// ---------- 5. Manage Supplier ----------
K.push(h1("5. Manage Supplier (the Dashboard)"));
K.push(para("Manage Supplier is where you spend most of your time. From top to bottom:"));

K.push(h2("5.1 Summary tiles"));
K.push(para("Four tiles summarise the suppliers you can see: Total Suppliers, Not Submitted, Awaiting Supplier Response, and Completed."));

K.push(h2("5.2 Filters"));
K.push(para("Use the filter row to narrow the list. Filters combine, so you can, for example, show only P1 suppliers in one business unit that are Not Submitted."));
K.push(table(["Filter", "Use it to"], [
  ["Business Unit", "Show suppliers for one entity, or all business units you can access."],
  ["Supplier Name", "Type part of a name to search."],
  ["Status", "Show only a chosen status (e.g. Completed)."],
  ["Priority", "Show only P1 / P2 / P3 / P4."],
  ["Assigned To", "Show only suppliers assigned to a chosen team member (or unassigned)."],
], [2600, 7000]));
K.push(para([t("Below the filters, three tabs group the list by recency: ", {}), t("Recently Updated", { bold: true }), t(", ", {}), t("Updated Last Week", { bold: true }), t(", and ", {}), t("Everything Else", { bold: true }), t(". The count on each tab reflects the current filters.")]));

K.push(h2("5.3 Choosing columns"));
K.push(para([t("The ", {}), t("Columns", { bold: true }), t(" button above the table opens a menu where you tick the columns to show or hide. Beyond the default columns you can add Tax Organization Type, Supplier Type, TRN / VAT Registration Number, Trade License Number and Last Updated. Your choice is remembered in your browser; use ", {}), t("Reset", { bold: true }), t(" to return to the defaults.")]));

K.push(h2("5.4 Reading a row"));
K.push(bullet([t("Priority", { bold: true }), t(" — the coloured P1–P4 badge (blank if not set).")]));
K.push(bullet([t("Supplier Name", { bold: true }), t(" — the supplier's name. A bell with a number means the supplier submitted changes awaiting your review; click it to open the record.")]));
K.push(bullet([t("Status", { bold: true }), t(" — the workflow badge (see section 4).")]));
K.push(bullet([t("Assigned To / Department", { bold: true }), t(" — drop-downs you can change inline to assign ownership (see 5.6).")]));
K.push(bullet([t("Actions", { bold: true }), t(" — view attached documents, edit the record, and notify the supplier (see 5.7).")]));

K.push(h2("5.5 Pagination"));
K.push(para("The pager below the table shows “Page X of Y (N suppliers)” with Previous / Next buttons. When the current view fits on a single page the buttons are disabled but the summary still shows, so you always know how many records match."));

K.push(h2("5.6 Assigning suppliers"));
K.push(para("Assign a supplier to a team member and/or a department so responsibility is clear:"));
K.push(bullet([t("Per row: ", { bold: true }), t("change the Assigned To or Department drop-down on the supplier's row.")]));
K.push(bullet([t("In bulk: ", { bold: true }), t("tick the checkboxes on the left of several rows (or the header checkbox to select the page), then use the bulk assign controls to set the assignee or department for all selected, and Clear Selection when done.")]));

K.push(h2("5.7 Row actions"));
K.push(table(["Action", "What it does"], [
  ["View documents", "Opens the documents attached to the supplier (Trade License, VAT Certificate, bank letter, signed declaration, and others). Disabled when there are none."],
  ["Edit", "Opens the full supplier registration form to view or change the record (section 7)."],
  ["Notify", "Sends the supplier a secure self-service link by e-mail so they can confirm their details (section 8)."],
], [2600, 7000]));

// ---------- 6. Adding suppliers ----------
K.push(h1("6. Adding Suppliers"));
K.push(para("There are two ways to bring suppliers into the portal."));

K.push(h2("6.1 Register a single supplier"));
K.push(step("On Manage Supplier, click Register Supplier (top right)."));
K.push(step("Choose the business unit and fill in the supplier's details across the form tabs."));
K.push(step("Save. The supplier appears in the list as Not Submitted."));

K.push(h2("6.2 Upload many suppliers from Excel"));
K.push(para("Use this for bulk onboarding or migration."));
K.push(step("Click Generate Template and download the Excel template (optionally pre-scoped to a business unit)."));
K.push(step("Fill in one supplier per row using the template's columns, then save the file."));
K.push(step("Click Upload Supplier Data, choose the business unit, and select your file."));
K.push(step("Review the import summary. It reports how many suppliers were imported and lists any rows where an optional value could not be matched."));
K.push(h3("Understanding the import report"));
K.push(bullet([t("Duplicates are skipped: ", { bold: true }), t("a supplier already present (by Supplier Number) is not imported again.")]));
K.push(bullet([t("Unrecognised optional values are left blank: ", { bold: true }), t("if a Branch Name or City does not match the portal's lists, the supplier still imports but that single value is left blank and listed in the “Imported with blanks” report. The rest of the row is unaffected.")]));
K.push(note("Tip:", "To reduce blanks, make sure bank branch and city names in your file match the names configured in the portal (matching ignores upper/lower case)."));

// ---------- 7. Editing ----------
K.push(h1("7. Editing a Supplier"));
K.push(para("The supplier registration form is organised into tabs (for example Business / Organization, Address, Contact, Site, and Bank). A progress ring shows completion, and required fields are marked with an asterisk."));
K.push(bullet([t("Which fields appear, and which are required, is controlled centrally in ", {}), t("Settings ▸ Supplier Fields", { bold: true }), t(" (section 10.3).")]));
K.push(bullet([t("VAT: ", { bold: true }), t("when a supplier is not VAT-registered, the TRN and VAT Registration Type fields are hidden. A Yes/No question asks whether the address is the same as on the Trade License and VAT Certificate; if not, a separate VAT-certificate address is captured.")]));
K.push(bullet([t("Documents: ", { bold: true }), t("attach or view the Trade License, VAT Certificate, Bank Confirmation Letter and other files from the form.")]));
K.push(para("Click Save to store your changes. Editing here does not require the supplier to re-confirm."));

// ---------- 8. Inviting suppliers ----------
K.push(h1("8. Inviting Suppliers to Confirm Their Details"));
K.push(para("Rather than typing everything yourself, you can invite suppliers to review and confirm their own data through the secure self-service site."));
K.push(h2("8.1 Sending an invitation"));
K.push(bullet([t("One supplier: ", { bold: true }), t("use the Notify action on the supplier's row.")]));
K.push(bullet([t("Many at once: ", { bold: true }), t("use Notify All Suppliers (top right) to invite everyone in the current scope who has a valid e-mail. Suppliers without a valid e-mail are skipped and reported.")]));
K.push(h2("8.2 What the supplier experiences"));
K.push(num("They receive an e-mail with a secure link and open it.", "nums"));
K.push(num("They enter a 6-digit verification code sent to their e-mail (one-time password).", "nums"));
K.push(num("They review and update their details across the same tabbed form, attaching documents as needed.", "nums"));
K.push(num("They sign a declaration (typed name plus a drawn signature) and submit.", "nums"));
K.push(num("They receive a “Supplier Registration Completed” confirmation e-mail, and the record moves to Completed.", "nums"));
K.push(note("Security by design:", "the link times out after a period of inactivity. If it expires, the supplier simply reopens the most recent e-mail to get a fresh link and code."));
K.push(h2("8.3 Reviewing supplier-submitted changes"));
K.push(para([t("When a supplier's submission differs from the stored record, a ", {}), t("bell with a count", { bold: true }), t(" appears next to their name on the dashboard. Open the record to see the changed fields highlighted, then confirm or adjust them. The signed declaration is stored with the supplier's documents and can be viewed and downloaded.")]));

// ---------- 9. Printing ----------
K.push(h1("9. Printing & Exporting"));
K.push(bullet([t("Print: ", { bold: true }), t("the Print button opens a dialog where you choose the columns to include; the whole filtered list is printed (not just the current page), with a clean, readable layout.")]));
K.push(bullet([t("Export: ", { bold: true }), t("where enabled, export the filtered supplier list to a file for use outside the portal.")]));

// ---------- 10. Settings ----------
K.push(h1("10. Settings (Administrators)"));
K.push(para("The Settings area is available to Administrators and controls how the portal behaves for everyone."));

K.push(h2("10.1 Business Unit"));
K.push(para("Create and edit the group entities suppliers are registered under. Each business unit has a name and code, a logo (shown on the supplier's self-service page and e-mails), and a contact e-mail used, for example, on the “session expired” help screen."));

K.push(h2("10.2 Manage Users"));
K.push(bullet([t("Roles: ", { bold: true }), t("Administrators manage configuration and users; Normal Users work with suppliers. Grant each user access to the specific portals and business units they need.")]));
K.push(bullet([t("New / edit user: ", { bold: true }), t("use New User to add someone, or the edit action on a row to change their role, access or status.")]));
K.push(bullet([t("Pending registrations: ", { bold: true }), t("people who self-register appear in a banner “awaiting linking” until an administrator links or rejects them. Reject an individual entry with its × button, or use “Remove N invalid” to clear malformed (spam / scanner) entries in one click. Real registrations are preserved.")]));

K.push(h2("10.3 Supplier Field Manager"));
K.push(para("Controls the supplier form for every supplier:"));
K.push(bullet([t("Visible to supplier: ", { bold: true }), t("show or hide each field on the self-service form.")]));
K.push(bullet([t("Mandatory: ", { bold: true }), t("make a visible field required before the supplier can submit.")]));
K.push(bullet([t("Order and tabs: ", { bold: true }), t("arrange fields into tabs (steps) and set their sequence. The internal edit form and the supplier form both follow these settings.")]));
K.push(note("Note:", "if a supplier reports a field is missing, check that it is set to visible for the relevant tab here."));

K.push(h2("10.4 Customer Fields & Document Types"));
K.push(para("Equivalent configuration for the customer module, and the list of document categories used across the portal."));

K.push(h2("10.5 Audit Log"));
K.push(para("A record of significant actions — user changes, deletions, notifications and more — with who performed them and when. Use it to review activity and support investigations."));

// ---------- 11. Troubleshooting ----------
K.push(h1("11. Tips & Troubleshooting"));
K.push(table(["Situation", "What to do"], [
  ["A supplier says their link shows “Session expired”", "That is normal after inactivity. Ask them to reopen the most recent Partner Connect e-mail and click the link again to get a fresh code. The screen also shows a business-unit e-mail they can contact."],
  ["Imported rows show blanks for Branch or City", "The value did not match the portal's lists. The supplier still imported; add the correct branch/city to the portal and re-import, or leave the field for later editing."],
  ["The pager seems to be missing", "When the filtered list fits on one page, Previous/Next are disabled but the “Page 1 of 1 (N suppliers)” summary still shows. Widen the filter to see more."],
  ["A field the supplier needs is not on their form", "Check Settings ▸ Supplier Fields — the field must be set to Visible for the correct tab."],
  ["A supplier did not receive a notification", "Confirm the supplier has a valid Contact E-mail; the bulk “Notify All” skips and reports addresses that are missing or invalid."],
  ["Printed output looks faint", "Print again — the layout is designed to stay readable whether or not your browser prints background colours."],
], [3400, 6200]));

// ---------- 12. Glossary ----------
K.push(h1("12. Glossary"));
K.push(table(["Term", "Meaning"], [
  ["Self-service", "The supplier-facing site where a supplier confirms their own details via a secure link."],
  ["OTP", "One-Time Password — the 6-digit code e-mailed to a supplier to verify their identity."],
  ["Declaration", "The statement a supplier signs (typed name + drawn signature) confirming their information is true; stored as a PDF."],
  ["Business Unit", "A Dubai Investments entity a supplier is registered under."],
  ["Pending changes", "Supplier-submitted values that differ from the stored record and await internal review (shown by the bell)."],
  ["Priority", "Internal triage label P1–P4; never shown to suppliers."],
], [2600, 7000]));

// ---------- Build ----------
const doc = buildDoc({ title: "Supplier Portal — Internal User Guide", children: K });
write(doc, "Supplier Portal - Internal User Guide.docx");
