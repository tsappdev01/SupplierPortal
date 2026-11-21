using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using UglyToad.PdfPig;

namespace SupplierPortal.Web.Services;

/// <summary>Outcome of a document upload: whether it saved, any error, and (for VAT / Trade
/// License PDFs) numbers read from the document - the TRN or Trade License number in
/// <c>ExtractedNumber</c> and, for a Trade License, the D-U-N-S number in <c>DunsNumber</c>.</summary>
public record DocumentUploadResult(bool Success, string? Error, string? ExtractedNumber, string? DunsNumber);

/// <summary>A no-save Trade License extraction preview: the Claude-vision result and the regex-fallback
/// result side by side, plus which source production would use and how the fallback text was obtained.</summary>
public record LicensePreview(
    bool VisionEnabled,
    bool OcrEnabled,
    ParsedLicense? Vision,
    ParsedLicense? Fallback,
    string FallbackTextSource,
    int FallbackTextChars,
    string ChosenSource,
    string? VisionRaw);

/// <summary>Stores supplier-uploaded documents (Trade License, VAT Certification, Other) as
/// physical files under the app's content root - not wwwroot, so they are never directly
/// web-servable - with only their metadata in the database.</summary>
public class SupplierDocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService,
    IDocumentOcrService ocr, IHttpClientFactory httpClientFactory, LicenseQrService qrService,
    SupplierPortal.Web.Services.Storage.IFileStorageService fileStore, ITradeLicenseReader licenseReader,
    IIdentityDocumentReader identityReader, IDocumentFieldReader docReader)
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // UAE Tax Registration Numbers are always 15 digits. Matched either next to a recognizable
    // label, or (fallback) as any standalone 15-digit run in the document - VAT certificates
    // consistently print the TRN as the only long digit run on the page.
    private static readonly Regex TrnLabeled = new(
        @"(?:TRN|Tax\s*Registration\s*Number)\D{0,10}(\d[\d\s]{13,25}\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrnBare = new(@"\b(\d{15})\b", RegexOptions.Compiled);

    // Trade-license fields (number, D-U-N-S, legal type, partners/members) are parsed by the reusable
    // TradeLicenseParser so there is a single source of truth for that bilingual DED layout.

    private string DocumentsDirectory(int supplierId) =>
        Path.Combine(env.ContentRootPath, "doc", "supplier", supplierId.ToString());

    /// <summary>Runs the Trade License extraction on an uploaded file WITHOUT saving anything, returning
    /// both the Claude-vision result and the regex-fallback result so an administrator can preview and
    /// compare what each reader extracts. Mirrors the production selection logic (vision preferred when it
    /// returns data, else the PDF text layer / OCR + regex parser), but touches no supplier record.</summary>
    public async Task<LicensePreview> PreviewLicenseAsync(byte[] bytes, string? contentType, string fileName)
    {
        ParsedLicense? vision = null;
        string? visionRaw = null;
        if (licenseReader.IsEnabled)
        {
            try { (vision, visionRaw) = await licenseReader.ReadWithRawAsync(bytes, contentType); }
            catch { /* preview is best-effort; a reader failure just leaves the vision column empty */ }
        }

        ParsedLicense? fallback = null;
        var textSource = "none";
        var textChars = 0;
        var ext = Path.GetExtension(fileName);
        var tmp = Path.Combine(Path.GetTempPath(), $"licpreview_{Guid.NewGuid():N}{ext}");
        try
        {
            await File.WriteAllBytesAsync(tmp, bytes);
            var text = TryReadPdfText(tmp);
            if (!string.IsNullOrWhiteSpace(text)) { textSource = "PDF text layer"; }
            else if (ocr.IsEnabled)
            {
                text = await ocr.ExtractTextAsync(tmp);
                if (!string.IsNullOrWhiteSpace(text)) { textSource = "OCR"; }
            }
            textChars = text?.Length ?? 0;
            if (!string.IsNullOrWhiteSpace(text)) { fallback = TradeLicenseParser.Parse(text); }
        }
        catch { /* best-effort */ }
        finally { try { File.Delete(tmp); } catch { /* temp cleanup */ } }

        var chosen = vision is not null && LicenseHasData(vision) ? "Claude vision"
            : fallback is not null ? "DED parser (text/OCR)" : "none";
        return new LicensePreview(licenseReader.IsEnabled, ocr.IsEnabled, vision, fallback, textSource, textChars, chosen, visionRaw);
    }

    /// <summary>Re-runs extraction on the Trade License already on file for a supplier and saves the
    /// captured fields + parties - for licences uploaded before auto-read existed, or to refresh with an
    /// improved reader. Returns success, an error message when nothing could be read, and whether the
    /// vision reader is configured (so the caller can explain a thin result).</summary>
    public async Task<(bool Success, string? Error, bool VisionEnabled)> ReExtractLicenseAsync(int supplierId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId && d.DocumentType == SupplierDocumentType.TradeLicense)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();
        if (doc is null)
        {
            return (false, "No Trade License document is on file for this supplier.", licenseReader.IsEnabled);
        }

        byte[]? bytes = doc.Content;
        if ((bytes is null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            try { bytes = await fileStore.ReadAsync(doc.BlobKey); } catch { bytes = null; }
        }
        if (bytes is null || bytes.Length == 0)
        {
            return (false, "The stored Trade License file could not be read.", licenseReader.IsEnabled);
        }

        var (license, readSource) = await ExtractChosenLicenseAsync(bytes, doc.ContentType, doc.FileName);
        if (license is null || !LicenseHasData(license))
        {
            var why = licenseReader.IsEnabled
                ? "Extraction did not find readable data in the stored file."
                : "Extraction found nothing - the Claude-vision reader is not configured, so only the text parser ran (scanned licences need vision).";
            return (false, why, licenseReader.IsEnabled);
        }

        try { await ApplyLicenseToSupplierAsync(db, supplierId, license, readSource); }
        catch (Exception ex) { return (false, $"Extraction succeeded but saving failed: {ex.Message}", licenseReader.IsEnabled); }

        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId, "Re-ran Trade License extraction");
        return (true, null, licenseReader.IsEnabled);
    }

    /// <summary>Runs the production selection (Claude vision when it returns data, else the PDF text layer
    /// or OCR + regex parser) over a file's bytes and returns the chosen licence and its read source.</summary>
    private async Task<(ParsedLicense? License, string? ReadSource)> ExtractChosenLicenseAsync(byte[] bytes, string? contentType, string fileName)
    {
        if (licenseReader.IsEnabled)
        {
            try
            {
                var vision = await licenseReader.ReadAsync(bytes, contentType);
                if (vision is not null && LicenseHasData(vision)) { return (vision, "Claude vision"); }
            }
            catch { /* fall through to the text parser */ }
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"licx_{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
        try
        {
            await File.WriteAllBytesAsync(tmp, bytes);
            var text = TryReadPdfText(tmp);
            if (string.IsNullOrWhiteSpace(text) && ocr.IsEnabled) { text = await ocr.ExtractTextAsync(tmp); }
            if (!string.IsNullOrWhiteSpace(text)) { return (TradeLicenseParser.Parse(text), "DED parser"); }
        }
        catch { /* best-effort */ }
        finally { try { File.Delete(tmp); } catch { /* temp cleanup */ } }

        return (null, null);
    }

    /// <summary>Writes a parsed licence's fields and parties onto the supplier record (the non-destructive
    /// Tl* capture, plus folding D-U-N-S and legal type into the editable fields). Shared by the upload
    /// path and the re-extract action. Caller wraps this in its own try/catch - enrichment is optional.</summary>
    private static async Task ApplyLicenseToSupplierAsync(ApplicationDbContext db, int supplierId, ParsedLicense license, string? readSource)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null) { return; }

        // Keep the existing behaviour of folding D-U-N-S and legal type into the editable fields.
        if (!string.IsNullOrWhiteSpace(license.DunsNumber)) { supplier.DunsNumber = license.DunsNumber; }
        if (!string.IsNullOrWhiteSpace(license.LegalType)) { supplier.LicenseType = license.LegalType; }

        // Dedicated, non-destructive auto-read capture (shown read-only under the document).
        supplier.TlLicenseNumber = license.LicenseNumber;
        supplier.TlMainLicenseNumber = license.MainLicenseNumber;
        supplier.TlLegalName = license.LegalName ?? license.BusinessName;
        supplier.TlTradeName = license.TradeName;
        supplier.TlLegalType = license.LegalType;
        supplier.TlLicenseCategory = license.LicenseCategory;
        supplier.TlIssuingAuthority = license.IssuingAuthority;
        supplier.TlLicenseType = license.LicenseKind;
        supplier.TlIssueDate = ParseLicenseDate(license.IssueDate);
        supplier.TlExpiryDate = ParseLicenseDate(license.ExpiryDate);
        supplier.TlRegisterNumber = license.RegisterNumber;
        supplier.TlDcciNumber = license.DcciNumber;
        supplier.TlDunsNumber = license.DunsNumber;
        supplier.TlPaidCapital = ParseCapital(license.PaidCapital);
        supplier.TlCapitalCurrency = license.CapitalCurrency;
        supplier.TlActivities = license.Activities.Count > 0
            ? string.Join(" | ", license.Activities.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)))
            : null;
        // Mirror the activities into the editable KYS "Licensed business activities" field when it is still
        // blank, so the auto-read value is pre-filled there too (newline-separated for the textarea).
        if (string.IsNullOrWhiteSpace(supplier.KysLicensedActivities) && license.Activities.Count > 0)
        {
            var kysActivities = string.Join("\n", license.Activities.Select(a => a.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
            if (kysActivities.Length > 500) { kysActivities = kysActivities[..500]; }
            if (!string.IsNullOrWhiteSpace(kysActivities)) { supplier.KysLicensedActivities = kysActivities; }
        }
        supplier.TlPoBox = license.PoBox;
        supplier.TlParcelId = license.ParcelId;
        supplier.TlPremises = license.Premises;
        supplier.TlEmail = license.Email;
        supplier.TlPhone = license.Phone;
        supplier.TlMobile = license.Mobile;
        supplier.TlAddress = license.Address;
        supplier.TlReadAt = DateTime.UtcNow;
        supplier.TlReadSource = readSource;

        // Replace the parties annex (owners/partners/managers) with the freshly read set.
        if (license.Members.Count > 0)
        {
            var existingPartners = await db.SupplierPartners.Where(p => p.SupplierId == supplierId).ToListAsync();
            db.SupplierPartners.RemoveRange(existingPartners);
            foreach (var partner in license.Members)
            {
                db.SupplierPartners.Add(new SupplierPartner
                {
                    SupplierId = supplierId,
                    Name = partner.Name,
                    Nationality = partner.Nationality,
                    SerialNo = partner.PersonNo,
                    Share = partner.Share,
                    Role = partner.Role
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Writes the read Emirates ID / Passport fields onto the supplier record (non-destructive
    /// capture shown read-only under the ID document), and fills the editable KYS "Emirates ID / Passport
    /// No." + expiry when those are still blank - preferring the Emirates ID, else the passport.</summary>
    private static async Task ApplyIdentityToSupplierAsync(ApplicationDbContext db, int supplierId, ParsedIdentityDocument id, string? readSource)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null) { return; }

        supplier.EmiratesIdNumber = id.EmiratesIdNumber;
        supplier.EmiratesIdExpiryDate = ParseLicenseDate(id.EmiratesIdExpiry);
        supplier.PassportNumber = id.PassportNumber;
        supplier.PassportExpiryDate = ParseLicenseDate(id.PassportExpiry);
        supplier.IdDocReadAt = DateTime.UtcNow;
        supplier.IdDocReadSource = readSource;

        // Convenience: fill the editable KYS ID No / expiry when still empty (Emirates ID first, else passport).
        var number = id.EmiratesIdNumber ?? id.PassportNumber;
        var expiry = ParseLicenseDate(id.EmiratesIdExpiry) ?? ParseLicenseDate(id.PassportExpiry);
        if (string.IsNullOrWhiteSpace(supplier.KysIdNumber) && !string.IsNullOrWhiteSpace(number)) { supplier.KysIdNumber = number; }
        if (supplier.KysIdExpiryDate is null && expiry is not null) { supplier.KysIdExpiryDate = expiry; }

        await db.SaveChangesAsync();
    }

    /// <summary>Re-runs identity extraction on the Emirates ID / Passport already on file and saves it.</summary>
    public async Task<(bool Success, string? Error, bool VisionEnabled)> ReExtractIdentityAsync(int supplierId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId && d.DocumentType == SupplierDocumentType.EmiratesIdOrPassport)
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();
        if (doc is null) { return (false, "No Emirates ID / Passport document is on file for this supplier.", identityReader.IsEnabled); }
        if (!identityReader.IsEnabled)
        {
            return (false, "The Claude-vision reader is not configured, so identity documents cannot be read.", false);
        }

        byte[]? bytes = doc.Content;
        if ((bytes is null || bytes.Length == 0) && !string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            try { bytes = await fileStore.ReadAsync(doc.BlobKey); } catch { bytes = null; }
        }
        if (bytes is null || bytes.Length == 0) { return (false, "The stored identity file could not be read.", true); }

        ParsedIdentityDocument? id = null;
        try { id = await identityReader.ReadAsync(bytes, doc.ContentType); } catch { /* best-effort */ }
        if (id is null || !id.HasData) { return (false, "Extraction did not find readable identity data in the stored file.", true); }

        try { await ApplyIdentityToSupplierAsync(db, supplierId, id, "Claude vision"); }
        catch (Exception ex) { return (false, $"Extraction succeeded but saving failed: {ex.Message}", true); }

        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId, "Re-ran Emirates ID / Passport extraction");
        return (true, null, true);
    }

    // ---- KYS document extraction prompts (flat JSON out; prefer the English value on bilingual docs) ----
    private const string VatPrompt = """
Extract from this UAE Federal Tax Authority VAT Registration Certificate. It is bilingual Arabic/English.
Return ONLY a JSON object with these keys, using null when a value is absent. Keep the registered address
as a single full line exactly as printed:
{ "taxRegistrationNumber": string|null, "legalNameEnglish": string|null, "legalNameArabic": string|null,
  "registeredAddress": string|null, "effectiveRegistrationDate": "DD/MM/YYYY"|null }
""";

    private const string CertIncorpPrompt = """
Extract from this Certificate of Incorporation / commercial registration certificate. It may be bilingual;
prefer the ENGLISH value. Return ONLY a JSON object with these keys, using null when absent:
{ "companyName": string|null, "registrationNumber": string|null, "dateOfIncorporation": "DD/MM/YYYY"|null }
""";

    private const string BankLetterPrompt = """
Extract from this written bank account confirmation / IBAN letter. Return ONLY a JSON object with these keys,
using null when absent. Keep the IBAN and account number exactly as printed (no spaces):
{ "accountHolderName": string|null, "bankName": string|null, "branchName": string|null,
  "accountNumber": string|null, "iban": string|null, "swiftBic": string|null }
""";

    private const string ProofOfAddressPrompt = """
Extract from this proof of address (UAE Ejari tenancy contract or a utility bill). Prefer the ENGLISH value.
Return ONLY a JSON object, using null when a value is absent. Dates as DD/MM/YYYY:
{ "address": string|null, "tenantName": string|null, "landlordName": string|null,
  "ejariOrContractNumber": string|null, "contractStartDate": "DD/MM/YYYY"|null,
  "contractEndDate": "DD/MM/YYYY"|null, "annualRent": string|null, "propertyUsage": string|null,
  "plotOrBuilding": string|null }
("address" is a single full address line.)
""";

    private const string IdentityPrompt = """
Extract from this identity document (a UAE Emirates ID or a passport). If it is a passport, LOCATE the Machine
Readable Zone (MRZ) — the two lines of monospaced characters at the bottom filled with '<' — read both lines
exactly, and PREFER the MRZ-derived values (passport number, nationality/country code, date of birth, sex,
date of expiry) over the printed text when they differ. In the MRZ, dates are YYMMDD; convert them to
DD/MM/YYYY. Return ONLY a JSON object, using null when a value is absent. Dates as DD/MM/YYYY:
{ "documentKind": "Emirates ID"|"Passport"|null, "idOrPassportNumber": string|null, "fullName": string|null,
  "nationality": string|null, "countryCode": string|null, "dateOfBirth": "DD/MM/YYYY"|null, "sex": "M"|"F"|null,
  "placeOfBirth": string|null, "dateOfIssue": "DD/MM/YYYY"|null, "placeOfIssue": string|null,
  "dateOfExpiry": "DD/MM/YYYY"|null, "issuingAuthority": string|null, "mrzLine1": string|null,
  "mrzLine2": string|null }
""";

    /// <summary>Runs Claude extraction over every uploaded KYS document and STORES the key attributes it
    /// reads onto the supplier's KYS fields (fill-only: an existing value is never overwritten). Internal
    /// reviewers only. Returns a summary of what was read and stored. Never throws.</summary>
    public async Task<KysExtractionResult> ExtractAndApplyKysAsync(int supplierId, string actorUsername)
    {
        var result = new KysExtractionResult { Enabled = docReader.IsEnabled };
        if (!docReader.IsEnabled && !licenseReader.IsEnabled && !identityReader.IsEnabled)
        {
            result.Error = "The Claude reader is not configured (no Claude:ApiKey).";
            return result;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null) { result.Error = "Supplier not found."; return result; }

        var docs = await db.SupplierDocuments.Where(d => d.SupplierId == supplierId).ToListAsync();

        async Task<byte[]?> BytesAsync(SupplierDocument d)
        {
            var b = d.Content;
            if ((b is null || b.Length == 0) && !string.IsNullOrWhiteSpace(d.BlobKey))
            {
                try { b = await fileStore.ReadAsync(d.BlobKey); } catch { b = null; }
            }
            return b is { Length: > 0 } ? b : null;
        }

        SupplierDocument? Latest(SupplierDocumentType t) =>
            docs.Where(d => d.DocumentType == t).OrderByDescending(d => d.Id).FirstOrDefault();

        // Per-field extraction log rows (field + value + source document/file), saved to SupplierKysExtractionLogs.
        var extractionLogs = new List<SupplierKysExtractionLog>();
        static string? Trunc(string? s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);

        void LogField(SupplierDocument? doc, string docLabel, string fieldName, string? value, bool stored)
        {
            if (string.IsNullOrWhiteSpace(value)) { return; }
            extractionLogs.Add(new SupplierKysExtractionLog
            {
                SupplierId = supplierId,
                DocumentId = doc?.Id,
                DocumentType = doc?.DocumentType.ToString() ?? "",
                DocumentLabel = docLabel,
                FileName = doc?.FileName,
                FieldName = fieldName,
                FieldValue = Trunc(value.Trim(), 2000),
                Outcome = stored ? "Stored" : "Skipped (already filled)",
                ExtractedAt = DateTime.UtcNow,
                ExtractedBy = actorUsername,
            });
        }

        // Logs a value read from a document that has no supplier field to store into (reference / audit only).
        void LogRead(SupplierDocument? doc, string docLabel, string fieldName, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return; }
            extractionLogs.Add(new SupplierKysExtractionLog
            {
                SupplierId = supplierId,
                DocumentId = doc?.Id,
                DocumentType = doc?.DocumentType.ToString() ?? "",
                DocumentLabel = docLabel,
                FileName = doc?.FileName,
                FieldName = fieldName,
                FieldValue = Trunc(value.Trim(), 2000),
                Outcome = "Read (reference only)",
                ExtractedAt = DateTime.UtcNow,
                ExtractedBy = actorUsername,
            });
        }

        // Stores a value onto the supplier (fill-only: never overwrites) and logs the field + source document.
        void SetStr(SupplierDocument? doc, string docLabel, string label, Func<string?> get, Action<string> set, string? value)
        {
            var v = value?.Trim();
            if (string.IsNullOrWhiteSpace(v)) { return; }
            var stored = string.IsNullOrWhiteSpace(get());
            if (stored)
            {
                set(v!);
                result.Stored.Add($"{label}: {v}");
            }
            LogField(doc, docLabel, label, v, stored);
        }

        async Task<Dictionary<string, string>?> ReadDocAsync(SupplierDocumentType t, string prompt, string label)
        {
            var doc = Latest(t);
            if (doc is null || !docReader.IsEnabled) { return null; }
            var bytes = await BytesAsync(doc);
            if (bytes is null) { return null; }
            Dictionary<string, string>? fields = null;
            try { (fields, _) = await docReader.ReadFieldsAsync(bytes, doc.ContentType, prompt); } catch { }
            if (fields is { Count: > 0 }) { result.DocsRead.Add(label); }
            return fields;
        }

        static string? V(Dictionary<string, string>? f, string key) =>
            f is not null && f.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        // VAT Certificate -> TRN
        var vatDoc = Latest(SupplierDocumentType.VatCertification);
        var vat = await ReadDocAsync(SupplierDocumentType.VatCertification, VatPrompt, "VAT Certificate");
        SetStr(vatDoc, "VAT Certificate", "TRN / VAT Registration Number", () => supplier.TaxRegistrationNumber, v => supplier.TaxRegistrationNumber = v, V(vat, "taxRegistrationNumber"));
        // The remaining VAT Certificate fields have no dedicated supplier column, so they are captured in the
        // extraction log for reference (visible in SupplierKysExtractionLogs).
        LogRead(vatDoc, "VAT Certificate", "Full English legal name", V(vat, "legalNameEnglish"));
        LogRead(vatDoc, "VAT Certificate", "Full Arabic legal name", V(vat, "legalNameArabic"));
        LogRead(vatDoc, "VAT Certificate", "Registered address", V(vat, "registeredAddress"));
        LogRead(vatDoc, "VAT Certificate", "Effective Registration Date", V(vat, "effectiveRegistrationDate"));

        // Certificate of Incorporation -> Date of Incorporation
        var incDoc = Latest(SupplierDocumentType.CertificateOfIncorporation);
        var inc = await ReadDocAsync(SupplierDocumentType.CertificateOfIncorporation, CertIncorpPrompt, "Certificate of Incorporation");
        var incDate = ParseLicenseDate(V(inc, "dateOfIncorporation"));
        if (incDate is not null)
        {
            var stored = supplier.DateOfIncorporation is null;
            if (stored)
            {
                supplier.DateOfIncorporation = incDate;
                result.Stored.Add($"Date of Incorporation: {incDate:dd/MM/yyyy}");
            }
            LogField(incDoc, "Certificate of Incorporation", "Date of Incorporation", incDate.Value.ToString("dd/MM/yyyy"), stored);
        }

        // Bank Confirmation Letter -> KYS Bank Account Details
        var bankDoc = Latest(SupplierDocumentType.BankConfirmationLetter);
        var bank = await ReadDocAsync(SupplierDocumentType.BankConfirmationLetter, BankLetterPrompt, "Bank Confirmation Letter");
        SetStr(bankDoc, "Bank Confirmation Letter", "Account Holder Name", () => supplier.KysAccountHolderName, v => supplier.KysAccountHolderName = v, V(bank, "accountHolderName"));
        SetStr(bankDoc, "Bank Confirmation Letter", "Bank Name", () => supplier.KysBankName, v => supplier.KysBankName = v, V(bank, "bankName"));
        SetStr(bankDoc, "Bank Confirmation Letter", "Bank Branch", () => supplier.KysBankBranch, v => supplier.KysBankBranch = v, V(bank, "branchName"));
        SetStr(bankDoc, "Bank Confirmation Letter", "Account Number", () => supplier.KysAccountNumber, v => supplier.KysAccountNumber = v, V(bank, "accountNumber"));
        SetStr(bankDoc, "Bank Confirmation Letter", "IBAN", () => supplier.KysIban, v => supplier.KysIban = v, V(bank, "iban"));
        SetStr(bankDoc, "Bank Confirmation Letter", "SWIFT / BIC", () => supplier.KysSwiftBic, v => supplier.KysSwiftBic = v, V(bank, "swiftBic"));

        // Proof of Address -> KYS actual operating address
        var addrDoc = Latest(SupplierDocumentType.ProofOfAddress);
        var addr = await ReadDocAsync(SupplierDocumentType.ProofOfAddress, ProofOfAddressPrompt, "Proof of Address");
        SetStr(addrDoc, "Proof of Address", "Actual Operating Address", () => supplier.KysActualOperatingAddress, v => supplier.KysActualOperatingAddress = v, V(addr, "address"));
        // Remaining tenancy / Ejari fields have no supplier column — captured in the extraction log.
        LogRead(addrDoc, "Proof of Address", "Tenant name", V(addr, "tenantName"));
        LogRead(addrDoc, "Proof of Address", "Landlord name", V(addr, "landlordName"));
        LogRead(addrDoc, "Proof of Address", "Ejari / Contract number", V(addr, "ejariOrContractNumber"));
        LogRead(addrDoc, "Proof of Address", "Contract start date", V(addr, "contractStartDate"));
        LogRead(addrDoc, "Proof of Address", "Contract end date", V(addr, "contractEndDate"));
        LogRead(addrDoc, "Proof of Address", "Annual rent", V(addr, "annualRent"));
        LogRead(addrDoc, "Proof of Address", "Property usage", V(addr, "propertyUsage"));
        LogRead(addrDoc, "Proof of Address", "Plot / Building", V(addr, "plotOrBuilding"));

        // Trade License (reuse the dedicated reader) -> licensed activities + signatory
        var tlDoc = Latest(SupplierDocumentType.TradeLicense);
        if (tlDoc is not null && licenseReader.IsEnabled)
        {
            var bytes = await BytesAsync(tlDoc);
            if (bytes is not null)
            {
                ParsedLicense? lic = null;
                try { lic = await licenseReader.ReadAsync(bytes, tlDoc.ContentType); } catch { }
                if (lic is not null)
                {
                    result.DocsRead.Add("Trade License");
                    if (lic.Activities.Count > 0)
                    {
                        var acts = string.Join("; ", lic.Activities.Select(a => a.Name));
                        if (acts.Length > 500) { acts = acts[..500]; }
                        SetStr(tlDoc, "Trade License", "Licensed business activities", () => supplier.KysLicensedActivities,
                            v => supplier.KysLicensedActivities = v, acts);
                    }
                    var signatory = lic.Members.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Name));
                    if (signatory is not null)
                    {
                        SetStr(tlDoc, "Trade License", "Name of signatory", () => supplier.KysSignatoryName, v => supplier.KysSignatoryName = v, signatory.Name);
                        SetStr(tlDoc, "Trade License", "Job title", () => supplier.KysRepPosition, v => supplier.KysRepPosition = v, signatory.Role);
                    }
                    // Capture every other field read from the Trade License in the extraction log (reference).
                    LogRead(tlDoc, "Trade License", "License Number", lic.LicenseNumber);
                    LogRead(tlDoc, "Trade License", "Main License Number", lic.MainLicenseNumber);
                    LogRead(tlDoc, "Trade License", "Legal Name", lic.LegalName ?? lic.BusinessName);
                    LogRead(tlDoc, "Trade License", "Trade Name", lic.TradeName);
                    LogRead(tlDoc, "Trade License", "Legal Type", lic.LegalType);
                    LogRead(tlDoc, "Trade License", "License Category", lic.LicenseCategory);
                    LogRead(tlDoc, "Trade License", "Issuing Authority", lic.IssuingAuthority);
                    LogRead(tlDoc, "Trade License", "License Type", lic.LicenseKind);
                    LogRead(tlDoc, "Trade License", "Issue Date", lic.IssueDate);
                    LogRead(tlDoc, "Trade License", "Expiry Date", lic.ExpiryDate);
                    LogRead(tlDoc, "Trade License", "Register Number", lic.RegisterNumber);
                    LogRead(tlDoc, "Trade License", "DCCI Number", lic.DcciNumber);
                    LogRead(tlDoc, "Trade License", "D&B D-U-N-S Number", lic.DunsNumber);
                    LogRead(tlDoc, "Trade License", "Paid-up Capital", lic.PaidCapital);
                    LogRead(tlDoc, "Trade License", "Capital Currency", lic.CapitalCurrency);
                    LogRead(tlDoc, "Trade License", "P.O. Box", lic.PoBox);
                    LogRead(tlDoc, "Trade License", "Parcel ID", lic.ParcelId);
                    LogRead(tlDoc, "Trade License", "Premises", lic.Premises);
                    LogRead(tlDoc, "Trade License", "Address", lic.Address);
                    LogRead(tlDoc, "Trade License", "Email", lic.Email);
                    LogRead(tlDoc, "Trade License", "Phone", lic.Phone);
                    LogRead(tlDoc, "Trade License", "Mobile", lic.Mobile);
                    if (lic.Activities.Count > 0)
                    {
                        LogRead(tlDoc, "Trade License", "All licensed activities", string.Join("; ", lic.Activities.Select(a => a.Name)));
                    }
                    if (lic.Members.Count > 0)
                    {
                        var partners = string.Join(" | ", lic.Members.Where(m => !string.IsNullOrWhiteSpace(m.Name))
                            .Select(m => string.Join(", ", new[] { m.Name, m.Role, m.Nationality, m.Share }
                                .Where(x => !string.IsNullOrWhiteSpace(x)))));
                        LogRead(tlDoc, "Trade License", "Partners / Managers", partners);
                    }
                }
            }
        }

        // Emirates ID / Passport -> KYS ID No + expiry (stored); all other identity fields logged for reference.
        var idDoc = Latest(SupplierDocumentType.EmiratesIdOrPassport);
        var idf = await ReadDocAsync(SupplierDocumentType.EmiratesIdOrPassport, IdentityPrompt, "Emirates ID / Passport");
        if (idf is { Count: > 0 })
        {
            SetStr(idDoc, "Emirates ID / Passport", "Emirates ID / Passport No.", () => supplier.KysIdNumber, v => supplier.KysIdNumber = v, V(idf, "idOrPassportNumber"));
            var idExpiry = ParseLicenseDate(V(idf, "dateOfExpiry"));
            if (idExpiry is not null)
            {
                var stored = supplier.KysIdExpiryDate is null;
                if (stored)
                {
                    supplier.KysIdExpiryDate = idExpiry;
                    result.Stored.Add($"ID expiry date: {idExpiry:dd/MM/yyyy}");
                }
                LogField(idDoc, "Emirates ID / Passport", "Expiry date", idExpiry.Value.ToString("dd/MM/yyyy"), stored);
            }
            LogRead(idDoc, "Emirates ID / Passport", "Document kind", V(idf, "documentKind"));
            LogRead(idDoc, "Emirates ID / Passport", "Full name", V(idf, "fullName"));
            LogRead(idDoc, "Emirates ID / Passport", "Nationality", V(idf, "nationality"));
            LogRead(idDoc, "Emirates ID / Passport", "Date of birth", V(idf, "dateOfBirth"));
            LogRead(idDoc, "Emirates ID / Passport", "Sex", V(idf, "sex"));
            LogRead(idDoc, "Emirates ID / Passport", "Place of birth", V(idf, "placeOfBirth"));
            LogRead(idDoc, "Emirates ID / Passport", "Date of issue", V(idf, "dateOfIssue"));
            LogRead(idDoc, "Emirates ID / Passport", "Place of issue", V(idf, "placeOfIssue"));
            LogRead(idDoc, "Emirates ID / Passport", "Issuing authority", V(idf, "issuingAuthority"));
            LogRead(idDoc, "Emirates ID / Passport", "Country code", V(idf, "countryCode"));
            LogRead(idDoc, "Emirates ID / Passport", "MRZ line 1", V(idf, "mrzLine1"));
            LogRead(idDoc, "Emirates ID / Passport", "MRZ line 2", V(idf, "mrzLine2"));
        }

        if (result.Stored.Count > 0 || extractionLogs.Count > 0)
        {
            // Save the per-field extraction log (field + value + source file) regardless of whether anything
            // was newly stored, so the reviewer can always see what each document yielded.
            if (extractionLogs.Count > 0)
            {
                db.SupplierKysExtractionLogs.AddRange(extractionLogs);
            }
            if (result.Stored.Count > 0)
            {
                supplier.UpdatedBy = actorUsername;
                supplier.UpdatedAt = DateTime.UtcNow;
            }
            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Stored.Clear();
                result.Error = $"Extraction succeeded but saving failed: {ex.Message}";
                return result;
            }
            if (result.Stored.Count > 0)
            {
                await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId,
                    $"Extracted & stored KYS data from documents ({string.Join(", ", result.DocsRead)})");
            }
        }
        return result;
    }

    /// <summary>The auto-read Emirates ID / Passport capture for the read-only panel under that document.</summary>
    public async Task<IdentityReadback> GetIdReadbackAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId);
        return s is null ? new IdentityReadback() : new IdentityReadback
        {
            EmiratesIdNumber = s.EmiratesIdNumber,
            EmiratesIdExpiryDate = s.EmiratesIdExpiryDate,
            PassportNumber = s.PassportNumber,
            PassportExpiryDate = s.PassportExpiryDate,
            ReadAt = s.IdDocReadAt,
            ReadSource = s.IdDocReadSource
        };
    }

    /// <summary>The auto-read Trade License capture (Tl* fields + parties) for the read-only panel shown
    /// under the Trade License document on the internal form.</summary>
    public async Task<TradeLicenseReadback> GetLicenseReadbackAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var parties = await db.SupplierPartners.AsNoTracking()
            .Where(p => p.SupplierId == supplierId)
            .OrderBy(p => p.Id)
            .ToListAsync();
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId);
        if (s is null)
        {
            return new TradeLicenseReadback { Parties = parties };
        }
        return new TradeLicenseReadback
        {
            LicenseNumber = s.TlLicenseNumber,
            MainLicenseNumber = s.TlMainLicenseNumber,
            LegalName = s.TlLegalName,
            TradeName = s.TlTradeName,
            LegalType = s.TlLegalType,
            LicenseCategory = s.TlLicenseCategory,
            IssuingAuthority = s.TlIssuingAuthority,
            LicenseType = s.TlLicenseType,
            IssueDate = s.TlIssueDate,
            ExpiryDate = s.TlExpiryDate,
            RegisterNumber = s.TlRegisterNumber,
            DcciNumber = s.TlDcciNumber,
            DunsNumber = s.TlDunsNumber,
            PaidCapital = s.TlPaidCapital,
            CapitalCurrency = s.TlCapitalCurrency,
            Activities = s.TlActivities,
            PoBox = s.TlPoBox,
            ParcelId = s.TlParcelId,
            Premises = s.TlPremises,
            Email = s.TlEmail,
            Phone = s.TlPhone,
            Mobile = s.TlMobile,
            Address = s.TlAddress,
            ReadAt = s.TlReadAt,
            ReadSource = s.TlReadSource,
            Parties = parties
        };
    }

    public async Task<List<SupplierDocument>> GetDocumentsAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Project without the Content bytes: the checklist only needs metadata, and this keeps large
        // database-stored files out of the form load (and out of the DMZ self-service bundle).
        return await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.UploadedAt)
            .Select(d => new SupplierDocument
            {
                Id = d.Id,
                SupplierId = d.SupplierId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                StoredFileName = d.StoredFileName,
                BlobKey = d.BlobKey,
                ContentType = d.ContentType,
                FileSizeBytes = d.FileSizeBytes,
                UploadedAt = d.UploadedAt,
                UploadedBy = d.UploadedBy,
            })
            .ToListAsync();
    }

    /// <summary>Blazor upload path (interactive <c>InputFile</c>).</summary>
    public async Task<DocumentUploadResult> UploadAsync(
        int supplierId, SupplierDocumentType documentType, IBrowserFile file, string uploadedBy)
    {
        if (file.Size > MaxFileSizeBytes)
        {
            return new DocumentUploadResult(false, "File exceeds the 10 MB size limit.", null, null);
        }

        await using var source = file.OpenReadStream(MaxFileSizeBytes);
        return await UploadFromStreamAsync(
            supplierId, documentType, source, file.Name, file.ContentType, file.Size, uploadedBy);
    }

    /// <summary>Stream-based upload path, shared by the Blazor upload and the REST API (which receives an
    /// <c>IFormFile</c>). Keeps a single implementation of storage, single-slot replacement, number
    /// extraction and auditing regardless of the transport.</summary>
    public async Task<DocumentUploadResult> UploadFromStreamAsync(
        int supplierId, SupplierDocumentType documentType, Stream content, string fileName,
        string? contentType, long size, string uploadedBy)
    {
        if (size > MaxFileSizeBytes)
        {
            return new DocumentUploadResult(false, "File exceeds the 10 MB size limit.", null, null);
        }

        var directory = DocumentsDirectory(supplierId);
        Directory.CreateDirectory(directory);

        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(directory, storedFileName);

        await using (var destination = new FileStream(fullPath, FileMode.Create))
        {
            await content.CopyToAsync(destination);
        }

        string? extractedNumber = null;
        string? dunsNumber = null;
        string? licenseType = null;
        List<LicenseMember> partners = [];
        ParsedLicense? license = null;   // full trade-license data (vision reader, or regex fallback)
        string? readSource = null;
        ParsedIdentityDocument? idDoc = null;   // Emirates ID / Passport capture (vision reader)

        if (documentType is SupplierDocumentType.VatCertification or SupplierDocumentType.TradeLicense)
        {
            // Metadata extraction (QR decode, official-PDF download, OCR, regex parsing) is best-effort.
            // A failure anywhere here must never block the upload, so the whole block is guarded; on error
            // the extracted values simply stay null/empty and the file is still saved to the DB below.
            try
            {
            var extractionPath = fullPath;
            string? tempOfficialPdf = null;

            // Trade License: if the document carries a DED verification QR code, download the official
            // PDF it points to and extract from that authoritative copy instead of the uploaded image.
            if (documentType == SupplierDocumentType.TradeLicense)
            {
                var qrUrl = qrService.TryReadQrUrl(fullPath);
                if (!string.IsNullOrWhiteSpace(qrUrl))
                {
                    tempOfficialPdf = await TryDownloadPdfAsync(qrUrl);
                    if (tempOfficialPdf is not null)
                    {
                        extractionPath = tempOfficialPdf;
                    }
                }
            }

            // Trade License: prefer the Claude-vision reader (handles DED + free-zone layouts and scans),
            // reading the best copy (the official PDF from the QR when available, else the upload).
            if (documentType == SupplierDocumentType.TradeLicense && licenseReader.IsEnabled)
            {
                var bytes = await File.ReadAllBytesAsync(extractionPath);
                var media = extractionPath == tempOfficialPdf ? "application/pdf" : contentType;
                var vision = await licenseReader.ReadAsync(bytes, media);
                if (vision is not null && LicenseHasData(vision))
                {
                    license = vision;
                    readSource = "Claude vision";
                }
            }

            // Fallback (and the VAT path): prefer the PDF's own text layer, else cloud OCR (Azure Document
            // Intelligence) for scanned licenses, then the regex parser. Best-effort - any value may stay null.
            if (license is null)
            {
                var text = TryReadPdfText(extractionPath);
                if (string.IsNullOrWhiteSpace(text) && ocr.IsEnabled)
                {
                    text = await ocr.ExtractTextAsync(extractionPath);
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (documentType == SupplierDocumentType.VatCertification)
                    {
                        extractedNumber = MatchNumber(text, TrnLabeled, TrnBare);
                    }
                    else
                    {
                        license = TradeLicenseParser.Parse(text);
                        readSource = "DED parser";
                    }
                }
            }

            if (documentType == SupplierDocumentType.TradeLicense && license is not null)
            {
                extractedNumber = license.LicenseNumber;
                dunsNumber = license.DunsNumber;
                licenseType = license.LegalType;
                partners = license.Members;
            }

            if (tempOfficialPdf is not null)
            {
                try { File.Delete(tempOfficialPdf); }
                catch { /* best-effort temp cleanup */ }
            }
            }
            catch
            {
                // Extraction is optional enrichment; swallow so a parse/OCR/download failure never
                // stops the file from being saved. Extracted values remain null and are simply not applied.
            }
        }
        else if (documentType == SupplierDocumentType.EmiratesIdOrPassport && identityReader.IsEnabled)
        {
            // Emirates ID / Passport: read the identity fields with the vision reader. Best-effort - a
            // failure must never block the upload, so it is guarded and simply leaves idDoc null.
            try
            {
                var bytes = await File.ReadAllBytesAsync(fullPath);
                idDoc = await identityReader.ReadAsync(bytes, contentType);
            }
            catch { /* optional enrichment */ }
        }

        // The local file above was needed for QR/OCR extraction. Move the durable copy to its store:
        //  • blob storage when enabled (record the blob key), else
        //  • the database Content column (matching customer documents).
        // Either way the local temp copy is then removed, so new uploads never linger on disk.
        var fileBytes = await File.ReadAllBytesAsync(fullPath);
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        await using var db = await dbFactory.CreateDbContextAsync();

        // Trade License / VAT / Bank / Signed Declaration are single-slot: a new upload replaces the
        // previous one. "Other" documents accumulate. Load the ones being replaced so their old blobs
        // can be removed after the new file is saved (never before, so a save failure can't lose data).
        var replaced = documentType != SupplierDocumentType.Other
            ? await db.SupplierDocuments
                .Where(d => d.SupplierId == supplierId && d.DocumentType == documentType)
                .ToListAsync()
            : new List<SupplierDocument>();

        // Lay blob files out like the "download all" zip: a folder per supplier, each file prefixed by
        // its document-type label — e.g. "10001_Al Nisr Publishing LLC/Trade License _ <original>.pdf".
        var meta = await db.Suppliers.Where(s => s.Id == supplierId)
            .Select(s => new { s.SupplierNumber, s.SupplierName })
            .FirstOrDefaultAsync();

        string? blobKey;
        try
        {
            if (fileStore.Enabled)
            {
                var folder = SanitizeSegment($"{meta?.SupplierNumber}_{meta?.SupplierName}");
                var name = SanitizeSegment($"{DocumentTypeLabel(documentType)} _ {fileName}");
                // Single-slot overwrites its own key; "Other" disambiguates duplicate names with " (n)".
                blobKey = await fileStore.SaveAtKeyAsync($"{folder}/{name}", fileBytes, resolvedContentType,
                    overwrite: documentType != SupplierDocumentType.Other);
            }
            else
            {
                blobKey = null;
            }
        }
        finally
        {
            // Always remove the local temp copy, even if the blob save throws, so failed uploads never
            // leave orphaned files accumulating under doc/supplier/{id}/.
            try { File.Delete(fullPath); } catch { /* best-effort cleanup of the local temp copy */ }
        }
        byte[]? contentBytes = blobKey is null ? fileBytes : null;

        // Now retire the superseded single-slot files — but never delete the blob we just wrote (which
        // happens when a same-named file is re-uploaded and overwrites its own key).
        foreach (var old in replaced)
        {
            if (!string.Equals(old.BlobKey, blobKey, StringComparison.Ordinal))
            {
                await DeleteStoredFileAsync(old);
            }
            db.SupplierDocuments.Remove(old);
        }

        db.SupplierDocuments.Add(new SupplierDocument
        {
            SupplierId = supplierId,
            DocumentType = documentType,
            FileName = fileName,
            StoredFileName = storedFileName,
            Content = contentBytes,
            BlobKey = blobKey,
            ContentType = resolvedContentType,
            FileSizeBytes = size,
            UploadedBy = uploadedBy
        });

        // Commit the document (and any single-slot replacement) first. This is the critical path: the
        // file must be recorded and the extracted number returned to the form regardless of whether the
        // optional enrichment below succeeds.
        await db.SaveChangesAsync();

        // Trade License extras: fold the D-U-N-S number and legal type into the supplier record and store
        // the partners annex for later reference. This is OPTIONAL enrichment, persisted in a separate,
        // best-effort save so any problem here - for example a database not yet migrated for the
        // LicenseType column or the SupplierPartners table - can never undo the upload above or block the
        // extracted number from reaching the form. Written only when something parsed, so a re-upload
        // that fails to parse never wipes good data.
        if (documentType == SupplierDocumentType.TradeLicense && license is not null)
        {
            try { await ApplyLicenseToSupplierAsync(db, supplierId, license, readSource); }
            catch
            {
                // Enrichment is optional; the document is already saved and the number already returned.
            }
        }

        // Emirates ID / Passport: fold the read identity fields into the supplier record (best-effort).
        if (documentType == SupplierDocumentType.EmiratesIdOrPassport && idDoc is not null && idDoc.HasData)
        {
            try { await ApplyIdentityToSupplierAsync(db, supplierId, idDoc, "Claude vision"); }
            catch { /* optional enrichment */ }
        }

        await auditService.LogAsync(uploadedBy, AuditAction.Update, "Supplier", supplierId, $"Uploaded {documentType} document '{fileName}'");

        return new DocumentUploadResult(true, null, extractedNumber, dunsNumber);
    }

    /// <summary>Runs a labeled regex (then an optional bare fallback) over already-extracted text and
    /// returns the first capture with inner whitespace stripped, or null when neither matches.</summary>
    private static string? MatchNumber(string text, Regex labeled, Regex? bareFallback)
    {
        var labeledMatch = labeled.Match(text);
        if (labeledMatch.Success)
        {
            return Regex.Replace(labeledMatch.Groups[1].Value, @"\s+", "");
        }

        if (bareFallback is not null)
        {
            var bareMatch = bareFallback.Match(text);
            if (bareMatch.Success)
            {
                return bareMatch.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>Downloads a PDF from a QR-code URL (the Dubai DED verification link) so extraction
    /// can use the authoritative copy. Best-effort: returns null on any failure or a non-PDF response.
    /// The caller deletes the returned temp file after use.</summary>
    private async Task<string?> TryDownloadPdfAsync(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return null;
            }

            // SSRF guard: the QR URL is attacker-controlled (decoded from an uploaded image), and this runs on
            // the internal server, so refuse any target that resolves to a loopback/link-local/private address
            // (e.g. the cloud metadata endpoint 169.254.169.254 or internal-only hosts).
            if (await ResolvesToBlockedHostAsync(uri))
            {
                return null;
            }

            var client = httpClientFactory.CreateClient("license");
            using var response = await client.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var looksPdf = response.Content.Headers.ContentType?.MediaType == "application/pdf"
                || (bytes.Length > 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46);
            if (!looksPdf)
            {
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), $"license-{Guid.NewGuid():N}.pdf");
            await File.WriteAllBytesAsync(tempPath, bytes);
            return tempPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True when the URI's host resolves to a loopback/link-local/private/reserved address, which
    /// must not be fetched (SSRF protection). Unresolvable hosts are treated as blocked.</summary>
    private static async Task<bool> ResolvesToBlockedHostAsync(Uri uri)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try { addresses = await Dns.GetHostAddressesAsync(uri.Host); }
            catch { return true; }
        }
        return addresses.Length == 0 || addresses.Any(IsPrivateOrReserved);
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) { return true; }

        if (ip.IsIPv4MappedToIPv6) { ip = ip.MapToIPv4(); }
        var b = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 0                                   // 0.0.0.0/8
                || b[0] == 10                                  // 10.0.0.0/8
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // 100.64.0.0/10 (CGNAT)
                || b[0] == 127                                 // loopback
                || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 (link-local, incl. metadata)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168);               // 192.168.0.0/16
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC;                      // fc00::/7 (unique local)
        }
        return true;
    }

    /// <summary>Reads the concatenated text layer of a PDF (best-effort). A non-PDF file, a scanned
    /// image with no text layer, or a parse failure all yield null rather than throwing.</summary>
    private static string? TryReadPdfText(string pdfPath)
    {
        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            return string.Join("\n", document.GetPages().Select(p => p.Text));
        }
        catch
        {
            // Malformed/encrypted/non-standard PDF - treat exactly like "nothing found".
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int documentId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return (false, "Document not found.");
        }

        await DeleteStoredFileAsync(document);
        db.SupplierDocuments.Remove(document);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Delete, "Supplier", document.SupplierId, $"Deleted {document.DocumentType} document '{document.FileName}'");

        return (true, null);
    }

    /// <summary>Marks (or clears) an attachment's internal-review flag. Internal reviewers only - this is
    /// never driven from the supplier self-service form. Records who reviewed it and when.</summary>
    public async Task<(bool Success, string? Error)> SetReviewedAsync(int documentId, bool reviewed, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return (false, "Document not found.");
        }

        document.IsReviewed = reviewed;
        document.ReviewedBy = reviewed ? actorUsername : null;
        document.ReviewedAt = reviewed ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", document.SupplierId,
            $"{(reviewed ? "Marked" : "Cleared")} review on {document.DocumentType} document '{document.FileName}'");

        return (true, null);
    }

    /// <summary>Returns the bytes + file name of every image attachment for a supplier (in display
    /// order), for combining into a PDF / Excel export. Non-image documents (e.g. PDFs) are skipped.</summary>
    public async Task<List<(byte[] Bytes, string FileName)>> GetImageFilesAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId && d.ContentType.StartsWith("image/"))
            .OrderBy(d => d.DocumentType).ThenBy(d => d.UploadedAt)
            .ToListAsync();

        var result = new List<(byte[] Bytes, string FileName)>();
        foreach (var doc in docs)
        {
            var bytes = await ReadStoredBytesAsync(doc);
            if (bytes is not null)
            {
                result.Add((bytes, doc.FileName));
            }
        }
        return result;
    }

    /// <summary>Every attachment for a supplier as (bytes, original file name), of any content type -
    /// used to build the "download all" zip. Skips any record whose physical file is missing.</summary>
    public async Task<List<(byte[] Bytes, string FileName)>> GetAllFilesAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.UploadedAt)
            .ToListAsync();

        var result = new List<(byte[] Bytes, string FileName)>();
        foreach (var doc in docs)
        {
            var bytes = await ReadStoredBytesAsync(doc);
            if (bytes is not null)
            {
                // Prefix each zipped file with its document-type label, e.g. "Trade License _ <original name>".
                result.Add((bytes, $"{DocumentTypeLabel(doc.DocumentType)} _ {doc.FileName}"));
            }
        }
        return result;
    }

    /// <summary>Human-readable document-type label used to prefix file names in the "download all" zip.</summary>
    private static string DocumentTypeLabel(SupplierDocumentType type) => type switch
    {
        SupplierDocumentType.TradeLicense => "Trade License",
        SupplierDocumentType.VatCertification => "VAT",
        SupplierDocumentType.BankConfirmationLetter => "Bank Confirmation Letter",
        SupplierDocumentType.SignedDeclaration => "Signed Declaration",
        SupplierDocumentType.EmiratesIdOrPassport => "Emirates ID or Passport",
        SupplierDocumentType.ProofOfAddress => "Proof of Address",
        SupplierDocumentType.PriceListOrQuotation => "Price List or Quotation",
        SupplierDocumentType.ProofOfOrigin => "Proof of Origin",
        SupplierDocumentType.CertificateOfIncorporation => "Certificate of Incorporation",
        _ => "Other"
    };

    /// <summary>Makes a string safe as a single blob path segment: no slashes/control characters,
    /// collapsed whitespace, trimmed, and length-capped. Falls back to "unknown" when empty.</summary>
    /// <summary>True when a vision read produced at least one meaningful field (so we prefer it over the
    /// regex fallback). A blank result (API returned nothing useful) is treated as a miss.</summary>
    private static bool LicenseHasData(ParsedLicense l) =>
        !string.IsNullOrWhiteSpace(l.LicenseNumber)
        || !string.IsNullOrWhiteSpace(l.LegalType)
        || !string.IsNullOrWhiteSpace(l.ExpiryDate)
        || !string.IsNullOrWhiteSpace(l.IssuingAuthority)
        || !string.IsNullOrWhiteSpace(l.RegisterNumber)
        || l.Members.Count > 0
        || l.Activities.Count > 0;

    /// <summary>Parses a licence date string (DD/MM/YYYY and a few common variants) to a DateTime, or null.</summary>
    private static DateTime? ParseLicenseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd.MM.yyyy" };
        if (DateTime.TryParseExact(value.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt;
        }
        return DateTime.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var any) ? any : null;
    }

    /// <summary>Parses the paid-capital string (digits, possibly with commas/decimals) to a decimal, or null.</summary>
    private static decimal? ParseCapital(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        var digits = new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
        return decimal.TryParse(digits, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return "unknown"; }
        var sb = new System.Text.StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var ch in value)
        {
            var c = (char.IsControl(ch) || ch == '\\' || ch == '/') ? ' ' : ch;
            if (c == ' ')
            {
                if (lastWasSpace) { continue; }
                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }
            sb.Append(c);
        }
        var cleaned = sb.ToString().Trim().TrimEnd('.', ' ').Trim();
        if (cleaned.Length == 0) { return "unknown"; }
        return cleaned.Length > 200 ? cleaned[..200].TrimEnd('.', ' ') : cleaned;
    }

    /// <summary>The owning supplier id for a document, or null when the document does not exist. Lets the
    /// download endpoint enforce per-supplier access scope before serving the file.</summary>
    public async Task<int?> GetSupplierIdForDocumentAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SupplierDocuments.Where(d => d.Id == documentId)
            .Select(d => (int?)d.SupplierId).FirstOrDefaultAsync();
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return null;
        }

        var bytes = await ReadStoredBytesAsync(document);
        return bytes is null ? null : (bytes, document.ContentType, document.FileName);
    }

    /// <summary>Reads a document's bytes from Azure Blob Storage (blob key), the database (Content column),
    /// or the legacy local-disk copy — in that order. Returns null when the file can't be found.</summary>
    private async Task<byte[]?> ReadStoredBytesAsync(SupplierDocument doc)
    {
        // Prefer the blob, but never give up on it alone: if the blob read fails or comes back empty
        // (e.g. a stale/missing blob key on a record whose bytes are actually in the database or on disk),
        // fall through to the Content column and then the legacy local-disk copy.
        if (!string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            try
            {
                var blobBytes = await fileStore.ReadAsync(doc.BlobKey);
                if (blobBytes is { Length: > 0 })
                {
                    return blobBytes;
                }
            }
            catch { /* fall through to the other sources below */ }
        }
        if (doc.Content is { Length: > 0 })
        {
            return doc.Content;
        }
        var fullPath = Path.Combine(DocumentsDirectory(doc.SupplierId), doc.StoredFileName);
        return File.Exists(fullPath) ? await File.ReadAllBytesAsync(fullPath) : null;
    }

    /// <summary>Deletes a document's stored file: the blob (blob key) or the legacy local-disk copy.
    /// Database-stored bytes (Content) are removed with the document row itself.</summary>
    private async Task DeleteStoredFileAsync(SupplierDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            await fileStore.DeleteAsync(doc.BlobKey);
            return;
        }
        if (doc.Content is { Length: > 0 })
        {
            return; // stored in the database — removed when the row is deleted
        }
        DeletePhysicalFile(doc.SupplierId, doc.StoredFileName);
    }

    private void DeletePhysicalFile(int supplierId, string storedFileName)
    {
        var path = Path.Combine(DocumentsDirectory(supplierId), storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>One read-only "what Claude read from this document" card per uploaded document, built from
    /// the per-field extraction log. Used to show every document's extracted fields (VAT Certificate,
    /// Certificate of Incorporation, Bank Confirmation Letter, Proof of Address, …) the same way the Trade
    /// License capture is shown. For each document the latest value seen for each field is used, so re-runs
    /// supersede earlier ones without duplicating rows.</summary>
    public async Task<List<KysExtractionCard>> GetExtractionCardsAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var logs = await db.SupplierKysExtractionLogs.AsNoTracking()
            .Where(l => l.SupplierId == supplierId)
            .ToListAsync();

        var cards = new List<KysExtractionCard>();
        foreach (var group in logs.GroupBy(l => l.DocumentType))
        {
            if (string.IsNullOrWhiteSpace(group.Key)) { continue; }

            // Latest row per field name (most recent extraction wins), kept in the order they were written
            // in that newest run so the card reads top-to-bottom like the document.
            var latestByField = group
                .GroupBy(x => x.FieldName)
                .Select(fg => fg.OrderByDescending(x => x.ExtractedAt).ThenByDescending(x => x.Id).First())
                .OrderBy(x => x.Id)
                .ToList();
            if (latestByField.Count == 0) { continue; }

            var newest = group.OrderByDescending(x => x.ExtractedAt).ThenByDescending(x => x.Id).First();
            cards.Add(new KysExtractionCard
            {
                DocumentType = group.Key,
                DocumentLabel = string.IsNullOrWhiteSpace(newest.DocumentLabel) ? group.Key : newest.DocumentLabel,
                DocumentId = newest.DocumentId,
                FileName = newest.FileName,
                ExtractedAt = newest.ExtractedAt,
                ExtractedBy = newest.ExtractedBy,
                Fields = latestByField.Select(x => new KysExtractionCardField
                {
                    Name = x.FieldName,
                    Value = x.FieldValue ?? string.Empty,
                    Outcome = x.Outcome,
                }).ToList(),
            });
        }
        // Stable order: show the documents in a sensible, repeatable sequence.
        return cards.OrderBy(c => c.DocumentLabel, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

/// <summary>The auto-read Trade License capture surfaced read-only under the Trade License document.</summary>
public sealed class TradeLicenseReadback
{
    public string? LicenseNumber { get; set; }
    public string? MainLicenseNumber { get; set; }
    public string? LegalName { get; set; }
    public string? TradeName { get; set; }
    public string? LegalType { get; set; }
    public string? LicenseCategory { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? LicenseType { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RegisterNumber { get; set; }
    public string? DcciNumber { get; set; }
    public string? DunsNumber { get; set; }
    public decimal? PaidCapital { get; set; }
    public string? CapitalCurrency { get; set; }
    public string? Activities { get; set; }
    public string? PoBox { get; set; }
    public string? ParcelId { get; set; }
    public string? Premises { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadSource { get; set; }
    public List<SupplierPortal.Web.Models.SupplierPartner> Parties { get; set; } = [];

    /// <summary>True when a license has actually been read (so the panel is worth showing).</summary>
    public bool HasData => ReadAt is not null || !string.IsNullOrWhiteSpace(LicenseNumber) || Parties.Count > 0;
}

/// <summary>The auto-read Emirates ID / Passport capture for the read-only panel under that document.</summary>
public sealed class IdentityReadback
{
    public string? EmiratesIdNumber { get; set; }
    public DateTime? EmiratesIdExpiryDate { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime? PassportExpiryDate { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ReadSource { get; set; }

    /// <summary>True when something was read (so the panel is worth showing).</summary>
    public bool HasData => ReadAt is not null
        || !string.IsNullOrWhiteSpace(EmiratesIdNumber) || !string.IsNullOrWhiteSpace(PassportNumber);
}

/// <summary>Summary of a "extract &amp; store all KYS data from documents" run.</summary>
public sealed class KysExtractionResult
{
    /// <summary>True when the Claude document reader is configured.</summary>
    public bool Enabled { get; set; }
    /// <summary>Documents that were read (had content Claude could parse).</summary>
    public List<string> DocsRead { get; } = [];
    /// <summary>"Field: value" entries that were newly stored on the supplier (empty fields only).</summary>
    public List<string> Stored { get; } = [];
    /// <summary>A fatal error (reader not configured, supplier missing); null on success.</summary>
    public string? Error { get; set; }
}

/// <summary>A read-only "what Claude read from this document" card, built from the per-field extraction log.
/// One card per uploaded document (VAT Certificate, Certificate of Incorporation, Bank Confirmation Letter,
/// Proof of Address, …), shown under the Documents section the same way the Trade License capture is shown.</summary>
public sealed class KysExtractionCard
{
    /// <summary>The <see cref="SupplierDocumentType"/> name (e.g. "VatCertification").</summary>
    public string DocumentType { get; set; } = string.Empty;
    /// <summary>Friendly document name shown in the card header (e.g. "VAT Certificate").</summary>
    public string DocumentLabel { get; set; } = string.Empty;
    /// <summary>The source document id (for preview / download); null if the document was removed.</summary>
    public int? DocumentId { get; set; }
    public string? FileName { get; set; }
    public DateTime ExtractedAt { get; set; }
    public string? ExtractedBy { get; set; }
    public List<KysExtractionCardField> Fields { get; set; } = [];
}

/// <summary>One extracted field (name + value) on a <see cref="KysExtractionCard"/>.</summary>
public sealed class KysExtractionCardField
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    /// <summary>"Stored", "Skipped (already filled)" or "Read (reference only)".</summary>
    public string Outcome { get; set; } = string.Empty;
}
