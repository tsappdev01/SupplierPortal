using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Sends customer self-service emails (update-request link, submission and completion notices)
/// via SQL Server Database Mail. Mirrors <see cref="SupplierEmailService"/> so the customer flow behaves
/// exactly like the supplier one.</summary>
public class CustomerEmailService(
    IDbContextFactory<ApplicationDbContext> dbFactory, IWebHostEnvironment env, AuditService auditService,
    CustomerAccessTokenService accessTokenService, UserService userService, IConfiguration configuration,
    EmailBrandingService branding, EmailDispatcher emailDispatcher)
{
    private const string MailProfileName = "BP";
    private const string TemplateRelativePath = "doc/customer_update_notification.html";

    private static string BrandBlock(string? logoSrc, string? name)
    {
        var safeName = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(name) ? EmailBrandingService.DefaultBrandName : name);
        return string.IsNullOrWhiteSpace(logoSrc)
            ? $"<div style=\"color:#0b2545;font-size:16px;font-weight:700;letter-spacing:.02em;margin-bottom:10px\">{safeName}</div>"
            : $"<img src=\"{logoSrc}\" alt=\"{safeName}\" style=\"max-height:48px;max-width:220px;display:block\" />";
    }

    /// <summary>Sends the customer a secure self-service link and moves the record to "Awaiting Customer
    /// Response". Takes the customer id/name/email primitives so the dashboard list projection can call it.</summary>
    public async Task<(bool Success, string? Error)> NotifyCustomerAsync(
        int customerId, string customerName, string? email, string actorUsername, string baseUrl)
    {
        // Resolve the recipient (Primary Email, else the contact's Email) and the branding business unit.
        int? businessUnitId;
        await using (var lookupDb = await dbFactory.CreateDbContextAsync())
        {
            var c = await lookupDb.Customers.AsNoTracking().Include(x => x.Contact)
                .FirstOrDefaultAsync(x => x.Id == customerId);
            if (c is null)
            {
                return (false, "Customer not found.");
            }
            businessUnitId = c.BusinessUnitId;
            if (string.IsNullOrWhiteSpace(email))
            {
                email = CustomerAccessTokenService.EmailFor(c);
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "This customer has no email address on file.");
        }

        var templatePath = Path.Combine(env.WebRootPath, "doc", "customer_update_notification.html");
        if (!File.Exists(templatePath))
        {
            return (false, $"Email template not found at wwwroot/{TemplateRelativePath}.");
        }

        var rawToken = await accessTokenService.CreateTokenAsync(customerId, actorUsername);

        // Customers are external, so the link must point at the public self-service host. Precedence: the
        // business unit's own Domain (each unit can have its own DMZ host), then the global
        // SelfService:PortalUrl, then the caller's base URL. A bare host is turned into an https:// URL.
        string? buDomain = null;
        if (businessUnitId is int buId)
        {
            await using var buDb = await dbFactory.CreateDbContextAsync();
            buDomain = await buDb.BusinessUnits.Where(b => b.Id == buId)
                .Select(b => b.Domain).FirstOrDefaultAsync();
        }
        var linkBase = SupplierEmailService.SelfServiceLinkBase(buDomain, configuration["SelfService:PortalUrl"], baseUrl);
        var link = $"{linkBase.TrimEnd('/')}/customer/self-service/{rawToken}";

        var brand = await branding.GetBrandNameAsync(actorUsername, businessUnitId);
        var logo = await branding.GetBusinessUnitLogoSrcAsync(businessUnitId) ?? await branding.GetLogoSrcAsync(actorUsername, businessUnitId);
        var body = await File.ReadAllTextAsync(templatePath);
        body = body
            .Replace("{CompanyLogo}", BrandBlock(logo, brand))
            .Replace("{Customer Name}", System.Net.WebUtility.HtmlEncode(customerName))
            .Replace("{Link}", link);

        var subject = $"Customer Information Update - {customerName}";

        var fromEmail = await branding.GetPortalSenderEmailAsync(businessUnitId, Models.PortalType.Customer);
        var (sent, sendError) = await emailDispatcher.SendHtmlAsync(email, subject, body, fromEmail: fromEmail, fromName: brand);
        if (!sent)
        {
            return (false, sendError);
        }

        await auditService.LogAsync(
            actorUsername, AuditAction.Notify, "Customer", customerId,
            $"Sent update notification email to {email}");

        // Move the record to "Awaiting Customer Response".
        await using (var statusDb = await dbFactory.CreateDbContextAsync())
        {
            var customer = await statusDb.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer is not null)
            {
                customer.Status = CustomerStatuses.AwaitingCustomerResponse;
                customer.UpdatedAt = DateTime.UtcNow;
                await statusDb.SaveChangesAsync();
            }
        }

        return (true, null);
    }

    /// <summary>Best-effort courtesy email to the internal user who sent the link, once the customer submits.</summary>
    public async Task NotifyRequestorOfSubmissionAsync(Customer customer, string requestedByUsername, string baseUrl)
    {
        var requestor = await userService.GetByUsernameAsync(requestedByUsername);
        if (requestor is null || string.IsNullOrWhiteSpace(requestor.Email))
        {
            return;
        }

        var brand = await branding.GetBrandNameAsync(requestedByUsername, customer.BusinessUnitId);
        var logo = await branding.GetBusinessUnitLogoSrcAsync(customer.BusinessUnitId);
        var link = $"{baseUrl.TrimEnd('/')}/admin/customers/{customer.Id}";
        var safeName = System.Net.WebUtility.HtmlEncode(customer.LegalName ?? "Customer");
        var body = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:24px"">
  <div style=""max-width:560px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;border:1px solid #e2e5ea"">
    <div style=""background:#ffffff;padding:18px 24px;border-bottom:1px solid #e2e5ea"">{BrandBlock(logo, brand)}</div>
    <div style=""padding:24px;color:#2f3a4c;font-size:14.5px;line-height:1.6"">
      <p style=""margin:0 0 12px"">{safeName} has submitted their information for review.</p>
      <p style=""margin:0 0 12px""><a href=""{link}"">Open the customer record</a> to review and complete it.</p>
    </div>
  </div>
</div>";

        // Best-effort: the submission is already saved, so a failed courtesy email must not surface.
        var fromEmail = await branding.GetPortalSenderEmailAsync(customer.BusinessUnitId, Models.PortalType.Customer);
        var (sent, _) = await emailDispatcher.SendHtmlAsync(
            requestor.Email!, $"Customer Update Submitted - {customer.LegalName}", body, fromEmail: fromEmail, fromName: brand);
        if (sent)
        {
            await auditService.LogAsync(
                "Customer Self-Service", AuditAction.Notify, "Customer", customer.Id,
                $"Notified {requestor.Email} that {customer.LegalName} submitted an update");
        }
    }

    /// <summary>Sends the customer a thank-you email once their record is marked Completed.</summary>
    public async Task<(bool Success, string? Error)> NotifyCustomerCompletedAsync(
        int customerId, string customerName, string? email, string actorUsername, int? businessUnitId)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "This customer has no email address on file.");
        }

        var brand = await branding.GetBrandNameAsync(actorUsername, businessUnitId);
        var logo = await branding.GetBusinessUnitLogoSrcAsync(businessUnitId) ?? await branding.GetLogoSrcAsync(actorUsername, businessUnitId);
        var safeName = System.Net.WebUtility.HtmlEncode(customerName);
        var body = $@"<div style=""font-family:Segoe UI,Arial,sans-serif;background:#f4f6f9;padding:24px"">
  <div style=""max-width:560px;margin:0 auto;background:#fff;border-radius:10px;overflow:hidden;border:1px solid #e2e5ea"">
    <div style=""background:#ffffff;padding:18px 24px;border-bottom:1px solid #e2e5ea"">{BrandBlock(logo, brand)}</div>
    <div style=""padding:24px;color:#2f3a4c;font-size:14.5px;line-height:1.6"">
      <h2 style=""margin:0 0 12px;color:#0b2545;font-size:19px"">Thank you, {safeName}</h2>
      <p style=""margin:0 0 12px"">Your customer registration has been <strong>reviewed and completed</strong>.
        No further action is required from you at this time.</p>
      <p style=""margin:18px 0 0;color:#7b8697;font-size:13px"">This is an automated message — please do not reply.</p>
    </div>
  </div>
</div>";

        var fromEmail = await branding.GetPortalSenderEmailAsync(businessUnitId, Models.PortalType.Customer);
        var (sent, sendError) = await emailDispatcher.SendHtmlAsync(
            email, $"Customer Registration Completed — {customerName}", body, fromEmail: fromEmail, fromName: brand);
        if (!sent)
        {
            return (false, sendError);
        }

        await auditService.LogAsync(
            actorUsername, AuditAction.Notify, "Customer", customerId,
            $"Sent completion thank-you email to {email}");

        return (true, null);
    }
}
