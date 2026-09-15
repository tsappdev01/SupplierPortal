const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];
const step = (t) => para([g.t(t)], { after: 60 });

kids.push(...titlePage("Supplier Portal", "User Guide (Internal Staff)", [
  ["Document", "User Guide — Internal Staff"],
  ["Audience", "Business users / reviewers (Normal User, Administrator)"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Getting Started"));
kids.push(h2("1.1 Signing in"));
kids.push(bullet("Open the portal URL. Depending on configuration you either sign in with your organisation account (SSO) or with your e-mail/login name and password (local / Active Directory)."));
kids.push(bullet("After 15 minutes of inactivity you are signed out automatically; sign in again to continue."));
kids.push(bullet("If you enter the wrong password five times, sign-in is locked for 15 minutes."));
kids.push(h2("1.2 What you can see"));
kids.push(para("Access is scoped by role and business unit. Administrators see everything; a Normal User sees the suppliers/customers assigned to them or owned by their department within their business units. Use the theme toggle (top bar) for light/dark mode."));

kids.push(h1("2. Manage Supplier Dashboard"));
kids.push(para("The dashboard lists suppliers with KPI tiles (Total, Not Submitted, Awaiting Supplier Response, Completed) and tabs for Recently Updated / Updated Last Week / Everything Else."));
kids.push(h2("2.1 Finding suppliers"));
kids.push(bullet("Filter by Business Unit, Supplier Name, Status, Priority and Assigned To. Switch tabs to change the recency window. Use Previous/Next to page."));
kids.push(h2("2.2 Row actions"));
kids.push(bullet("Documents — open the Attached Documents window (preview, open, download, or download all as a zip)."));
kids.push(bullet("Edit — open the supplier record to view or change details."));
kids.push(bullet("E-mail — send the supplier a self-service link (a confirmation is requested first)."));
kids.push(bullet("Assigned To / Department — set the owner directly from the row."));
kids.push(h2("2.3 Registering a supplier"));
kids.push(num("Click Register Supplier (Administrators)."));
kids.push(num("Complete the multi-step form: business/organisation, tax & VAT, trade licence, address, contacts, site & business unit, and payment/banking. Mandatory fields are marked with an asterisk; hover the help icon on a field for guidance."));
kids.push(num("Save. A supplier number is generated for the business unit."));
kids.push(h2("2.4 Requesting data from a supplier"));
kids.push(num("From a supplier row, click the e-mail icon and confirm. The supplier receives a branded link and completes their own data (see the Self-Service Guide)."));
kids.push(num("The status moves to Awaiting Supplier Response. When they submit, it moves to For Review and you are notified."));
kids.push(h2("2.5 Reviewing a submission"));
kids.push(bullet("Open the supplier; fields the supplier changed are highlighted for your acknowledgement. Review the uploaded documents and the signed declaration PDF."));
kids.push(bullet("When satisfied, mark the record Completed. Completing the review invalidates the supplier's self-service link."));
kids.push(h2("2.6 Notify all / bulk actions"));
kids.push(bullet("Select rows with the checkboxes to send notifications or assign a department to several suppliers at once. Selection clears when you switch tabs. Notify All Suppliers sends to every eligible supplier in the current business unit (a confirmation is shown)."));

kids.push(h1("3. Working with Documents"));
kids.push(bullet("In Attached Documents each card shows a preview with an Open and a Download button; a spinner appears until each preview loads."));
kids.push(bullet("Download all (zip) downloads every file with a progress bar; the archive is named 'Supplier Number_Supplier Name.zip' and each file is prefixed with its document type (e.g. 'Trade License _ …')."));
kids.push(bullet("All images as PDF combines image documents into one PDF (a message appears if there are no images)."));

kids.push(h1("4. Printing & Exporting"));
kids.push(h2("4.1 Print"));
kids.push(num("Click Print. Choose the columns to include (Select all / Default / Clear)."));
kids.push(num("Click Print — every supplier in the active tab is printed (not just the current page). The layout adapts: portrait for a few columns, landscape for many."));
kids.push(h2("4.2 Generate template / import"));
kids.push(bullet("Generate Template downloads a blank or pre-populated Excel template for the business to fill in and re-upload."));
kids.push(bullet("Upload Supplier Data imports the completed template; duplicates are detected by supplier number, and app-managed fields (priority, assignment, department, the VAT-address block and the declaration audit) are preserved on re-import."));

kids.push(h1("5. Manage Customer"));
kids.push(para("The Customer module mirrors the supplier flow for KYC records: a dashboard scoped to your business units, register/edit, self-service with OTP, a configurable KYC document checklist, and a Draft → For Review → Completed workflow."));

kids.push(h1("6. Tips"));
kids.push(bullet("A red asterisk marks a mandatory field; the field-help popups explain purpose and expected values."));
kids.push(bullet("Long operations (template generation, downloads) show progress; wait for completion."));
kids.push(bullet("If you are unexpectedly signed out, it is due to the 15-minute inactivity lock — simply sign in again."));

write(buildDoc({ title: "Supplier Portal — User Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - User Guide.docx");
