using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Persists landlord registrations submitted through the public wizard. Uploaded files are
/// written as physical files under the app's content root (doc/landlord/{id}) — never wwwroot, so
/// they are not directly web-servable — with only their metadata in the database.</summary>
public class LandlordRegistrationService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService)
{
    /// <summary>A file collected in the wizard but not yet persisted — held in memory until the
    /// registration is saved and its Id (and therefore its document folder) exists.</summary>
    public record PendingDocument(string Slot, string FileName, string ContentType, byte[] Content);

    private string DocumentsDirectory(int registrationId) =>
        Path.Combine(env.ContentRootPath, "doc", "landlord", registrationId.ToString());

    public async Task<string> CreateAsync(LandlordRegistration registration, IReadOnlyList<PendingDocument> documents)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        registration.ReferenceNumber = await GenerateReferenceAsync(db);
        registration.SubmittedAt = DateTime.UtcNow;
        registration.Status = LandlordRegistrationStatus.Submitted;

        db.LandlordRegistrations.Add(registration);
        await db.SaveChangesAsync();

        if (documents.Count > 0)
        {
            var directory = DocumentsDirectory(registration.Id);
            Directory.CreateDirectory(directory);

            foreach (var doc in documents)
            {
                var storedFileName = $"{Guid.NewGuid():N}{Path.GetExtension(doc.FileName)}";
                await File.WriteAllBytesAsync(Path.Combine(directory, storedFileName), doc.Content);

                db.LandlordDocuments.Add(new LandlordDocument
                {
                    LandlordRegistrationId = registration.Id,
                    Slot = doc.Slot,
                    FileName = doc.FileName,
                    StoredFileName = storedFileName,
                    ContentType = string.IsNullOrWhiteSpace(doc.ContentType) ? "application/octet-stream" : doc.ContentType,
                    FileSizeBytes = doc.Content.LongLength
                });
            }

            await db.SaveChangesAsync();
        }

        await auditService.LogAsync(
            registration.PrimaryEmail, AuditAction.Create, "LandlordRegistration", registration.Id,
            $"Landlord registration {registration.ReferenceNumber} submitted for property {registration.PropertyCode}");

        return registration.ReferenceNumber;
    }

    public async Task<List<LandlordRegistration>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordRegistrations
            .AsNoTracking()
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<LandlordRegistration?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordRegistrations
            .AsNoTracking()
            .Include(r => r.Signatories.OrderBy(s => s.Sequence))
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetDocumentFileAsync(int documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.LandlordDocuments.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document is null)
        {
            return null;
        }

        var fullPath = Path.Combine(DocumentsDirectory(document.LandlordRegistrationId), document.StoredFileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath);
        return (bytes, document.ContentType, document.FileName);
    }

    // DIP-LR-{year}-{6-digit sequence}, sequence scoped per calendar year.
    private static async Task<string> GenerateReferenceAsync(ApplicationDbContext db)
    {
        var prefix = $"DIP-LR-{DateTime.UtcNow.Year}-";
        var count = await db.LandlordRegistrations.CountAsync(r => r.ReferenceNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D6}";
    }
}
