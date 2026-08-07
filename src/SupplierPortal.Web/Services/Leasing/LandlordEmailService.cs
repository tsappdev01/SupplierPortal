using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Data.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Sends the Landlord-registration notification emails (FRD §28) through SQL Server Database Mail
/// (<c>sp_send_dbmail</c>) using the configured profile (<c>Leasing:MailProfile</c>, default <c>BP</c>),
/// exactly like the supplier emails. Sending is gated by <c>Leasing:SendEmails</c> (defaults to on outside
/// Development); every send is best-effort and never blocks the workflow transition that triggered it.
/// </summary>
public class LandlordEmailService(IDbContextFactory<LeasingDbContext> dbFactory, IWebHostEnvironment env, IOptions<LeasingOptions> options)
{
    private string MailProfileName => options.Value.MailProfile;
    private bool ShouldSend => options.Value.SendEmails ?? !env.IsDevelopment();

    public Task SendInviteAsync(string toEmail, string legalOrCompany, string link) =>
        SendAsync(toEmail, "Landlord Registration – Action Required",
            $"<p>Dear Landlord,</p><p>Please complete/update your landlord registration information for " +
            $"<strong>{legalOrCompany}</strong> using the secure link below.</p>" +
            $"<p><a href=\"{link}\">Complete Registration</a></p>" +
            "<p>This link is confidential and should not be shared.</p><p>Dubai Investments — Business Centre</p>");

    public Task SendCorrectionAsync(string toEmail, string propertyCode, string comment, string link) =>
        SendAsync(toEmail, "Action Required – Landlord Registration",
            "<p>Business Centre has reviewed your registration and requires additional information/documentation.</p>" +
            $"<p><strong>Comment:</strong><br/>{System.Net.WebUtility.HtmlEncode(comment)}</p>" +
            $"<p>Please use the link below to update your registration for property <strong>{propertyCode}</strong>.</p>" +
            $"<p><a href=\"{link}\">Update Registration</a></p>");

    public Task SendRejectionAsync(string toEmail, string propertyCode, string reason) =>
        SendAsync(toEmail, "Landlord Registration – Not Accepted",
            $"<p>Your landlord registration for property <strong>{propertyCode}</strong> could not be accepted.</p>" +
            $"<p><strong>Reason:</strong><br/>{System.Net.WebUtility.HtmlEncode(reason)}</p>" +
            "<p>Business Centre will contact you if the process is to be restarted.</p>");

    public Task SendCompletionToLandlordAsync(string toEmail, string propertyCode) =>
        SendAsync(toEmail, "Landlord Registration Completed",
            $"<p>Your landlord registration for property <strong>{propertyCode}</strong> has been successfully " +
            "reviewed and completed by Business Centre.</p>");

    public Task SendCompletionToStaffAsync(IEnumerable<string> staffEmails, string propertyCode, string? company, string completedBy, DateTime completedAt)
    {
        var body = "<p>Landlord Registration Completed</p><ul>" +
                   $"<li>Property Code: {propertyCode}</li>" +
                   $"<li>Company: {company ?? "—"}</li>" +
                   $"<li>Completed By: {completedBy}</li>" +
                   $"<li>Completion Date: {completedAt:yyyy-MM-dd HH:mm} UTC</li></ul>";
        return Task.WhenAll(staffEmails.Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .Select(e => SendAsync(e, $"Landlord Registration Completed – {propertyCode}", body)));
    }

    private async Task SendAsync(string toEmail, string subject, string body)
    {
        if (!ShouldSend || string.IsNullOrWhiteSpace(toEmail)) return; // skipped in Development unless Leasing:SendEmails=true
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {MailProfileName},
                    @recipients = {toEmail},
                    @subject = {subject},
                    @body = {body},
                    @body_format = 'HTML';");
        }
        catch
        {
            // Best-effort: a mail outage must never block the workflow transition. The audit event still records it.
        }
    }
}
