using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Admin/leasing-manager side of the Landlord module: creates the owner "shell" (business-set core
/// contact), assigns premises from the read-only land view, issues the tokenised self-service invite,
/// and records the immutable registration trace. The landlord-updated fields and the review workflow
/// are exercised through <see cref="LandlordSelfService"/> / the review screen.
/// </summary>
public class LandlordService(IDbContextFactory<LeasingDbContext> dbFactory)
{
    private static readonly TimeSpan LinkValidity = TimeSpan.FromDays(30);

    // ---- Registration-trace event types (kept as constants so the timeline and code can't drift) ----
    public const string EventCreated = "Created";
    public const string EventPremisesAssigned = "PremisesAssigned";
    public const string EventPremisesRemoved = "PremisesRemoved";
    public const string EventInvited = "Invited";
    public const string EventSubmitted = "Submitted";
    public const string EventApproved = "Approved";
    public const string EventRejected = "Rejected";

    public async Task<List<LandlordListItem>> GetAllAsync(int? businessUnitId = null, LandlordStatus? status = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Landlords.AsNoTracking().AsQueryable();
        if (businessUnitId is int bu) query = query.Where(l => l.BusinessUnitId == bu);
        if (status is LandlordStatus s) query = query.Where(l => l.Status == s);

        return await query
            .OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)
            .Select(l => new LandlordListItem(
                l.Id, l.LegalName, l.Type, l.Status, l.BusinessUnitId,
                l.Reference, l.Premises.Count, l.UpdatedAt ?? l.CreatedAt))
            .ToListAsync();
    }

    public async Task<Landlord?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Landlords
            .Include(l => l.Premises)
            .Include(l => l.Signatories)
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<List<LandlordFieldChange>> GetFieldChangesAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordFieldChanges.AsNoTracking()
            .Where(c => c.LandlordId == landlordId)
            .OrderByDescending(c => c.ChangedAt)
            .ToListAsync();
    }

    public async Task<List<LandlordEvent>> GetTraceAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordEvents.AsNoTracking()
            .Where(e => e.LandlordId == landlordId)
            .OrderByDescending(e => e.At)
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error, int Id)> CreateAsync(
        LandlordType type, string legalName, string primaryContactName, string email, string mobile,
        int businessUnitId, string actor)
    {
        legalName = legalName.Trim();
        email = email.Trim();
        if (string.IsNullOrWhiteSpace(legalName)) return (false, "Legal name is required.", 0);
        if (string.IsNullOrWhiteSpace(email)) return (false, "Primary contact email is required.", 0);

        await using var db = await dbFactory.CreateDbContextAsync();
        var landlord = new Landlord
        {
            Type = type,
            LegalName = legalName,
            PrimaryContactName = primaryContactName.Trim(),
            Email = email,
            Mobile = mobile.Trim(),
            BusinessUnitId = businessUnitId,
            Status = LandlordStatus.Draft,
            CreatedBy = actor,
            CreatedAt = DateTime.UtcNow
        };
        db.Landlords.Add(landlord);
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlord.Id, EventCreated, $"Owner shell created for {legalName}", actor);
        return (true, null, landlord.Id);
    }

    public async Task<(bool Success, string? Error)> UpdateBusinessFieldsAsync(
        int id, LandlordType type, string legalName, string primaryContactName, string email, string mobile, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return (false, "Landlord not found.");

        l.Type = type;
        l.LegalName = legalName.Trim();
        l.PrimaryContactName = primaryContactName.Trim();
        l.Email = email.Trim();
        l.Mobile = mobile.Trim();
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Assigns a premises (a <c>dbo.Property</c> view row, by Id) to the landlord, snapshotting the
    /// code/building/zone. Idempotent on the unique (LandlordId, PremisesId) index.</summary>
    public async Task<(bool Success, string? Error)> AssignPremisesAsync(int landlordId, int premisesId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var landlord = await db.Landlords.FirstOrDefaultAsync(l => l.Id == landlordId);
        if (landlord is null) return (false, "Landlord not found.");

        if (await db.LandlordPremises.AnyAsync(p => p.LandlordId == landlordId && p.PremisesId == premisesId))
            return (false, "That property is already assigned to this landlord.");

        var premises = await db.Premises.AsNoTracking().FirstOrDefaultAsync(p => p.Id == premisesId);
        if (premises is null) return (false, "Property not found in the land register.");

        db.LandlordPremises.Add(new LandlordPremises
        {
            LandlordId = landlordId,
            PremisesId = premisesId,
            PropertyCode = premises.PropertyCode,
            BuildingName = premises.BuildingName,
            Zone = premises.Zone,
            LinkedBy = actor
        });
        landlord.UpdatedBy = actor;
        landlord.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventPremisesAssigned,
            $"Assigned {premises.PropertyCode}{(string.IsNullOrWhiteSpace(premises.BuildingName) ? "" : $" / {premises.BuildingName}")}", actor);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> RemovePremisesAsync(int landlordId, int landlordPremisesId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = await db.LandlordPremises.FirstOrDefaultAsync(p => p.Id == landlordPremisesId && p.LandlordId == landlordId);
        if (link is null) return (false, "Assignment not found.");

        db.LandlordPremises.Remove(link);
        await db.SaveChangesAsync();
        await AddEventAsync(db, landlordId, EventPremisesRemoved, $"Removed {link.PropertyCode}", actor);
        return (true, null);
    }

    /// <summary>Issues a fresh self-service invite link and moves the landlord to
    /// <see cref="LandlordStatus.AwaitingLandlordResponse"/>. Returns the raw (unhashed) token to embed in the URL.</summary>
    public async Task<(bool Success, string? Error, string? RawToken)> InviteAsync(int landlordId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var landlord = await db.Landlords.Include(l => l.Premises).FirstOrDefaultAsync(l => l.Id == landlordId);
        if (landlord is null) return (false, "Landlord not found.", null);
        if (string.IsNullOrWhiteSpace(landlord.Email)) return (false, "This landlord has no primary contact email.", null);
        if (landlord.Premises.Count == 0) return (false, "Assign at least one property before inviting the landlord.", null);

        var raw = GenerateRawToken();
        db.LandlordAccessTokens.Add(new LandlordAccessToken
        {
            LandlordId = landlordId,
            TokenHash = Hash(raw),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(LinkValidity),
            CreatedBy = actor
        });

        if (landlord.Status is LandlordStatus.Draft)
            landlord.Status = LandlordStatus.AwaitingLandlordResponse;
        landlord.UpdatedBy = actor;
        landlord.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventInvited, $"Invite link issued to {landlord.Email}", actor);
        return (true, null, raw);
    }

    /// <summary>Loads the landlord for the self-service session (premises + signatories + documents).</summary>
    public async Task<Landlord?> GetForSelfServiceAsync(int landlordId) => await GetByIdAsync(landlordId);

    /// <summary>
    /// Applies the landlord's self-service update: identity fields, secondary contact, signing rule and
    /// signatories. Records a field-change row per changed value (for the review highlight / trace), moves
    /// the record to <see cref="LandlordStatus.UnderReview"/>, stamps a reference on first submission, and
    /// writes the Submitted trace event. The business-set core contact is never overwritten here.
    /// </summary>
    public async Task<(bool Success, string? Error, string? Reference)> SubmitAsync(int landlordId, LandlordSelfUpdate update, string actor, int? accessTokenId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.Include(x => x.Signatories).FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return (false, "Landlord not found.", null);

        var changes = new List<LandlordFieldChange>();
        void Track(string key, string label, string? oldVal, string? newVal)
        {
            if ((oldVal ?? "") == (newVal ?? "")) return;
            changes.Add(new LandlordFieldChange
            {
                LandlordId = landlordId, LandlordAccessTokenId = accessTokenId,
                FieldKey = key, DisplayLabel = label, OldValue = oldVal, NewValue = newVal,
                ChangedBy = actor
            });
        }

        Track(nameof(l.EmiratesId), "Emirates ID", l.EmiratesId, update.EmiratesId);
        Track(nameof(l.TradeLicenseNo), "Trade Licence No", l.TradeLicenseNo, update.TradeLicenseNo);
        Track(nameof(l.TaxRegistrationNo), "Tax Registration No", l.TaxRegistrationNo, update.TaxRegistrationNo);
        Track(nameof(l.SecondaryContactName), "Secondary Contact Name", l.SecondaryContactName, update.SecondaryContactName);
        Track(nameof(l.SecondaryContactEmail), "Secondary Contact Email", l.SecondaryContactEmail, update.SecondaryContactEmail);
        Track(nameof(l.SecondaryContactMobile), "Secondary Contact Mobile", l.SecondaryContactMobile, update.SecondaryContactMobile);
        Track(nameof(l.SigningRule), "Signing Rule", l.SigningRule.ToString(), update.SigningRule.ToString());

        l.EmiratesId = update.EmiratesId?.Trim();
        l.TradeLicenseNo = update.TradeLicenseNo?.Trim();
        l.TaxRegistrationNo = update.TaxRegistrationNo?.Trim();
        l.SecondaryContactName = update.SecondaryContactName?.Trim();
        l.SecondaryContactEmail = update.SecondaryContactEmail?.Trim();
        l.SecondaryContactMobile = update.SecondaryContactMobile?.Trim();
        l.SigningRule = update.SigningRule;

        // Replace signatories with the submitted set (primary contact is kept as the ordered first entry).
        db.LandlordSignatories.RemoveRange(l.Signatories);
        var order = 0;
        foreach (var s in update.Signatories.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
        {
            l.Signatories.Add(new LandlordSignatory
            {
                LandlordId = landlordId,
                Name = s.Name.Trim(),
                Email = s.Email.Trim(),
                Mobile = s.Mobile?.Trim(),
                Order = order++,
                IsPrimaryContact = s.IsPrimaryContact
            });
        }

        l.Status = LandlordStatus.UnderReview;
        l.SubmittedAt = DateTime.UtcNow;
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        l.Reference ??= $"DIP-LR-{DateTime.UtcNow:yyyy}-{100000 + landlordId}";

        db.LandlordFieldChanges.AddRange(changes);
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventSubmitted,
            $"{changes.Count} field{(changes.Count == 1 ? "" : "s")} updated; reference {l.Reference}", actor);
        return (true, null, l.Reference);
    }

    /// <summary>Records a leasing-manager decision (Approve / Reject) with a note.</summary>
    public async Task<(bool Success, string? Error)> DecideAsync(int landlordId, bool approve, string? note, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var landlord = await db.Landlords.FirstOrDefaultAsync(l => l.Id == landlordId);
        if (landlord is null) return (false, "Landlord not found.");
        if (landlord.Status != LandlordStatus.UnderReview)
            return (false, "Only submissions under review can be approved or rejected.");

        landlord.Status = approve ? LandlordStatus.Approved : LandlordStatus.Rejected;
        landlord.ReviewedBy = actor;
        landlord.ReviewedAt = DateTime.UtcNow;
        landlord.DecisionNote = note?.Trim();
        landlord.UpdatedBy = actor;
        landlord.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, approve ? EventApproved : EventRejected,
            string.IsNullOrWhiteSpace(note) ? null : note, actor);
        return (true, null);
    }

    private static async Task AddEventAsync(LeasingDbContext db, int landlordId, string type, string? detail, string actor)
    {
        db.LandlordEvents.Add(new LandlordEvent { LandlordId = landlordId, EventType = type, Detail = detail, By = actor });
        await db.SaveChangesAsync();
    }

    private static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}

public record LandlordListItem(
    int Id, string LegalName, LandlordType Type, LandlordStatus Status, int BusinessUnitId,
    string? Reference, int PremisesCount, DateTime UpdatedAt);

/// <summary>The landlord-updated fields captured in the self-service wizard.</summary>
public class LandlordSelfUpdate
{
    public string? EmiratesId { get; set; }
    public string? TradeLicenseNo { get; set; }
    public string? TaxRegistrationNo { get; set; }
    public string? SecondaryContactName { get; set; }
    public string? SecondaryContactEmail { get; set; }
    public string? SecondaryContactMobile { get; set; }
    public SigningRule SigningRule { get; set; } = SigningRule.All;
    public List<SignatoryInput> Signatories { get; set; } = [];
}

public class SignatoryInput
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Mobile { get; set; }
    public bool IsPrimaryContact { get; set; }
}
