# QA Regression & Security Report — Customer Portal & Self-Service

Scope: Customer Portal (internal admin) and Customer Self-Service (DMZ Partner Connect + Web).
Method: static code audit (two parallel review passes), manual verification of each finding against
the code, targeted fixes, unit tests, and full build + test runs. Supplier/Landlord/Broker out of scope
except for parity comparison.

Result: **build 0 errors, 42/42 unit tests passing.** All confirmed findings fixed; one field-issue is
under live diagnosis (see §Data Integrity).

---

## 1. Security (PT / vulnerability / XSS / SQL injection)

| # | Severity | Finding | Fix |
|---|----------|---------|-----|
| S1 | HIGH | **Mass assignment on self-service save.** `ApplyValues` wrote *every* client-supplied FieldKey via reflection, bypassing `CustomerControl=Readonly`/visibility and letting a crafted payload set workflow/scaffolding fields (`Status`, `IsDraft`, `BaanRef`, `CustomerId`, `CustomerCode`, `CreatedBy`). | Only fields that are **visible + customer-editable** in the config are applied; scaffolding fields (not in the field config) are ignored. |
| S2 | HIGH | **Forged "Signed Declaration" upload.** The self-service upload validated extension/size but not the document *type*, and uploads replace by type — a crafted request could overwrite the server-generated signed PDF (the supplier had this exact bug, already fixed there). | Self-service upload rejects the reserved `Signed Declaration` type and any type not in the active KYC list. |
| S3 | LOW-MED | **Self-XSS via stored content-type.** Upload stored the client-supplied MIME; a `.png` containing HTML with `Content-Type: text/html` could be re-inlined as a same-origin blob in the DMZ preview. | Stored content-type is now derived from the validated extension (pdf/jpeg/png), ignoring the client MIME. |
| — | — | **SQL injection** | None. All data access is EF Core LINQ; the only raw SQL is in migrations using constant group names (no user input). |
| — | — | **Stored XSS (admin preview)** | Not exploitable — `ServeDocument` serves only pdf/image/plain inline; HTML/SVG/other are forced to `application/octet-stream` attachment, plus global `X-Content-Type-Options: nosniff`. |
| — | — | **IDOR on documents** | Safe — document endpoints resolve the owning customer and enforce `CanAccessCustomerAsync` (business-unit scope); self-service scopes every id to the session's customer. |
| — | — | **OTP brute force** | Safe — hashed OTP, per-link attempt cap + send cap, expiry, CSPRNG. |
| — | — | **Email-template injection** | Safe — all user data HTML-encoded before templating. |
| — | — | **DMZ auth / CSRF / open-redirect** | Safe — `__Host-` HttpOnly/Secure/SameSite=Strict cookie, same-origin guard + `LocalRedirect` on sign-in, API key compared with `FixedTimeEquals`. |

## 2. Functional (end-to-end)

| # | Severity | Finding | Fix |
|---|----------|---------|-----|
| F1 | CRITICAL | **"Mark completed" ran no validation.** The validation block in `SaveAndSetStatus` was gated on the retired `For Review` status, while the only caller passes `Complete` — so a record could be completed with missing/invalid data and a completion email sent. | Guard changed to `Complete`; required fields, date logic, formats and required documents are validated before completion. |
| F2 | MEDIUM | **Submit did no server-side validation.** Validation lived only in the Blazor clients; a replayed/malformed API call could complete an invalid record. | `/submit` re-validates required fields, required docs, and issue/expiry date logic server-side, and requires a signature. |
| F3 | LOW | **Expiry-before-issue only enforced for Trade License.** Passport / Dual Passport / Visa / Govt-ID / Emirates-ID pairs had no expiry≥issue check. | Generalised the rule to all issue/expiry date pairs (server-side at submit). |

## 3. Data quality & validation

- Required-field, email, TIN (10 digits), TRN (15 digits), maxlength, calendar-only dates, and
  conditional (depends-on) field clearing verified in both forms; server-side re-validation added at submit.
- Retired the **For Review** and **Returned** statuses; workflow is now Not Submitted → Awaiting Customer
  Response → Completed. Migration realigns legacy rows.

## 4. Data integrity

- `CustomerService.SaveAsync` now **returns whether a row was actually persisted**; the self-service
  gateway surfaces a real failure instead of a false "success", and the DMZ form fails loudly (and blocks
  Submit) if a save does not persist. This closes a class of silent data loss.
- Contact (1:1) persistence verified: keys/FKs preserved across `SetValues`, row created if absent.

## 5. Documents (upload / download / view)

- Upload (type + extension + size validated), list, inline preview (per-card loading spinner), Open,
  Download with live 0→100% progress, **All images as PDF**, and **Download all (zip)** named
  `{CustomerNumber}_{CustomerName}.zip` — all verified; the customer viewer is at parity with supplier.

## 6. Broken links / routes

- All customer hrefs/`NavigateTo` and document endpoints resolve to existing routes; the emailed link
  `/customer/self-service/{token}` matches the DMZ + Web routes. No broken links found.

## 7. Unit testing

- Added `CustomerWorkflowTests` (7 tests): active status set, retired-status exclusion, Nationality 2 LOV
  parity, UAE-first phone codes, unknown-lookup fallback, blank-clears-field, account/contact dictionary
  round-trip. Existing `CustomerFieldAccessorTests` retained. **Suite: 42 passing.**

## Open item — "submitted but not updated"

Symptom: a DMZ submission reports success but the internal record shows old data and status still
"Awaiting Customer Response". Code review of the save path found it correct, so **server-side logging was
added** (`CustomerSelfServiceSave`) that prints the target customer id and the field values in/out of each
save. Most likely causes to confirm from the log: (a) the self-service link maps to a *different* customer
record than the one being viewed (duplicate name), or (b) the internal edit form was open with stale data
and re-saved over the submission. The new "real failure" propagation + fail-loud client will also convert
any genuine non-persisting save into a visible error rather than a false success.
