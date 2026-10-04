const g = require("./docgen.js");
const { para, h1, h2, h3, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Self-Service Supplier Portal Guide", [
  ["Document", "Self-Service Supplier Portal Guide"],
  ["Audience", "Suppliers (external) completing their information"],
  ["Version", "2.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Overview"));
kids.push(para("Partner Connect is the Dubai Investments supplier front-end. It lets you review and complete your company's information securely, directly from a link sent to you by e-mail. You do not need a user account, and there is no public self-registration — access is only via the secure link we send you."));
kids.push(para("Partner Connect runs on a hardened, internet-facing host (partnerconnect.dubaiinvestments.com) and passes your entries to the internal Supplier Portal through a dedicated self-service interface, so your data reaches Dubai Investments safely. Access is protected by a one-time passcode (OTP) sent to your registered e-mail, and the link is valid for a limited time. Your information is used to keep your supplier record accurate and UAE e-invoicing compliant."));
kids.push(para("You will need: the e-mail invitation containing your secure link, access to the e-mail inbox on file, and your company documents (trade licence, VAT certificate, and — if your bank details change — a bank confirmation letter).", { size: 18, color: g.colors.GREY }));

kids.push(h1("2. Step 1 — Open Your Secure Link"));
kids.push(num("Open the invitation e-mail from Dubai Investments (Partner Connect) and click the secure link, or paste it into your browser."));
kids.push(num("The link is personal to your company and time-limited. There is no way to sign up on your own; if the link has expired you will see a clear session-expired screen — contact your Dubai Investments contact for a new link."));

kids.push(h1("3. Step 2 — Verify Your Identity (OTP)"));
kids.push(num("On the Verify Your Identity screen, a one-time code is sent to the e-mail address on file for your company."));
kids.push(num("Open your inbox, copy the code and enter it on the screen."));
kids.push(num("Click Verify Code. If you did not receive it, click Resend code — a short progress indicator shows the code being sent again."));
kids.push(para("For your security: you have a limited number of attempts and sends. If the code doesn't arrive, check your spam folder and confirm the e-mail on file with your Dubai Investments contact.", { size: 18, color: g.colors.GREY }));

kids.push(h1("4. Step 3 — Complete Your Information"));
kids.push(para("After verification, the supplier wizard opens. A progress ring and a step-by-step stepper at the top show how far through you are and let you move between the tabs you have been given. Required fields are marked with a red asterisk (*). A small information (i) icon next to a field opens a short tooltip explaining what is expected."));
kids.push(para("Depending on what Dubai Investments has requested, you will see: (1) Business / Organisation, (2) Address & Contact Information, and — where configured — Site / Business Unit and Payment / Banking. Address and Contact are combined on a single tab."));

kids.push(h2("4.1 Business / Organisation"));
kids.push(para("Capture your company's legal and tax identity:"));
kids.push(bullet("Supplier Name."));
kids.push(bullet("Tax Organisation Type — shown read-only; this is set by Dubai Investments."));
kids.push(bullet("Supplier Type."));
kids.push(bullet("Trade Licence Number, Issue Date, Expiry Date and Issuing Body. The dates are sanity-checked: the issue date cannot be in the future and the expiry date cannot be before the issue date."));
kids.push(bullet("Taxpayer Country."));
kids.push(bullet([g.t("VAT Registered? ", { bold: true }), g.t("If you select Yes, the TRN / VAT Registration Number and VAT Registration Type appear and are required, and uploading the VAT Certificate becomes mandatory. The TRN / VAT Registration Number must be exactly 15 digits.")]));
kids.push(bullet("Supplier TIN — must be exactly 10 digits."));
kids.push(bullet("Business Partner Type."));
kids.push(bullet("eInvoicing Wave Applicability."));

kids.push(h2("4.2 VAT Group Details"));
kids.push(para("If your VAT Registration Type is a VAT Group, a structured VAT Group table appears. List each entity in the group; at least one fully-completed row is required, and each row's TRN (15 digits) and TIN (10 digits) length is checked. The column headers carry information tooltips to guide you."));
kids.push(table(["Column", "What to enter"],
 [["Business Legal Name","The legal name of the group entity."],
  ["Address","The entity's address as per its trade licence."],
  ["E-Invoicing Wave","Wave 1 or Wave 2."],
  ["TRN Number","15 digits, as per the VAT Tax Group certificate."],
  ["TIN Number","10 digits, obtained from the EmaraTax portal. This is not the same as the VAT TRN."],
  ["Bus. Location Type","Mainland or Free Zone."]],
 [2500, 7000]));

kids.push(h2("4.3 Address & Contact Information"));
kids.push(para("Address and Contact are completed together on one tab."));
kids.push(h3("Address"));
kids.push(bullet("Enter your main company address."));
kids.push(bullet([g.t("\"Is your address the same as your Trade Licence and VAT Certificate?\" ", { bold: true }), g.t("Choose Yes or No. If you choose No, a mandatory \"Address (As per VAT Certificate)\" block appears — Address Line 1, Country, and City / Emirate are required.")]));
kids.push(h3("Contact"));
kids.push(bullet("Enter the contact person's name and e-mail address(es); e-mail format is validated."));
kids.push(bullet("Enter Phone and Mobile using the combined control on each — the country dial code, area code and number are captured together in a single field."));

kids.push(h2("4.4 Site / Business Unit and Payment / Banking"));
kids.push(para("Where Dubai Investments has requested them, complete the Site / Business Unit tab and the Payment / Banking tab (bank, branch, account name/number, IBAN and SWIFT/BIC). If you change your bank account, a Bank Confirmation Letter is required (see Documents)."));

kids.push(h2("4.5 Documents"));
kids.push(bullet("Trade Licence — mandatory. It is enforced both when you move to the next tab and again on final submit."));
kids.push(bullet("VAT Certificate — mandatory when you are VAT registered."));
kids.push(bullet("Bank Confirmation Letter — required if you change your bank account details."));
kids.push(bullet("Other Documents — upload any further files Dubai Investments has asked for."));
kids.push(para("Accepted formats are PDF and images, each with a size limit. When you upload a trade licence, the system may automatically read the licence number and details to help you — please check them.", { size: 18, color: g.colors.GREY }));

kids.push(h1("5. Validation"));
kids.push(para("Your entries are checked when you click Next on a tab (for that tab's fields) and again when you click Proceed (for the whole form). If anything is missing or incorrect, a clear message appears in a toast — when there are several problems they are listed as bullet points so you can fix them one by one."));

kids.push(h1("6. Step 4 — Proceed & Sign the Declaration"));
kids.push(num("When every tab is complete, click Proceed. The Declaration opens."));
kids.push(num("Read the declaration, then type your full name and draw your signature in the box using your mouse (or your finger on a touch screen)."));
kids.push(num("Submit the declaration. A friendly animated \"Thank You\" confirmation card is shown, and you receive a branded \"Supplier Registration Completed\" e-mail."));
kids.push(para("A signed declaration PDF is generated and stored with your documents. It contains your Business / Organisation, Address and Contact Information together with the declaration text and your signature. It does not include Site / Business Unit or Payment / Banking details.", { size: 18, color: g.colors.GREY }));

kids.push(h1("7. Privacy & Security"));
kids.push(bullet("Your session is protected by the OTP and a secure token; do not share your link or code with anyone."));
kids.push(bullet("There is no public self-registration and you have no password — access is only via the e-mailed link and the OTP. Dubai Investments will never ask you for a password."));
kids.push(bullet("Partner Connect never exposes internal systems directly; your entries reach the portal through a dedicated, controlled interface."));
kids.push(bullet("For your security, avoid using a shared or public computer, and close the browser when you finish. If your link or session expires, a clear session-expired screen is shown and you should request a new link."));

kids.push(h1("8. Troubleshooting"));
kids.push(table(["Issue", "What to do"],
 [["The link doesn't open / says expired","Request a new link from your Dubai Investments contact (links are time-limited and there is no self-registration)."],
  ["I didn't receive the OTP e-mail","Check spam; click Resend code (the progress indicator confirms it is being re-sent); confirm the e-mail on file is correct."],
  ["I entered the code but it's rejected","Make sure you entered the full code and that it hasn't expired. Request a new code if needed."],
  ["My TRN or TIN is rejected","The TRN must be exactly 15 digits and the TIN exactly 10 digits; check each VAT Group row too."],
  ["I can't move to the next tab","Upload the Trade Licence and complete that tab's mandatory (*) fields — validation runs on Next as well as on Proceed."],
  ["A document won't upload","Use PDF or an image; reduce the file size; try a clearer scan."],
  ["I can't Proceed","Fix every item listed in the validation toast, upload required documents (Trade Licence always; VAT Certificate if VAT registered), then sign the declaration (typed name and drawn signature)."],
  ["I need to change something after submitting","Contact Dubai Investments; they will issue a new link."]],
 [3200, 6300]));

write(buildDoc({ title: "Supplier Portal — Self-Service Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Self-Service Supplier Portal Guide.docx");
