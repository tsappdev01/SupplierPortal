// Generates "Supplier Portal - Quick Start Guide.docx" — a short, plain-language guide for internal
// staff (procurement / business-application users) of the admin site at
// https://supplierportal.dubaiinvestments.com. Deliberately brief: sign-in, a map of the screen, and
// the handful of everyday tasks in numbered steps. Run with NODE_PATH pointing at the docx node_modules:
//   node doc_quickguide.js
const g = require("./docgen.js");
const { para, h1, h2, num, bullet, table, titlePage, buildDoc, write, t } = g;
const C = g.colors;

const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const K = [];
const step = (s) => num(s, "steps");
const tip = (label, text) => para([t(label + " ", { bold: true, color: C.NAVY2 }), t(text)], { after: 120 });

// ---------- Title ----------
K.push(...titlePage("Supplier Portal", "Quick Start Guide", [
  ["Document", "Quick Start Guide (internal admin site)"],
  ["Portal", "https://supplierportal.dubaiinvestments.com"],
  ["Audience", "Procurement & Business Application team"],
  ["Read time", "About 5 minutes"],
  ["Version", "1.0"],
  ["Date", today],
]));

// ---------- 1. Welcome ----------
K.push(h1("1. What is the Supplier Portal?"));
K.push(para(
  "The Supplier Portal is where the team keeps supplier information up to date. You can add suppliers, " +
  "edit their details, invite suppliers to fill in their own information online, and export the list for " +
  "reporting. This guide covers the everyday tasks in a few short steps each."));

// ---------- 2. Sign in ----------
K.push(h1("2. Signing in"));
K.push(step("Open https://supplierportal.dubaiinvestments.com in your web browser (Chrome or Edge)."));
K.push(step("Sign in with your company account (single sign-on), or with the username and password provided to you."));
K.push(step("You land on the Home page. Use the dark menu on the left to move around."));
K.push(tip("Tip:", "If you cannot sign in, contact your administrator — accounts are created for you; there is no public self-registration."));

// ---------- 3. Finding your way ----------
K.push(h1("3. Finding your way around"));
K.push(para("Everything is reached from the left-hand menu:", { after: 80 }));
K.push(bullet([t("Supplier Portal → Manage Supplier", { bold: true }), t(" — the main screen: the list of all suppliers.")]));
K.push(bullet([t("Settings", { bold: true }), t(" — admin-only area (users, supplier fields, notification groups, scheduled reports, audit log).")]));
K.push(para("The Manage Supplier screen has four count tiles at the top — Total Suppliers, Not Submitted, " +
  "Awaiting Supplier Response, and Completed. Click a tile to filter the list to that group.", { after: 120 }));

// ---------- 4. Everyday tasks ----------
K.push(h1("4. Everyday tasks"));

K.push(h2("Find a supplier"));
K.push(step("Go to Manage Supplier."));
K.push(step("Type a name in the Supplier Name box, or use the Business Unit, Status and Priority filters."));
K.push(step("The list updates as you filter. Use the page buttons at the bottom to move through long lists."));

K.push(h2("Add a new supplier"));
K.push(step("On Manage Supplier, click Register Supplier."));
K.push(step("Fill in the details across the sections (Business, Address & Contact, Site, Banking). Required fields are marked with a red asterisk (*)."));
K.push(step("Click Save. The supplier now appears in the list."));

K.push(h2("Edit a supplier"));
K.push(step("Find the supplier and open it from the list."));
K.push(step("Change the details you need. Move between sections with Next / Back."));
K.push(step("Click Save."));

K.push(h2("Invite a supplier to update their own details"));
K.push(para("Instead of typing everything yourself, you can send the supplier a secure link to complete their " +
  "own information online (Partner Connect).", { after: 60 }));
K.push(step("Find the supplier in the list and click Notify."));
K.push(step("Check the email address, then click Send Email. The supplier receives a link and a one-time code."));
K.push(step("When they finish, their status changes to Awaiting Supplier Response → Completed, and you are notified of the changes to review."));
K.push(tip("Note:", "Use Notify again to resend the invitation if the supplier did not receive it."));

K.push(h2("Review changes a supplier submitted"));
K.push(step("Suppliers with new changes show a small bell/notification indicator in the list."));
K.push(step("Open the supplier to see what changed (edited fields are highlighted) and their uploaded documents."));
K.push(step("Confirm the details and Save to accept them."));

K.push(h2("Set a supplier's priority"));
K.push(step("In the list, set the Priority (P1–P4) for a supplier so the team knows what to work on first."));
K.push(step("Use the Priority filter to focus on the most urgent suppliers."));

K.push(h2("Show or hide table columns"));
K.push(step("On Manage Supplier, use the column chooser in the table header to add or remove columns."));
K.push(step("Your choice is remembered on your computer for next time. Use Reset to return to the default columns."));

K.push(h2("Export or print the list"));
K.push(step("Use Export to download the list as Excel or PDF, or Print for a printable page."));
K.push(step("Exports respect the filters you have applied, so filter first to export just what you need."));

K.push(h2("Upload many suppliers at once"));
K.push(step("Click Upload Supplier Data."));
K.push(step("First click Generate Template to download the spreadsheet (blank, or pre-filled with existing data), fill it in, and save it."));
K.push(step("Upload the completed file. The portal adds new suppliers and updates existing ones, and reports anything it could not read."));

// ---------- 5. Statuses at a glance ----------
K.push(h1("5. Supplier statuses at a glance"));
K.push(table(
  ["Status", "What it means"],
  [
    ["Not Submitted", "The supplier has not yet provided or confirmed their details."],
    ["Awaiting Supplier Response", "An invitation was sent; waiting for the supplier to complete their information."],
    ["Completed", "The supplier's information has been submitted and is up to date."],
  ],
  [3000, 6800]));

// ---------- 6. Tips ----------
K.push(h1("6. Handy tips"));
K.push(bullet("Click a count tile at the top to quickly filter the list (e.g. only Completed)."));
K.push(bullet("Required fields have a red asterisk (*); the form will tell you if something mandatory is missing when you click Next or Save."));
K.push(bullet("Hover the small information (i) icons on a form for guidance on what to enter."));
K.push(bullet("Always Save before leaving a supplier, or your changes are not kept."));

// ---------- 7. Getting help ----------
K.push(h1("7. Getting help"));
K.push(para("If something does not work as expected, or you need access to a feature you cannot see, contact your " +
  "Supplier Portal administrator (Business Applications team). Administrators can manage users, fields, " +
  "notification groups and scheduled reports under Settings.", { after: 120 }));

// ---------- Build ----------
write(buildDoc({ title: "Supplier Portal - Quick Start Guide", children: K }),
  "Supplier Portal - Quick Start Guide.docx");
