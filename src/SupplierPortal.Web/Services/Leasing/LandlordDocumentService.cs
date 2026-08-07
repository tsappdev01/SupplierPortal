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

    private string DocumentsDirectory(int landlordId) =>
        Path.Combine(env.ContentRootPath, "doc", "landlord", landlordId.ToString());

    public async Task<List<LandlordDocument>> GetDocumentsAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordDocuments
            .Where(d => d.LandlordId == landlordId)
            .OrderBy(d => d.DocumentType).ThenBy(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> UploadAsync(
        int landlordId, LandlordDocumentType documentType, IBrowserFile file, string uploadedBy, int? signatoryId = null)
    {
        if (file.Size > MaxFileSizeBytes) return (false, "File exceeds the 10 MB size limit.");

        var directory = DocumentsDirectory(landlordId);
        Directory.CreateDirectory(directory);
        var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.Name)}";
        var fullPath = Path.Combine(directory, storedFileName);

        await using (var source = file.OpenReadStream(MaxFileSizeBytes))
        await using (var destination = new FileStream(fullPath, FileMode.Create))
        {
            await source.CopyToAsync(destination);
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        // Single-slot identity documents (Emirates ID, Trade Licence, MOA, POA, title deed) replace the
        // previous upload; "Other" documents accumulate.
        if (documentType != LandlordDocumentType.Other)
        {
            var existing = await db.LandlordDocuments
                .Where(d => d.LandlordId == landlordId && d.DocumentType == documentType).ToListAsync();
            foreach (var old in existing)
            {
                DeletePhysicalFile(landlordId, old.StoredFileName);
                db.LandlordDocuments.Remove(old);
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
            SignatoryId = signatoryId
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
