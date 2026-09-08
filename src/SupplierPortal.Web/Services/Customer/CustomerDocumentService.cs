using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Manages customer KYC document uploads: the master document-type checklist and the uploaded
/// files (stored in the database on <see cref="CustomerDocument"/>). Uploading a type replaces any prior
/// file of that type for the customer.</summary>
public class CustomerDocumentService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public const long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

    /// <summary>The active KYC document types (the master checklist), in display order.</summary>
    public async Task<List<CustomerDocumentType>> GetTypesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerDocumentTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Sequence).ThenBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>Uploaded documents for a customer (without the file bytes).</summary>
    public async Task<List<CustomerDocument>> GetDocumentsAsync(int customerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerDocuments.AsNoTracking()
            .Where(d => d.CustomerId == customerId)
            .Select(d => new CustomerDocument
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                ContentType = d.ContentType,
                UploadedBy = d.UploadedBy,
                UploadedAt = d.UploadedAt
            })
            .OrderBy(d => d.DocumentType)
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error)> UploadAsync(
        int customerId, string documentType, IBrowserFile file, string uploadedBy)
    {
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return (false, "Only PDF, JPG or PNG files are allowed.");
        }
        if (file.Size > MaxFileSize)
        {
            return (false, $"'{file.Name}' is over the {MaxFileSize / (1024 * 1024)} MB limit.");
        }

        byte[] bytes;
        try
        {
            await using var stream = file.OpenReadStream(MaxFileSize);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            bytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            return (false, $"Could not read the file: {ex.Message}");
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        // Replace any existing document of the same type for this customer.
        var existing = await db.CustomerDocuments
            .Where(d => d.CustomerId == customerId && d.DocumentType == documentType).ToListAsync();
        if (existing.Count > 0)
        {
            db.CustomerDocuments.RemoveRange(existing);
        }
        db.CustomerDocuments.Add(new CustomerDocument
        {
            CustomerId = customerId,
            DocumentType = documentType,
            FileName = file.Name,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Content = bytes,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await audit.LogAsync(uploadedBy, AuditAction.Create, "CustomerDocument", customerId,
            $"Uploaded {documentType}: {file.Name}");
        return (true, null);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.CustomerDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        return doc is null ? null : (doc.Content, doc.ContentType, doc.FileName);
    }

    public async Task DeleteAsync(int documentId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.CustomerDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null)
        {
            return;
        }
        var customerId = doc.CustomerId;
        var type = doc.DocumentType;
        db.CustomerDocuments.Remove(doc);
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Delete, "CustomerDocument", customerId, $"Removed {type}");
    }
}
