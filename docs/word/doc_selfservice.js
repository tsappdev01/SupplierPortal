const g = require("./docgen.js");
const { para, h1, h2, bullet, num, table, titlePage, tocPage, buildDoc, write } = g;
const today = new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "long", year: "numeric" });
const kids = [];

kids.push(...titlePage("Supplier Portal", "Self-Service Supplier Portal Guide", [
  ["Document", "Self-Service Supplier Portal Guide"],
  ["Audience", "Suppliers (external) completing their information"],
  ["Version", "1.0"],
  ["Date", today],
]));
kids.push(...tocPage());

kids.push(h1("1. Overview"));
kids.push(para("The Supplier Self-Service portal lets you review and complete your company's information securely, directly from a link sent to you by Dubai Investments. You do not need a user account. Access is protected by a one-time passcode (OTP) sent to your registered e-mail, and the link is valid for a limited time. Your information is used to keep your supplier record accurate and UAE e-invoicing compliant."));
kids.push(para("You will need: the e-mail invitation containing your secure link, access to the e-mail inbox on file, and your company documents (trade licence, VAT certificate, bank confirmation letter).", { size: 18, color: g.colors.GREY }));

kids.push(h1("2. Step 1 — Open Your Secure Link"));
kids.push(num("Open the invitation e-mail from Dubai Investments (Partner Connect) and click the secure link, or paste it into your browser."));
kids.push(num("The link is personal to your company and time-limited. If it has expired, use the 'Resend' option or contact your Dubai Investments contact for a new link."));

kids.push(h1("3. Step 2 — Verify Your Identity (OTP)"));
kids.push(num("On the Verify Your Identity screen, a 6-digit verification code is sent to the e-mail address on file for your company."));
kids.push(num("Open your inbox, copy the 6-digit code (it looks like 310075) and enter it on the screen. The code expires in 10 minutes."));
kids.push(num("Click Verify Code. If you did not receive it, click Resend code (up to five times)."));
kids.push(para("For your security: you have a limited number of attempts and sends. If the code doesn't arrive, check your spam folder and confirm the e-mail on file with your Dubai Investments contact.", { size: 18, color: g.colors.GREY }));

kids.push(h1("4. Step 3 — Complete Your Information"));
kids.push(para("After verification, the supplier form opens. Work through the steps and complete the fields shown to you. Fields marked with a red asterisk (*) are mandatory."));
kids.push(bullet("Business / Organisation — legal name, tax organisation type, trade licence details, VAT registration and TRN."));
kids.push(bullet("Address — your address as per the trade licence (and the VAT-certificate address if different)."));
kids.push(bullet("Contact — the person Dubai Investments should deal with, including e-mail and phone/mobile."));
kids.push(bullet("Payment / Banking — bank, branch, account name/number, IBAN and SWIFT/BIC."));
kids.push(para("Use the help icon next to a field for an explanation of what is expected. Some fields are read-only — these are managed by Dubai Investments. Your entries are validated (for example, e-mail format) before you can submit.", { size: 18, color: g.colors.GREY }));

kids.push(h1("5. Step 4 — Upload Documents"));
kids.push(num("In the documents section, upload the requested files: Trade Licence, VAT Certificate, Bank Confirmation Letter, and any other requested document."));
kids.push(num("Accepted formats are PDF and images. Each file has a size limit; if a file is too large, compress it or upload a clearer scan."));
kids.push(num("When you upload a trade licence, the system may automatically read the licence number and details to help you — please check them."));

kids.push(h1("6. Step 5 — Read & Sign the Declaration"));
kids.push(num("Before submitting, read the declaration. It confirms that the information you provided is true, correct and complete."));
kids.push(num("Type your full name as your digital signature, then draw your signature in the box using your mouse (or your finger on a touch screen). Use Clear to redo it."));
kids.push(num("Both the typed name and the drawn signature are required. A signed declaration PDF — containing your details and signature — is generated and stored with your documents."));

kids.push(h1("7. Step 6 — Submit"));
kids.push(num("Click Submit to send your information for review. You will see a confirmation that your response has been received."));
kids.push(num("After you submit, your secure link is closed — you cannot re-open or re-submit the form. If a change is needed, Dubai Investments will send you a new link."));
kids.push(num("A confirmation e-mail is sent to you, and your Dubai Investments contact is notified to review your submission."));

kids.push(h1("8. Privacy & Security"));
kids.push(bullet("Your session is protected by the OTP and a secure token; do not share your link or code with anyone."));
kids.push(bullet("For your security, avoid using a shared or public computer; close the browser when you finish."));
kids.push(bullet("Dubai Investments will never ask you for your password — you don't have one; access is only via the e-mailed link and OTP."));

kids.push(h1("9. Troubleshooting"));
kids.push(table(["Issue", "What to do"],
 [["The link doesn't open / says expired","Request a new link from your Dubai Investments contact (links are time-limited)."],
  ["I didn't receive the OTP e-mail","Check spam; click Resend code; confirm the e-mail on file is correct."],
  ["I entered the code but it's rejected","Make sure you entered all 6 digits and that the code hasn't expired (10 minutes). Request a new code if needed."],
  ["A document won't upload","Use PDF or an image; reduce the file size; try a clearer scan."],
  ["I can't submit","Complete all mandatory (*) fields, upload required documents, and sign the declaration (typed name and drawn signature)."],
  ["I need to change something after submitting","Contact Dubai Investments; they will issue a new link."]],
 [3200, 6300]));

write(buildDoc({ title: "Supplier Portal — Self-Service Guide", children: kids }), "/home/user/SupplierPortal/docs/word/Supplier Portal - Self-Service Supplier Portal Guide.docx");
