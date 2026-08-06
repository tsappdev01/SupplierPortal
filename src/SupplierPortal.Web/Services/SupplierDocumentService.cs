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

/// <summary>Stores supplier-uploaded documents (Trade License, VAT Certification, Other) as
/// physical files under the app's content root - not wwwroot, so they are never directly
/// web-servable - with only their metadata in the database.</summary>
public class SupplierDocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService,
    IDocumentOcrService ocr)
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // UAE Tax Registration Numbers are always 15 digits. Matched either next to a recognizable
    // label, or (fallback) as any standalone 15-digit run in the document - VAT certificates
    // consistently print the TRN as the only long digit run on the page.
    private static readonly Regex TrnLabeled = new(
        @"(?:TRN|Tax\s*Registration\s*Number)\D{0,10}(\d[\d\s]{13,25}\d)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TrnBare = new(@"\b(\d{15})\b", RegexOptions.Compiled);

    // The filler between the label and the value must be punctuation/whitespace only (not \D) -
    // license numbers are alphanumeric, so a greedy \D{0,n} filler would itself eat a letter
    // prefix like "TL-" off the front of the value before the capture group ever saw it.
    private static readonly Regex TradeLicenseLabeled = new(
        @"(?:Trade\s*Licen[cs]e|Licen[cs]e)\s*(?:No\.?|Number)\s*[:\-.]{0,2}\s*([A-Za-z0-9][A-Za-z0-9\-\/]{2,19})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // D&B D-U-N-S numbers are always 9 digits. The Dubai DED license prints the shorter (6-digit)
    // license number right before the D-U-N-S label, so match a 9-digit run near the label - with a
    // bare 9-digit fallback, which on these licenses is only ever the D-U-N-S number itself.
    private static readonly Regex DunsLabeled = new(
        @"D\W*U\W*N\W*S.*?\b(\d{9})\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex DunsBare = new(@"\b(\d{9})\b", RegexOptions.Compiled);

    // A row of the license "Partners" annex: a serial/person number, a Latin (English) name that
    // stops at the first digit or line break, then the ownership share as a percentage. Any Arabic
    // name and other text between the name and the share is skipped by the non-greedy [^%]*? gap.
    private static readonly Regex PartnerRow = new(
        @"(\d{4,7})\s+([A-Za-z][^\n%\d]*[A-Za-z.)])[^%]*?(\d{1,3}(?:\.\d+)?)\s*%",
        RegexOptions.Compiled);

    // Inline "License Members" layout helpers. A member name is >= 2 all-caps Latin words
    // (e.g. "ARIF ABDULLA AHMAD OBAID ALSHAMSI"); serial and share are read from a window around it.
    private static readonly Regex MemberName = new(
        @"\b[A-Z][A-Z]+(?:\s+[A-Z][A-Z.'&()\-]+){1,6}\b",
        RegexOptions.Compiled);
    private static readonly Regex SerialNumber = new(@"\b\d{4,7}\b", RegexOptions.Compiled);
    private static readonly Regex SharePercent = new(@"(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);

    private string DocumentsDirectory(int supplierId) =>
        Path.Combine(env.ContentRootPath, "doc", "supplier", supplierId.ToString());

    public async Task<List<SupplierDocument>> GetDocumentsAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.SupplierDocuments
            .Where(d => d.SupplierId == supplierId)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.UploadedAt)
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
        List<ParsedPartner> partners = [];

        if (documentType is SupplierDocumentType.VatCertification or SupplierDocumentType.TradeLicense)
        {
            // Prefer the PDF's own text layer; fall back to cloud OCR (Azure Document Intelligence)
            // for scanned/image licenses that carry no embedded text. Both are best-effort - if neither
            // yields text, every extracted value stays null/empty and the upload still succeeds.
            var text = TryReadPdfText(fullPath);
            if (string.IsNullOrWhiteSpace(text) && ocr.IsEnabled)
            {
                text = await ocr.ExtractTextAsync(fullPath);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (documentType == SupplierDocumentType.VatCertification)
                {
                    extractedNumber = MatchNumber(text, TrnLabeled, TrnBare);
                }
                else
                {
                    extractedNumber = MatchNumber(text, TradeLicenseLabeled, null);

                    var dunsMatch = DunsLabeled.Match(text);
                    if (!dunsMatch.Success)
                    {
                        dunsMatch = DunsBare.Match(text);
                    }
                    if (dunsMatch.Success)
                    {
                        dunsNumber = dunsMatch.Groups[1].Value;
                    }

                    partners = ExtractPartners(text);
                }
            }
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        // Trade License / VAT Certification are single-slot fields: a new upload replaces the
        // previous one. "Other" documents accumulate as a list instead.
        if (documentType != SupplierDocumentType.Other)
        {
            var existing = await db.SupplierDocuments
                .Where(d => d.SupplierId == supplierId && d.DocumentType == documentType)
                .ToListAsync();
            foreach (var old in existing)
            {
                DeletePhysicalFile(supplierId, old.StoredFileName);
                db.SupplierDocuments.Remove(old);
            }
        }

        db.SupplierDocuments.Add(new SupplierDocument
        {
            SupplierId = supplierId,
            DocumentType = documentType,
            FileName = fileName,
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            FileSizeBytes = size,
            UploadedBy = uploadedBy
        });

        // Trade License extras: fold the D-U-N-S number into the supplier record and store the
        // partners annex for later reference (not shown anywhere in the UI). Each is written only
        // when something actually parsed, so a re-upload that fails to parse never wipes good data.
        if (documentType == SupplierDocumentType.TradeLicense)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
            if (supplier is not null)
            {
                if (!string.IsNullOrWhiteSpace(dunsNumber))
                {
                    supplier.DunsNumber = dunsNumber;
                }

                if (partners.Count > 0)
                {
                    var existingPartners = await db.SupplierPartners
                        .Where(p => p.SupplierId == supplierId).ToListAsync();
                    db.SupplierPartners.RemoveRange(existingPartners);
                    foreach (var partner in partners)
                    {
                        db.SupplierPartners.Add(new SupplierPartner
                        {
                            SupplierId = supplierId,
                            Name = partner.Name,
                            Nationality = partner.Nationality,
                            SerialNo = partner.SerialNo,
                            Share = partner.Share,
                            Role = partner.Role
                        });
                    }
                }
            }
        }

        await db.SaveChangesAsync();
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

    private static int FirstIndexOf(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            var i = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i >= 0)
            {
                return i;
            }
        }
        return -1;
    }

    private static int EarliestIndexOf(string text, params string[] needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var i = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best))
            {
                best = i;
            }
        }
        return best;
    }

    // Known nationality/role strings so they can be read out of a member row wherever the text places
    // them. Roles are listed longest-first so "Shares Owner" wins over "Owner", "General Manager" over
    // "Manager", etc.
    private static readonly string[] KnownNationalities =
    [
        "United Arab Emirates", "Saudi Arabia", "Oman", "Kuwait", "Bahrain", "Qatar", "Yemen",
        "Egypt", "Jordan", "Lebanon", "Syria", "Sudan", "Iraq", "Iran", "Palestine",
        "India", "Pakistan", "Bangladesh", "Sri Lanka", "Nepal", "Philippines", "China",
        "United Kingdom", "United States", "Canada", "Germany", "France"
    ];

    private static readonly string[] KnownRoles =
    [
        "Shares Owner", "Managing Partner", "General Manager", "Managing Director",
        "Sole Proprietor", "Authorized Signatory", "Local Service Agent",
        "Manager", "Partner", "Director", "Owner", "Agent"
    ];

    private static readonly string[] StatusWords = ["Active", "Inactive", "Expired", "Cancelled"];

    /// <summary>Normalises a captured name and strips any trailing nationality / role / status words
    /// that a single-line inline row folds onto the end of the name (e.g. "... ALSHAMSI United Arab
    /// Emirates Shares Owner" -> "... ALSHAMSI").</summary>
    private static string CleanName(string raw)
    {
        var name = Regex.Replace(raw, @"\s+", " ").Trim().Trim('.', ',', '-', '/').Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var token in KnownRoles.Concat(KnownNationalities).Concat(StatusWords))
            {
                if (name.Length > token.Length
                    && name.EndsWith(token, StringComparison.OrdinalIgnoreCase)
                    && char.IsWhiteSpace(name[name.Length - token.Length - 1]))
                {
                    name = name[..^token.Length].Trim().Trim('.', ',', '-', '/').Trim();
                    changed = true;
                    break;
                }
            }
        }
        return name;
    }

    private static string? FirstContained(string haystack, string[] options)
    {
        foreach (var option in options)
        {
            if (haystack.Contains(option, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }
        return null;
    }


    private static string Slice(string s, int from, int to)
    {
        from = Math.Max(0, from);
        to = Math.Min(s.Length, to);
        return from < to ? s[from..to] : "";
    }

    /// <summary>Parses the owners/members section of a Dubai DED license. Handles both the older
    /// "Partners" / "ملحق الشركاء" annex (serial + name + share%) and the newer inline "License
    /// Members" / "الاطراف" table (No., Name, Nationality, Role, Share - the share is often blank for a
    /// manager). Best-effort and tuned to that bilingual layout; returns an empty list when nothing
    /// parses, so a non-standard license never breaks the upload. Names are matched in Latin, so an
    /// Arabic-only name yields no row.</summary>
    private static List<ParsedPartner> ExtractPartners(string text)
    {
        // Scope to the members section: start at the first heading we recognise (priority order) and
        // end at the next section/footer, so activities and later pages cannot look like member rows.
        var start = FirstIndexOf(text, "اصحاب", "الاطراف", "License Members", "Partners");
        var scope = start >= 0 ? text[start..] : text;
        var end = EarliestIndexOf(scope, "License Activities", "نشاط الرخصة", "الملاحظات", "Remarks", "Print Date");
        if (end > 0)
        {
            scope = scope[..end];
        }

        var partners = new List<ParsedPartner>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string rawName, string? serial, string? share, string? role, string? nationality)
        {
            var name = CleanName(rawName);
            if (name.Length < 3
                || name.Contains('&') || name.Contains('®')
                || name.Contains("D-U-N-S", StringComparison.OrdinalIgnoreCase)
                || name.Contains("License", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Members", StringComparison.OrdinalIgnoreCase)
                || KnownRoles.Any(r => name.Equals(r, StringComparison.OrdinalIgnoreCase))
                || KnownNationalities.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            if (!seen.Add(name))
            {
                return;
            }
            partners.Add(new ParsedPartner(
                name,
                nationality,
                string.IsNullOrWhiteSpace(serial) ? null : serial.Trim(),
                string.IsNullOrWhiteSpace(share) ? null : share.Trim(),
                role));
        }

        // Pass 1 - share-anchored rows (the classic annex, and any inline row that prints a share).
        foreach (Match m in PartnerRow.Matches(scope))
        {
            var window = Slice(scope, m.Index - 40, m.Index + m.Length + 40);
            Add(m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value,
                FirstContained(window, KnownRoles), FirstContained(window, KnownNationalities));
        }

        // Pass 2 - inline "License Members" rows anchored on the all-caps Latin name, so members whose
        // share is blank (e.g. a manager) are still captured. Serial/share/role/nationality are read
        // from a window around the name; names already captured in pass 1 are skipped by the dedupe.
        foreach (Match m in MemberName.Matches(scope))
        {
            // The serial/No. prints just before the name; nationality, role and share print just after
            // it on the same table row. Read each from its own side so a neighbouring row's share (e.g.
            // the owner's 100%) can't leak onto a share-less manager.
            var before = Slice(scope, m.Index - 30, m.Index);
            var after = Slice(scope, m.Index + m.Length, m.Index + m.Length + 90);
            var serials = SerialNumber.Matches(before);
            var shareMatch = SharePercent.Match(after);
            Add(m.Value,
                serials.Count > 0 ? serials[^1].Value : null,
                shareMatch.Success ? shareMatch.Groups[1].Value : null,
                FirstContained(after, KnownRoles),
                FirstContained(after, KnownNationalities));
        }

        return partners;
    }

    private sealed record ParsedPartner(
        string Name, string? Nationality, string? SerialNo, string? Share, string? Role);

    public async Task<(bool Success, string? Error)> DeleteAsync(int documentId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return (false, "Document not found.");
        }

        DeletePhysicalFile(document.SupplierId, document.StoredFileName);
        db.SupplierDocuments.Remove(document);
        await db.SaveChangesAsync();
        await auditService.LogAsync(actorUsername, AuditAction.Delete, "Supplier", document.SupplierId, $"Deleted {document.DocumentType} document '{document.FileName}'");

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
            var fullPath = Path.Combine(DocumentsDirectory(supplierId), doc.StoredFileName);
            if (File.Exists(fullPath))
            {
                result.Add((await File.ReadAllBytesAsync(fullPath), doc.FileName));
            }
        }
        return result;
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.SupplierDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return null;
        }

        var fullPath = Path.Combine(DocumentsDirectory(document.SupplierId), document.StoredFileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath);
        return (bytes, document.ContentType, document.FileName);
    }

    private void DeletePhysicalFile(int supplierId, string storedFileName)
    {
        var path = Path.Combine(DocumentsDirectory(supplierId), storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
