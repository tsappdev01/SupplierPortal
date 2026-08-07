using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Stores landlord-uploaded documents (Emirates ID, Trade Licence, MOA, POA, title deed, …) as
/// physical files under the content root — not wwwroot, so they are never directly web-servable — with only
/// their metadata in the database. All documents are retained for the complete registration trace.</summary>
public class LandlordDocumentService(IDbContextFactory<LeasingDbContext> dbFactory, IWebHostEnvironment env)
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Accepted upload types (FRD §12 — supported file type).</summary>
    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff"];

    private string DocumentsDirectory(int landlordId) =>
        Path.Combine(env.ContentRootPath, "doc", "landlord", landlordId.ToString());

    /// <summary>Current (live) documents only — replaced versions are hidden here but kept for the audit trail.</summary>
    public async Task<List<LandlordDocument>> GetDocumentsAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId && d.IsCurrent)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.UploadedAt)
            .ToListAsync();
    }

    /// <summary>Every document ever uploaded, including superseded versions, for the review history.</summary>
    public async Task<List<LandlordDocument>> GetAllVersionsAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId)
            .OrderBy(d => d.DocumentType).ThenByDescending(d => d.UploadedAt)
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

        var directory = DocumentsDirectory(landlordId);
        Directory.CreateDirectory(directory);
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(directory, storedFileName);

        await using (var source = file.OpenReadStream(MaxFileSizeBytes))
        await using (var destination = new FileStream(fullPath, FileMode.Create))
        {
            await source.CopyToAsync(destination);
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        // Single-slot identity documents (Emirates ID, Trade Licence, MOA, POA, title deed) supersede the
        // previous upload — the old row is retained (IsCurrent = false) for the audit history; "Other"
        // documents accumulate. Scope the slot to the contact where one is given.
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
            StoredFileName = storedFileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Size,
            UploadedBy = uploadedBy,
            ContactId = contactId,
            SignatoryId = signatoryId,
            Version = version,
            IsCurrent = true
        });
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.LandlordDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return (false, "Document not found.");
        DeletePhysicalFile(doc.LandlordId, doc.StoredFileName);
        db.LandlordDocuments.Remove(doc);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.LandlordDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return null;
        var fullPath = Path.Combine(DocumentsDirectory(doc.LandlordId), doc.StoredFileName);
        if (!File.Exists(fullPath)) return null;
        return (await File.ReadAllBytesAsync(fullPath), doc.ContentType, doc.FileName);
    }

    private void DeletePhysicalFile(int landlordId, string storedFileName)
    {
        var path = Path.Combine(DocumentsDirectory(landlordId), storedFileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
