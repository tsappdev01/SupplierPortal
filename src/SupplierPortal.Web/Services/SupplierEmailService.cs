using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Sends supplier notification emails via SQL Server Database Mail (sp_send_dbmail),
/// using the "BP" Database Mail profile configured on the SQL Server instance.</summary>
public class SupplierEmailService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService,
    SupplierAccessTokenService accessTokenService, UserService userService, IConfiguration configuration,
    EmailBrandingService branding)
{
    /// <summary>Renders the associated Business Unit name as the email header brand line (text renders
    /// reliably in every email client, unlike an embedded image).</summary>
    private static string BrandLine(string? name) =>
        $"<div style=\"color:#0b2545;font-size:16px;font-weight:700;letter-spacing:.02em;margin-bottom:10px\">{System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? EmailBrandingService.DefaultBrandName : name)}</div>";

    /// <summary>The email header brand block: the respective Business Unit's <b>logo</b> when one is on
    /// file (its hosted Logo URL, or the uploaded logo embedded as a base64 data URI). The logo alone is
    /// shown - the brand name is not repeated as text underneath it. Falls back to the text-only brand
    /// line when the unit has no logo.</summary>
    private static string BrandBlock(string? logoSrc, string? name)
    {
        if (string.IsNullOrWhiteSpace(logoSrc))
        {
            return BrandLine(name);
        }

        var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? EmailBrandingService.DefaultBrandName : name);
        return $"<img src=\"{logoSrc}\" alt=\"{safeName}\" style=\"max-height:48px;max-width:220px;display:block\" />";
    }

    /// <summary>Resolves the logo for a record's Business Unit first (the "respective" unit the user asked
    /// to see on the email) — its hosted Logo URL when set, else the embedded logo — falling back to the
    /// acting user's company logo.</summary>
    private async Task<string?> BusinessUnitLogoAsync(int? businessUnitId, string actorUsername) =>
        await branding.GetBusinessUnitLogoSrcAsync(businessUnitId)
        ?? await branding.GetLogoSrcAsync(actorUsername, businessUnitId);

    /// <summary>Picks the first non-empty self-service base from the candidates (business unit domain,
    /// global setting, request base) and turns a bare host (no scheme) into an https:// URL.</summary>
    public static string SelfServiceLinkBase(params string?[] candidates)
    {
        var chosen = candidates.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u))?.Trim() ?? "";
        if (chosen.Length == 0)
        {
            return "";
        }
        return chosen.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || chosen.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? chosen
            : $"https://{chosen}";
    }

    private const string MailProfileName = "BP";
    private const string TemplateRelativePath = "doc/supplier_update_notification.html";
    private const string SubmissionTemplateRelativePath = "doc/supplier_submission_notification.html";

    /// <summary>Takes the supplier's id/name/email as primitives (rather than a full Supplier
    /// entity) so callers - including bulk sends over the dashboard's lightweight list projection -
    /// don't need to load the full ~20-navigation-property Supplier graph just to send a link.</summary>
    public async Task<(bool Success, string? Error)> NotifySupplierAsync(
        int supplierId, string supplierName, string? contactEmail, string actorUsername, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            return (false, "This supplier has no Contact Email on file.");
        }

        var templatePath = Path.Combine(env.WebRootPath, "doc", "supplier_update_notification.html");
        if (!File.Exists(templatePath))
        {
            return (false, $"Email template not found at wwwroot/{TemplateRelativePath}.");
        }

        // The link is a secure, opaque, single-supplier-scoped token, not the raw authenticated
        // edit URL - it carries no supplier id/number and is further protected by an emailed OTP
        // once opened (see SupplierAccessTokenService).
        var rawToken = await accessTokenService.CreateTokenAsync(supplierId, actorUsername);

        // Load the supplier's Business Unit so the email is branded with that unit's logo and its link is
        // built on that unit's own self-service host (the unit's Domain).
        int? businessUnitId;
        string? buDomain;
        await using (var buDb = await dbFactory.CreateDbContextAsync())
        {
            var bu = await buDb.Suppliers.Where(s => s.Id == supplierId)
                .Select(s => new { s.BusinessUnitId, s.BusinessUnit!.Domain })
                .FirstOrDefaultAsync();
            businessUnitId = bu?.BusinessUnitId;
            buDomain = bu?.Domain;
        }

        // Suppliers are external, so the link must point at the public self-service host, not the intranet
        // URL the admin happens to be browsing. Precedence: the business unit's own Domain (its DMZ host),
        // then the global SelfService:PortalUrl, then the caller's base URL.
        var linkBase = SelfServiceLinkBase(buDomain, configuration["SelfService:PortalUrl"], baseUrl);
        var link = $"{linkBase.TrimEnd('/')}/supplier/self-service/{rawToken}";

        var brand = await branding.GetBrandNameAsync(actorUsername, businessUnitId);
        var logo = await BusinessUnitLogoAsync(businessUnitId, actorUsername);
        var body = await File.ReadAllTextAsync(templatePath);
        body = body
            .Replace("{CompanyLogo}", BrandBlock(logo, brand))
            .Replace("{Supplier Name}", supplierName)
            .Replace("{Link}", link);

        var subject = $"Supplier Information Update - {supplierName}";

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {contactEmail},
                    @subject = {subject},
                    @body = {body},
                    @body_format = 'HTML';");
        }
        catch (Exception ex)
        {
            return (false, $"Database Mail send failed: {ex.Message}");
        }

        await auditService.LogAsync(
            actorUsername, AuditAction.Notify, "Supplier", supplierId,
            $"Sent update notification email to {contactEmail}");

        // The supplier has now been asked to review: move the record to "Awaiting Supplier Response".
        await using (var statusDb = await dbFactory.CreateDbContextAsync())
        {
            var awaitingStatusId = (await statusDb.LookupValues.FirstOrDefaultAsync(
                l => l.Category == LookupCategory.SupplierStatus && l.DisplayName == SupplierStatuses.AwaitingSupplierResponse))?.Id;
            if (awaitingStatusId is not null)
            {
                var supplier = await statusDb.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
                if (supplier is not null)
                {
                    supplier.StatusId = awaitingStatusId;
                    await statusDb.SaveChangesAsync();
                }
            }
        }

        return (true, null);
    }

    /// <summary>Emails the internal user who originally sent the self-service link (<paramref name="requestedByUsername"/>,
    /// from <see cref="Models.SupplierAccessToken.CreatedBy"/>) once the supplier submits their update, so the
    /// requestor doesn't have to keep checking back. Best-effort: a missing user/email or a mail-send
    /// failure is swallowed rather than surfaced to the supplier completing the public form.</summary>
    public async Task NotifyRequestorOfSubmissionAsync(Supplier supplier, string requestedByUsername, string baseUrl)
    {
        var requestor = await userService.GetByUsernameAsync(requestedByUsername);
        if (requestor is null || string.IsNullOrWhiteSpace(requestor.Email))
        {
            return;
        }

        var templatePath = Path.Combine(env.WebRootPath, "doc", "supplier_submission_notification.html");
        if (!File.Exists(templatePath))
        {
            return;
        }

        var link = $"{baseUrl.TrimEnd('/')}/supplier/edit/{supplier.Id}";
        var brand = await branding.GetBrandNameAsync(requestedByUsername, supplier.BusinessUnitId);
        var logo = await BusinessUnitLogoAsync(supplier.BusinessUnitId, requestedByUsername);
        var body = await File.ReadAllTextAsync(templatePath);
        body = body
            .Replace("{CompanyLogo}", BrandBlock(logo, brand))
            .Replace("{Supplier Name}", supplier.SupplierName)
            .Replace("{Link}", link);

        var subject = $"Supplier Update Submitted - {supplier.SupplierName}";

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {requestor.Email},
                    @subject = {subject},
                    @body = {body},
                    @body_format = 'HTML';");

            await auditService.LogAsync(
                "Supplier Self-Service", AuditAction.Notify, "Supplier", supplier.Id,
                $"Notified {requestor.Email} that {supplier.SupplierName} submitted an update");
        }
        catch
        {
            // Best-effort: the supplier's submission has already been saved successfully by this
            // point, so a failed courtesy email to the requestor must not surface as an error to them.
        }
    }

    /// <summary>Sends the supplier a "thank you" email once their record is marked Complete, confirming
    /// their registration is finalised. Takes primitives so the dashboard can call it from its list
    /// projection without loading the full Supplier graph.</summary>
    public async Task<(bool Success, string? Error)> NotifySupplierCompletedAsync(
        int supplierId, string supplierName, string? contactEmail, string actorUsername, int? businessUnitId)
    {
        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            return (false, "This supplier has no Contact Email on file.");
        }

        var brand = await branding.GetBrandNameAsync(actorUsername, businessUnitId);
        var logo = await BusinessUnitLogoAsync(businessUnitId, actorUsername);
        var safeName = System.Net.WebUtility.HtmlEncode(supplierName);
        var body = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:24px"">
  <div style=""max-width:560px;margin:0 auto;background:#ffffff;border-radius:10px;overflow:hidden;border:1px solid #e2e5ea"">
    <div style=""background:#1a2233;padding:18px 24px"">{BrandBlock(logo, brand)}
      <div style=""color:#cfd6e6;font-size:13px"">Supplier Data Gathering Portal</div>
    </div>
    <div style=""padding:24px;color:#2f3a4c;font-size:14.5px;line-height:1.6"">
      <h2 style=""margin:0 0 12px;color:#1a2233;font-size:19px"">Thank you, {safeName}</h2>
      <p style=""margin:0 0 12px"">We're pleased to confirm that your response has been <strong>received and updated</strong>,
        and your supplier registration is now <strong>complete</strong>. No further action is required from you at this time.</p>
      <p style=""margin:0 0 12px"">Thank you for taking the time to provide your information and for partnering with us.</p>
      <p style=""margin:18px 0 0;color:#7b8697;font-size:13px"">This is an automated message — please do not reply.</p>
    </div>
  </div>
</div>";

        var subject = $"Supplier Registration Completed — {supplierName}";

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {contactEmail},
                    @subject = {subject},
                    @body = {body},
                    @body_format = 'HTML';");
        }
        catch (Exception ex)
        {
            return (false, $"Database Mail send failed: {ex.Message}");
        }

        await auditService.LogAsync(
            actorUsername, AuditAction.Notify, "Supplier", supplierId,
            $"Sent completion thank-you email to {contactEmail}");

        return (true, null);
    }
}
