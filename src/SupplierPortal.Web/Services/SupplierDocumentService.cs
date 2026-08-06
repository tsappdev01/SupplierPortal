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
                            Share = partner.Share
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

    /// <summary>Parses the "Partners" (ملحق الشركاء) annex of a Dubai DED trade license. Best-effort
    /// and tuned to that bilingual layout: each row carries a serial/person number, a Latin name and
    /// an ownership share; nationality is captured only when it prints in Latin next to the row.
    /// Returns an empty list when nothing parses, so a non-standard license never breaks the upload.</summary>
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

    private static List<ParsedPartner> ExtractPartners(string text)
    {
        // Scope to the owners/members section. Two layouts exist: the older "Partners" / "ملحق الشركاء"
        // annex, and the newer inline "License Members" / "الاطراف" block. Start at the first heading we
        // recognise (priority order) and stop at the page footer ("Print Date") so numbers from later
        // pages (receipts, the commercial register) cannot look like partner rows.
        var start = FirstIndexOf(text, "اصحاب", "الاطراف", "License Members", "Partners");
        var scope = start >= 0 ? text[start..] : text;
        var footer = scope.IndexOf("Print Date", StringComparison.OrdinalIgnoreCase);
        if (footer > 0)
        {
            scope = scope[..footer];
        }

        var partners = new List<ParsedPartner>();
        foreach (Match m in PartnerRow.Matches(scope))
        {
            var name = Regex.Replace(m.Groups[2].Value, @"\s+", " ").Trim();
            // Skip header/receipt noise that structurally resembles a row but is not a partner
            // (the license-details line carries "D&B D-U-N-S ® No.", receipts carry "Renew License").
            if (name.Length < 2 || name.Contains('&') || name.Contains('®')
                || name.Contains("D-U-N-S", StringComparison.OrdinalIgnoreCase)
                || name.Contains("License", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var serial = m.Groups[1].Value.Trim();
            var share = m.Groups[3].Value.Trim();
            var nationality = Regex.Match(m.Value, "United Arab Emirates", RegexOptions.IgnoreCase).Success
                ? "United Arab Emirates"
                : null;
            partners.Add(new ParsedPartner(name, nationality, serial, share));
        }
        return partners;
    }

    private sealed record ParsedPartner(string Name, string? Nationality, string? SerialNo, string? Share);

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
