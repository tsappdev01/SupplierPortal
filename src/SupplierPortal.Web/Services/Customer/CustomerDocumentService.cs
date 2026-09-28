using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Manages customer KYC document uploads: the master document-type checklist and the uploaded
/// files (stored in the database on <see cref="CustomerDocument"/>). Uploading a type replaces any prior
/// file of that type for the customer.</summary>
public class CustomerDocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit,
    SupplierPortal.Web.Services.Storage.IFileStorageService fileStore)
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

    /// <summary>All document types (active and inactive) for the admin management screen, in order.</summary>
    public async Task<List<CustomerDocumentType>> GetAllTypesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerDocumentTypes.AsNoTracking()
            .OrderBy(t => t.Sequence).ThenBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>Applies the admin edits to the document-type master list: updates existing rows, inserts new
    /// ones (Id == 0) and deletes rows the admin removed. Blank names are skipped.</summary>
    public async Task SaveTypesAsync(IReadOnlyList<CustomerDocumentType> edits, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.CustomerDocumentTypes.ToListAsync();

        // Delete rows no longer present in the edit set.
        var keepIds = edits.Where(e => e.Id != 0).Select(e => e.Id).ToHashSet();
        foreach (var row in rows.Where(r => !keepIds.Contains(r.Id)).ToList())
        {
            db.CustomerDocumentTypes.Remove(row);
        }

        foreach (var edit in edits)
        {
            if (string.IsNullOrWhiteSpace(edit.Name))
            {
                continue;
            }
            if (edit.Id != 0 && rows.FirstOrDefault(r => r.Id == edit.Id) is { } row)
            {
                row.Name = edit.Name.Trim();
                row.Requirement = string.IsNullOrWhiteSpace(edit.Requirement) ? null : edit.Requirement.Trim();
                row.IsRequired = edit.IsRequired;
                row.Sequence = edit.Sequence;
                row.IsActive = edit.IsActive;
            }
            else if (edit.Id == 0)
            {
                db.CustomerDocumentTypes.Add(new CustomerDocumentType
                {
                    Name = edit.Name.Trim(),
                    Requirement = string.IsNullOrWhiteSpace(edit.Requirement) ? null : edit.Requirement.Trim(),
                    IsRequired = edit.IsRequired,
                    Sequence = edit.Sequence,
                    IsActive = edit.IsActive
                });
            }
        }

        await db.SaveChangesAsync();
        await audit.LogAsync(actorUsername, AuditAction.Update, "CustomerDocumentType", null,
            "Updated customer document-type master list");
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

        return await UploadBytesAsync(customerId, documentType, bytes, file.Name, file.ContentType, uploadedBy);
    }

    /// <summary>Bytes-based upload path shared by the Blazor upload and the Self-Service API (which receives
    /// the bytes over HTTP). Validates the extension/size, then stores the document (replacing any existing
    /// one of the same type).</summary>
    public async Task<(bool Success, string? Error)> UploadBytesAsync(
        int customerId, string documentType, byte[] bytes, string fileName, string? contentType, string uploadedBy)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return (false, "Only PDF, JPG or PNG files are allowed.");
        }
        if (bytes.LongLength > MaxFileSize)
        {
            return (false, $"'{fileName}' is over the {MaxFileSize / (1024 * 1024)} MB limit.");
        }

        var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

        // When blob storage is enabled, store the bytes in Azure Blob Storage and keep only the key in the
        // database; otherwise the bytes are stored inline in the Content column (existing behaviour).
        string? blobKey = fileStore.Enabled
            ? await fileStore.SaveAsync($"customer/{customerId}", fileName, bytes, resolvedContentType)
            : null;

        await using var db = await dbFactory.CreateDbContextAsync();
        // Replace any existing document of the same type for this customer.
        var existing = await db.CustomerDocuments
            .Where(d => d.CustomerId == customerId && d.DocumentType == documentType).ToListAsync();
        foreach (var old in existing)
        {
            await DeleteStoredFileAsync(old);
        }
        if (existing.Count > 0)
        {
            db.CustomerDocuments.RemoveRange(existing);
        }
        db.CustomerDocuments.Add(new CustomerDocument
        {
            CustomerId = customerId,
            DocumentType = documentType,
            FileName = fileName,
            ContentType = resolvedContentType,
            Content = blobKey is null ? bytes : Array.Empty<byte>(),
            BlobKey = blobKey,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        await audit.LogAsync(uploadedBy, AuditAction.Create, "CustomerDocument", customerId,
            $"Uploaded {documentType}: {fileName}");
        return (true, null);
    }

    /// <summary>Every attachment for a customer as (bytes, file name), for the "download all" zip.</summary>
    public async Task<List<(byte[] Bytes, string FileName)>> GetAllFilesAsync(int customerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.CustomerDocuments.AsNoTracking()
            .Where(d => d.CustomerId == customerId)
            .OrderBy(d => d.DocumentType)
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

    /// <summary>Only the image attachments (bytes + file name), for the "combine images into one PDF" view.</summary>
    public async Task<List<(byte[] Bytes, string FileName)>> GetImageFilesAsync(int customerId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.CustomerDocuments.AsNoTracking()
            .Where(d => d.CustomerId == customerId && d.ContentType.StartsWith("image/"))
            .OrderBy(d => d.DocumentType)
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

    /// <summary>The owning customer id for a document, or null when it does not exist. Lets the download
    /// endpoint enforce per-customer access scope before serving the file.</summary>
    public async Task<int?> GetCustomerIdForDocumentAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerDocuments.Where(d => d.Id == documentId)
            .Select(d => (int?)d.CustomerId).FirstOrDefaultAsync();
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.CustomerDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null)
        {
            return null;
        }
        var bytes = await ReadStoredBytesAsync(doc);
        return bytes is null ? null : (bytes, doc.ContentType, doc.FileName);
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
        await DeleteStoredFileAsync(doc);
        db.CustomerDocuments.Remove(doc);
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Delete, "CustomerDocument", customerId, $"Removed {type}");
    }

    /// <summary>Reads a customer document's bytes from blob storage (when it has a key) or the Content column.</summary>
    private async Task<byte[]?> ReadStoredBytesAsync(CustomerDocument doc)
        => string.IsNullOrWhiteSpace(doc.BlobKey) ? doc.Content : await fileStore.ReadAsync(doc.BlobKey);

    private async Task DeleteStoredFileAsync(CustomerDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.BlobKey))
        {
            await fileStore.DeleteAsync(doc.BlobKey);
        }
    }
}
