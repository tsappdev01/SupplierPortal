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
    /// <summary>Renders the company-logo header image for an email, or an empty string when there is no
    /// company logo (the template then keeps its text title).</summary>
    private static string LogoImg(string? dataUri) =>
        string.IsNullOrEmpty(dataUri) ? "" :
        $"<img src=\"{dataUri}\" alt=\"Company logo\" style=\"display:block;max-height:48px;max-width:200px;margin-bottom:12px;background:#ffffff;padding:4px 8px;border-radius:4px\" />";

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

        // Suppliers are external, so the link must point at the public self-service host, not the
        // intranet URL the admin happens to be browsing. SelfService:PortalUrl (e.g. https://bps.dipark.com)
        // overrides the base when set; otherwise fall back to the caller's base URL.
        var portalUrl = configuration["SelfService:PortalUrl"];
        var linkBase = string.IsNullOrWhiteSpace(portalUrl) ? baseUrl : portalUrl;
        var link = $"{linkBase.TrimEnd('/')}/supplier/self-service/{rawToken}";

        var logo = await branding.GetCompanyLogoDataUriAsync(actorUsername);
        var body = await File.ReadAllTextAsync(templatePath);
        body = body
            .Replace("{CompanyLogo}", LogoImg(logo))
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
        var logo = await branding.GetCompanyLogoDataUriAsync(requestedByUsername)
                   ?? await branding.GetBusinessUnitLogoDataUriAsync(supplier.BusinessUnitId);
        var body = await File.ReadAllTextAsync(templatePath);
        body = body
            .Replace("{CompanyLogo}", LogoImg(logo))
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
}
