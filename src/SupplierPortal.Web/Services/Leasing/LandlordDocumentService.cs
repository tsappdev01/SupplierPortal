using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Stores landlord-uploaded documents entirely in the database as Base64 strings
/// (<c>leasing.LandlordDocuments.ContentBase64</c>) — nothing is written to the server file system. All
/// documents are retained for the complete registration trace; single-slot identity documents keep their
/// superseded versions (IsCurrent = false).</summary>
public class LandlordDocumentService(IDbContextFactory<LeasingDbContext> dbFactory)
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Accepted upload types (FRD §12 — supported file type).</summary>
    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    /// <summary>Current (live) documents only — metadata, without the (large) Base64 content.</summary>
    public async Task<List<LandlordDocument>> GetDocumentsAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId && d.IsCurrent)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.UploadedAt)
            .Select(d => Meta(d))
            .ToListAsync();
    }

    /// <summary>Every document ever uploaded, including superseded versions (metadata only).</summary>
    public async Task<List<LandlordDocument>> GetAllVersionsAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId)
            .OrderBy(d => d.DocumentType).ThenByDescending(d => d.UploadedAt)
            .Select(d => Meta(d))
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> UploadAsync(
        int landlordId, LandlordDocumentType documentType, IBrowserFile file, string uploadedBy,
        int? contactId = null, int? signatoryId = null)
    {
        // FRD §12 validation: not empty, supported type, within the size limit.
        if (file.Size == 0) return (false, "The selected file is empty.");
        if (file.Size > MaxFileSizeBytes) return (false, "File exceeds the 10 MB size limit.");
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (false, $"Unsupported file type '{ext}'. Allowed: PDF or image (JPG, PNG, TIFF).");

        // Read the whole file into memory and Base64-encode it for in-table storage.
        using var ms = new MemoryStream();
        await using (var source = file.OpenReadStream(MaxFileSizeBytes))
        {
            await source.CopyToAsync(ms);
        }
        var base64 = Convert.ToBase64String(ms.ToArray());

        await using var db = await dbFactory.CreateDbContextAsync();

        // Single-slot identity documents supersede the previous upload (kept with IsCurrent = false for
        // the audit history); "Other" documents accumulate. Slot scoped to the contact when given.
        var version = 1;
        if (documentType != LandlordDocumentType.Other)
        {
            var existing = await db.LandlordDocuments
                .Where(d => d.LandlordId == landlordId && d.DocumentType == documentType
                    && d.ContactId == contactId && d.IsCurrent)
                .ToListAsync();
            foreach (var old in existing)
            {
                old.IsCurrent = false;
                version = Math.Max(version, old.Version + 1);
            }
        }

        db.LandlordDocuments.Add(new LandlordDocument
        {
            LandlordId = landlordId,
            DocumentType = documentType,
            FileName = file.Name,
            StoredFileName = $"{Guid.NewGuid():N}{ext}",   // logical name only; no file on disk
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Size,
            ContentBase64 = base64,
            UploadedBy = uploadedBy,
            ContactId = contactId,
            SignatoryId = signatoryId,
            Version = version,
            IsCurrent = true
        });
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Stores a system-generated document (raw bytes) against the registration as Base64, superseding
    /// any previous current document of the same type. Used for the registration-summary PDF on submission.</summary>
    public async Task StoreGeneratedAsync(
        int landlordId, LandlordDocumentType documentType, string fileName, string contentType, byte[] bytes, string uploadedBy)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var version = 1;
        var existing = await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId && d.DocumentType == documentType && d.IsCurrent)
            .ToListAsync();
        foreach (var old in existing) { old.IsCurrent = false; version = Math.Max(version, old.Version + 1); }

        db.LandlordDocuments.Add(new LandlordDocument
        {
            LandlordId = landlordId,
            DocumentType = documentType,
            FileName = fileName,
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            ContentType = contentType,
            FileSizeBytes = bytes.LongLength,
            ContentBase64 = Convert.ToBase64String(bytes),
            UploadedBy = uploadedBy,
            Version = version,
            IsCurrent = true
        });
        await db.SaveChangesAsync();
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.LandlordDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return (false, "Document not found.");
        db.LandlordDocuments.Remove(doc);
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Decodes a document's Base64 content back to bytes for preview / download.</summary>
    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.LandlordDocuments
            .Where(d => d.Id == documentId)
            .Select(d => new { d.ContentBase64, d.ContentType, d.FileName })
            .FirstOrDefaultAsync();
        if (doc?.ContentBase64 is null) return null;
        return (Convert.FromBase64String(doc.ContentBase64), doc.ContentType, doc.FileName);
    }

    /// <summary>Projects metadata without the heavy Base64 blob, for list views.</summary>
    private static LandlordDocument Meta(LandlordDocument d) => new()
    {
        Id = d.Id, LandlordId = d.LandlordId, DocumentType = d.DocumentType, FileName = d.FileName,
        StoredFileName = d.StoredFileName, ContentType = d.ContentType, FileSizeBytes = d.FileSizeBytes,
        UploadedAt = d.UploadedAt, UploadedBy = d.UploadedBy, ContactId = d.ContactId,
        SignatoryId = d.SignatoryId, Version = d.Version, IsCurrent = d.IsCurrent
    };
}
