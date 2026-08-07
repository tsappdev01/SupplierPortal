using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Business-Centre + workflow side of the Landlord Registration module (FRD). Initiates a registration from
/// a property (snapshotting the read-only Property-View fields and pulling the invitation email from the
/// view), issues the secure invite, and drives the review lifecycle: submit → review → correction / reject
/// (with mandatory reason) / complete, plus restart-after-rejection. Every transition writes an audit event.
/// </summary>
public class LandlordService(IDbContextFactory<LeasingDbContext> dbFactory, LandlordEmailService email)
{
    private static readonly TimeSpan LinkValidity = TimeSpan.FromDays(30);

    // Registration-trace / audit event types (FRD §29).
    public const string EventCreated = "RegistrationCreated";
    public const string EventInvited = "InvitationSent";
    public const string EventOtpVerified = "OtpVerified";
    public const string EventDraftSaved = "DraftSaved";
    public const string EventSubmitted = "RegistrationSubmitted";
    public const string EventReviewStarted = "ReviewStarted";
    public const string EventCorrectionRequested = "CorrectionRequested";
    public const string EventResubmitted = "RegistrationResubmitted";
    public const string EventRejected = "RegistrationRejected";
    public const string EventRestarted = "RegistrationRestarted";
    public const string EventCompleted = "RegistrationCompleted";
    public const string EventLinkDeactivated = "LinkDeactivated";

    public const string ActorBusinessCentre = "BusinessCentre";
    public const string ActorLandlord = "Landlord";
    public const string ActorSystem = "System";

    // ---------------------------------------------------------------- queries

    public async Task<List<LandlordListItem>> GetAllAsync(int? businessUnitId = null, LandlordStatus? status = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Landlords.AsNoTracking().AsQueryable();
        if (businessUnitId is int bu) query = query.Where(l => l.BusinessUnitId == bu);
        if (status is LandlordStatus s) query = query.Where(l => l.Status == s);

        return await query
            .OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)
            .Select(l => new LandlordListItem(
                l.Id, l.CompanyName ?? l.PropertyCode, l.PropertyCode, l.Type, l.Status, l.BusinessUnitId,
                l.Email, l.Reference, l.Premises.Count, l.LinkActive, l.UpdatedAt ?? l.CreatedAt))
            .ToListAsync();
    }

    public async Task<Landlord?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Landlords
            .Include(l => l.Premises)
            .Include(l => l.Contacts)
            .Include(l => l.Signatories)
            .Include(l => l.Documents)
            .Include(l => l.Comments)
            .FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task<Landlord?> GetForSelfServiceAsync(int landlordId) => await GetByIdAsync(landlordId);

    public async Task<List<LandlordEvent>> GetTraceAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordEvents.AsNoTracking()
            .Where(e => e.LandlordId == landlordId)
            .OrderByDescending(e => e.At)
            .ToListAsync();
    }

    /// <summary>The current open correction comment, if the landlord has an outstanding correction to address.</summary>
    public async Task<LandlordRegistrationComment?> GetOpenCorrectionAsync(int landlordId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LandlordComments.AsNoTracking()
            .Where(c => c.LandlordId == landlordId && c.CommentType == LandlordCommentType.Correction && !c.IsResolved)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }

    // ---------------------------------------------------------------- initiation

    /// <summary>Initiates a registration from a property (FRD §3): snapshots the Property-View fields and,
    /// crucially, the invitation email from the view's Email column. Fails if the property has no email.</summary>
    public async Task<(bool Success, string? Error, int Id)> InitiateFromPropertyAsync(
        int premisesId, LandlordType type, int businessUnitId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var premises = await db.Premises.AsNoTracking().FirstOrDefaultAsync(p => p.Id == premisesId);
        if (premises is null) return (false, "Property not found in the land register.", 0);
        if (string.IsNullOrWhiteSpace(premises.EmailAddress))
            return (false, "This property has no email on file in the Property View; a registration cannot be initiated.", 0);

        var landlord = new Landlord
        {
            BusinessUnitId = businessUnitId,
            Type = type,
            PropertyId = premises.Id,
            PropertyCode = premises.PropertyCode,
            CompanyName = premises.CompanyName,
            Building = premises.BuildingName,
            Email = premises.EmailAddress!.Trim(),
            LandLordCode = premises.LandLordCode,
            Status = LandlordStatus.Draft,
            LinkActive = true,
            CreatedBy = actor,
            CreatedAt = DateTime.UtcNow
        };
        landlord.Premises.Add(new LandlordPremises
        {
            PremisesId = premises.Id, PropertyCode = premises.PropertyCode,
            BuildingName = premises.BuildingName, Zone = premises.Zone, LinkedBy = actor
        });
        db.Landlords.Add(landlord);
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlord.Id, EventCreated,
            $"Registration initiated for {premises.PropertyCode} ({premises.CompanyName}); invite email {landlord.Email}",
            actor, ActorBusinessCentre, null, LandlordStatus.Draft);
        return (true, null, landlord.Id);
    }

    /// <summary>Business Centre may correct the read-only Property Information / type before sending the invite.</summary>
    public async Task<(bool Success, string? Error)> UpdatePropertyInfoAsync(
        int id, LandlordType type, string? companyName, string? building, string email, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return (false, "Registration not found.");
        if (string.IsNullOrWhiteSpace(email)) return (false, "Invitation email is required.");

        l.Type = type;
        l.CompanyName = companyName?.Trim();
        l.Building = building?.Trim();
        l.Email = email.Trim();
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    // ---------------------------------------------------------------- invite

    /// <summary>Issues a fresh secure link, activates it, moves Draft/CorrectionRequired → InvitationSent
    /// and sends the invitation email to the Property-View address. Returns the raw token for the URL.</summary>
    public async Task<(bool Success, string? Error, string? RawToken)> InviteAsync(int landlordId, string actor, string? baseUrl = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var landlord = await db.Landlords.Include(l => l.Premises).FirstOrDefaultAsync(l => l.Id == landlordId);
        if (landlord is null) return (false, "Registration not found.", null);
        if (string.IsNullOrWhiteSpace(landlord.Email)) return (false, "This registration has no invitation email.", null);
        if (landlord.Status is LandlordStatus.RegistrationComplete or LandlordStatus.Rejected)
            return (false, "This registration is closed; restart it to issue a new link.", null);

        var raw = GenerateRawToken();
        db.LandlordAccessTokens.Add(new LandlordAccessToken
        {
            LandlordId = landlordId, TokenHash = Hash(raw),
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.Add(LinkValidity), CreatedBy = actor
        });

        var old = landlord.Status;
        if (landlord.Status is LandlordStatus.Draft) landlord.Status = LandlordStatus.InvitationSent;
        landlord.LinkActive = true;
        landlord.UpdatedBy = actor;
        landlord.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventInvited, $"Invite link issued to {landlord.Email}", actor, ActorBusinessCentre, old, landlord.Status);

        if (baseUrl is not null)
            await email.SendInviteAsync(landlord.Email, landlord.CompanyName ?? landlord.PropertyCode, $"{baseUrl}landlord/{raw}");
        return (true, null, raw);
    }

    // ---------------------------------------------------------------- landlord self-service

    /// <summary>Persists the landlord's contacts / signatories (documents are handled by
    /// <see cref="LandlordDocumentService"/>) either as a draft (InProgress) or as a submission.</summary>
    public async Task<(bool Success, string? Error, string? Reference)> SaveAsync(
        int landlordId, LandlordSelfUpdate update, bool submit, string actor, int? accessTokenId, string? ip)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.Include(x => x.Contacts).Include(x => x.Signatories).Include(x => x.Comments)
            .FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return (false, "Registration not found.", null);

        // Replace contacts with the submitted set (first primary).
        db.LandlordContacts.RemoveRange(l.Contacts);
        var contacts = update.Contacts.Where(c => !string.IsNullOrWhiteSpace(c.FullName)).ToList();
        foreach (var c in contacts)
        {
            l.Contacts.Add(new LandlordContact
            {
                LandlordId = landlordId, IsPrimary = c.IsPrimary, ContactType = c.ContactType,
                FullName = c.FullName.Trim(), Designation = c.Designation?.Trim(), Mobile = c.Mobile?.Trim(),
                Email = c.Email.Trim(), EmiratesId = c.EmiratesId?.Trim()
            });
        }

        db.LandlordSignatories.RemoveRange(l.Signatories);
        var order = 0;
        foreach (var s in update.Signatories.Where(s => !string.IsNullOrWhiteSpace(s.Name)))
        {
            l.Signatories.Add(new LandlordSignatory
            {
                LandlordId = landlordId, Name = s.Name.Trim(), Email = s.Email.Trim(),
                Mobile = s.Mobile?.Trim(), Order = order++, IsPrimaryContact = s.IsPrimaryContact
            });
        }
        l.SigningRule = update.SigningRule;

        var old = l.Status;
        if (submit)
        {
            l.Status = LandlordStatus.Submitted;
            l.SubmittedAt = DateTime.UtcNow;
            l.Reference ??= $"DIP-LR-{DateTime.UtcNow:yyyy}-{100000 + landlordId}";
            // Resolve any open correction now that the landlord has resubmitted.
            foreach (var open in l.Comments.Where(c => c.CommentType == LandlordCommentType.Correction && !c.IsResolved))
                open.IsResolved = true;
        }
        else if (l.Status is LandlordStatus.InvitationSent or LandlordStatus.OtpVerified)
        {
            l.Status = LandlordStatus.InProgress;
        }
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var wasResubmit = submit && old == LandlordStatus.CorrectionRequired;
        await AddEventAsync(db, landlordId,
            submit ? (wasResubmit ? EventResubmitted : EventSubmitted) : EventDraftSaved,
            submit ? $"Submitted; reference {l.Reference}" : "Draft saved",
            actor, ActorLandlord, old, l.Status, ip);

        return (true, null, l.Reference);
    }

    /// <summary>Records that the landlord passed OTP (once), moving InvitationSent → OtpVerified.</summary>
    public async Task MarkOtpVerifiedAsync(int landlordId, string actor, string? ip)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return;
        var old = l.Status;
        if (l.Status is LandlordStatus.InvitationSent) l.Status = LandlordStatus.OtpVerified;
        await db.SaveChangesAsync();
        await AddEventAsync(db, landlordId, EventOtpVerified, "OTP verified", actor, ActorLandlord, old, l.Status, ip);
    }

    // ---------------------------------------------------------------- business-centre review

    public async Task StartReviewAsync(int landlordId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null || l.Status != LandlordStatus.Submitted) return;
        l.Status = LandlordStatus.UnderReview;
        l.ReviewedBy = actor;
        l.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await AddEventAsync(db, landlordId, EventReviewStarted, null, actor, ActorBusinessCentre, LandlordStatus.Submitted, LandlordStatus.UnderReview);
    }

    /// <summary>Request re-upload / correction (FRD §18). Comment is mandatory, recorded, and emailed to the landlord.</summary>
    public async Task<(bool Success, string? Error)> RequestCorrectionAsync(int landlordId, string comment, string actor, string? baseUrl, string? ip)
    {
        if (string.IsNullOrWhiteSpace(comment)) return (false, "A correction comment is required.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return (false, "Registration not found.");
        if (l.Status is not (LandlordStatus.Submitted or LandlordStatus.UnderReview))
            return (false, "Only a submitted registration can be sent back for correction.");

        var old = l.Status;
        l.Status = LandlordStatus.CorrectionRequired;
        l.LinkActive = true;
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        db.LandlordComments.Add(new LandlordRegistrationComment
        {
            LandlordId = landlordId, CommentType = LandlordCommentType.Correction, Comment = comment.Trim(), CreatedBy = actor
        });
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventCorrectionRequested, comment.Trim(), actor, ActorBusinessCentre, old, l.Status, ip);

        var raw = await ReissueTokenIfNeededAsync(db, landlordId, actor);
        if (baseUrl is not null && raw is not null)
            await email.SendCorrectionAsync(l.Email, l.PropertyCode, comment.Trim(), $"{baseUrl}landlord/{raw}");
        return (true, null);
    }

    /// <summary>Reject (FRD §21). Reason is mandatory; the link is deactivated.</summary>
    public async Task<(bool Success, string? Error)> RejectAsync(int landlordId, string reason, string actor, string? ip)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "A rejection reason is required.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return (false, "Registration not found.");
        if (l.Status is not (LandlordStatus.Submitted or LandlordStatus.UnderReview))
            return (false, "Only a submitted registration can be rejected.");

        var old = l.Status;
        l.Status = LandlordStatus.Rejected;
        l.RejectionReason = reason.Trim();
        l.RejectedBy = actor;
        l.RejectedAt = DateTime.UtcNow;
        l.LinkActive = false;
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        db.LandlordComments.Add(new LandlordRegistrationComment
        {
            LandlordId = landlordId, CommentType = LandlordCommentType.Rejection, Comment = reason.Trim(), CreatedBy = actor
        });
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventRejected, reason.Trim(), actor, ActorBusinessCentre, old, l.Status, ip);
        await AddEventAsync(db, landlordId, EventLinkDeactivated, null, actor, ActorSystem, l.Status, l.Status);
        await email.SendRejectionAsync(l.Email, l.PropertyCode, reason.Trim());
        return (true, null);
    }

    /// <summary>Restart after rejection (FRD §23): a new registration copying the property snapshot + type,
    /// linked back to the rejected one which is retained as history. Returns the new registration id.</summary>
    public async Task<(bool Success, string? Error, int Id)> RestartAsync(int landlordId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var old = await db.Landlords.Include(l => l.Premises).FirstOrDefaultAsync(l => l.Id == landlordId);
        if (old is null) return (false, "Registration not found.", 0);
        if (old.Status != LandlordStatus.Rejected)
            return (false, "Only a rejected registration can be restarted.", 0);

        var fresh = new Landlord
        {
            BusinessUnitId = old.BusinessUnitId, Type = old.Type, PropertyId = old.PropertyId,
            PropertyCode = old.PropertyCode, CompanyName = old.CompanyName, Building = old.Building,
            Email = old.Email, LandLordCode = old.LandLordCode, Status = LandlordStatus.Draft,
            LinkActive = true, CreatedBy = actor, CreatedAt = DateTime.UtcNow, RestartedFromId = old.Id
        };
        foreach (var p in old.Premises)
            fresh.Premises.Add(new LandlordPremises { PremisesId = p.PremisesId, PropertyCode = p.PropertyCode, BuildingName = p.BuildingName, Zone = p.Zone, LinkedBy = actor });
        db.Landlords.Add(fresh);
        await db.SaveChangesAsync();

        await AddEventAsync(db, old.Id, EventRestarted, $"Restarted as registration #{fresh.Id}", actor, ActorBusinessCentre, LandlordStatus.Rejected, LandlordStatus.Rejected);
        await AddEventAsync(db, fresh.Id, EventCreated, $"Restarted from rejected registration #{old.Id}", actor, ActorBusinessCentre, null, LandlordStatus.Draft);
        return (true, null, fresh.Id);
    }

    /// <summary>Complete / approve (FRD §24): status → RegistrationComplete, link deactivated, and completion
    /// notifications sent to the landlord and internal staff.</summary>
    public async Task<(bool Success, string? Error)> CompleteAsync(int landlordId, string actor, IEnumerable<string> staffEmails, string? ip)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var l = await db.Landlords.FirstOrDefaultAsync(x => x.Id == landlordId);
        if (l is null) return (false, "Registration not found.");
        if (l.Status is not (LandlordStatus.Submitted or LandlordStatus.UnderReview))
            return (false, "Only a submitted registration can be completed.");

        var old = l.Status;
        l.Status = LandlordStatus.RegistrationComplete;
        l.CompletedBy = actor;
        l.CompletedAt = DateTime.UtcNow;
        l.LinkActive = false;
        l.UpdatedBy = actor;
        l.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await AddEventAsync(db, landlordId, EventCompleted, "Registration completed", actor, ActorBusinessCentre, old, l.Status, ip);
        await AddEventAsync(db, landlordId, EventLinkDeactivated, null, actor, ActorSystem, l.Status, l.Status);

        await email.SendCompletionToLandlordAsync(l.Email, l.PropertyCode);
        await email.SendCompletionToStaffAsync(staffEmails, l.PropertyCode, l.CompanyName, actor, l.CompletedAt!.Value);
        return (true, null);
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<string?> ReissueTokenIfNeededAsync(LeasingDbContext db, int landlordId, string actor)
    {
        var raw = GenerateRawToken();
        db.LandlordAccessTokens.Add(new LandlordAccessToken
        {
            LandlordId = landlordId, TokenHash = Hash(raw),
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.Add(LinkValidity), CreatedBy = actor
        });
        await db.SaveChangesAsync();
        return raw;
    }

    private static async Task AddEventAsync(LeasingDbContext db, int landlordId, string type, string? detail,
        string actor, string actorType, LandlordStatus? oldStatus = null, LandlordStatus? newStatus = null, string? ip = null)
    {
        db.LandlordEvents.Add(new LandlordEvent
        {
            LandlordId = landlordId, EventType = type, Detail = detail, By = actor, ByType = actorType,
            OldStatus = oldStatus?.ToString(), NewStatus = newStatus?.ToString(), Ip = ip
        });
        await db.SaveChangesAsync();
    }

    private static string GenerateRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}

public record LandlordListItem(
    int Id, string DisplayName, string PropertyCode, LandlordType Type, LandlordStatus Status, int BusinessUnitId,
    string Email, string? Reference, int PremisesCount, bool LinkActive, DateTime UpdatedAt);

/// <summary>The landlord-provided data captured in the self-service form.</summary>
public class LandlordSelfUpdate
{
    public SigningRule SigningRule { get; set; } = SigningRule.All;
    public List<ContactInput> Contacts { get; set; } = [];
    public List<SignatoryInput> Signatories { get; set; } = [];
}

public class ContactInput
{
    public int? Id { get; set; }
    public bool IsPrimary { get; set; }
    public ContactType ContactType { get; set; } = ContactType.Owner;
    public string FullName { get; set; } = "";
    public string? Designation { get; set; }
    public string? Mobile { get; set; }
    public string Email { get; set; } = "";
    public string? EmiratesId { get; set; }
}

public class SignatoryInput
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Mobile { get; set; }
    public bool IsPrimaryContact { get; set; }
}
