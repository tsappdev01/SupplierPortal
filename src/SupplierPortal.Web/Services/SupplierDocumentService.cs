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
    IDocumentOcrService ocr, IHttpClientFactory httpClientFactory, LicenseQrService qrService,
    SupplierPortal.Web.Services.Storage.IFileStorageService fileStore)
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

            // Prefer the PDF's own text layer; fall back to cloud OCR (Azure Document Intelligence)
            // for scanned/image licenses that carry no embedded text. Both are best-effort - if neither
            // yields text, every extracted value stays null/empty and the upload still succeeds.
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
                    var license = TradeLicenseParser.Parse(text);
                    extractedNumber = license.LicenseNumber;
                    dunsNumber = license.DunsNumber;
                    licenseType = license.LegalType;
                    partners = license.Members;
                }
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

        // The local file above was needed for QR/OCR extraction. Move the durable copy to its store:
        //  • blob storage when enabled (record the blob key), else
        //  • the database Content column (matching customer documents).
        // Either way the local temp copy is then removed, so new uploads never linger on disk.
        var fileBytes = await File.ReadAllBytesAsync(fullPath);
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        string? blobKey = fileStore.Enabled
            ? await fileStore.SaveAsync($"supplier/{supplierId}", fileName, fileBytes, resolvedContentType)
            : null;
        byte[]? contentBytes = blobKey is null ? fileBytes : null;
        try { File.Delete(fullPath); } catch { /* best-effort cleanup of the local temp copy */ }

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
                await DeleteStoredFileAsync(old);
                db.SupplierDocuments.Remove(old);
            }
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
        if (documentType == SupplierDocumentType.TradeLicense
            && (!string.IsNullOrWhiteSpace(dunsNumber) || !string.IsNullOrWhiteSpace(licenseType) || partners.Count > 0))
        {
            try
            {
                var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
                if (supplier is not null)
                {
                    if (!string.IsNullOrWhiteSpace(dunsNumber))
                    {
                        supplier.DunsNumber = dunsNumber;
                    }

                    if (!string.IsNullOrWhiteSpace(licenseType))
                    {
                        supplier.LicenseType = licenseType;
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
                                SerialNo = partner.PersonNo,
                                Share = partner.Share,
                                Role = partner.Role
                            });
                        }
                    }

                    await db.SaveChangesAsync();
                }
            }
            catch
            {
                // Enrichment is optional; the document is already saved and the number already returned.
            }
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
                result.Add((bytes, doc.FileName));
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

        var bytes = await ReadStoredBytesAsync(document);
        return bytes is null ? null : (bytes, document.ContentType, document.FileName);
    }

    /// <summary>Reads a document's bytes from Azure Blob Storage (blob key), the database (Content column),
    /// or the legacy local-disk copy — in that order. Returns null when the file can't be found.</summary>
    private async Task<byte[]?> ReadStoredBytesAsync(SupplierDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            return await fileStore.ReadAsync(doc.BlobKey);
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
}
