using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using UglyToad.PdfPig;

namespace SupplierPortal.Web.Services;

/// <summary>Stores supplier-uploaded documents (Trade License, VAT Certification, Other) as
/// physical files under the app's content root - not wwwroot, so they are never directly
/// web-servable - with only their metadata in the database.</summary>
public class SupplierDocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService)
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

    public async Task<(bool Success, string? Error, string? ExtractedNumber)> UploadAsync(
        int supplierId, SupplierDocumentType documentType, IBrowserFile file, string uploadedBy)
    {
        if (file.Size > MaxFileSizeBytes)
        {
            return (false, "File exceeds the 10 MB size limit.", null);
        }

        var directory = DocumentsDirectory(supplierId);
        Directory.CreateDirectory(directory);

        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.Name)}";
        var fullPath = Path.Combine(directory, storedFileName);

        await using (var destination = new FileStream(fullPath, FileMode.Create))
        await using (var source = file.OpenReadStream(MaxFileSizeBytes))
        {
            await source.CopyToAsync(destination);
        }

        string? extractedNumber = documentType switch
        {
            SupplierDocumentType.VatCertification => TryExtractNumber(fullPath, TrnLabeled, TrnBare),
            SupplierDocumentType.TradeLicense => TryExtractNumber(fullPath, TradeLicenseLabeled, null),
            _ => null
        };

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
            FileName = file.Name,
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Size,
            UploadedBy = uploadedBy
        });

        await db.SaveChangesAsync();
        await auditService.LogAsync(uploadedBy, AuditAction.Update, "Supplier", supplierId, $"Uploaded {documentType} document '{file.Name}'");

        return (true, null, extractedNumber);
    }

    /// <summary>Best-effort text-layer extraction for scanned/native PDFs: tries a labeled match
    /// first (e.g. "TRN: 100333156600003"), falling back to a bare-pattern match when provided.
    /// A non-PDF file, a scanned image with no text layer, or a parse failure all just yield no
    /// match rather than an error - the corresponding form field stays available for manual entry.</summary>
    private static string? TryExtractNumber(string pdfPath, Regex labeled, Regex? bareFallback)
    {
        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var document = PdfDocument.Open(pdfPath);
            var text = string.Join("\n", document.GetPages().Select(p => p.Text));

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
        }
        catch
        {
            // Malformed/encrypted/non-standard PDF - treat exactly like "nothing found".
        }

        return null;
    }

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
