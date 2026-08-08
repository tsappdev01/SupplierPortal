using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Data.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Sends the Landlord-registration notification emails (FRD §28) through SQL Server Database Mail
/// (<c>sp_send_dbmail</c>) using the configured profile (<c>Leasing:MailProfile</c>, default <c>BP</c>),
/// exactly like <c>SupplierEmailService</c>. Mail is sent unconditionally in every environment; every send
/// is best-effort and never blocks the workflow transition that triggered it.
/// </summary>
public class LandlordEmailService(
    IDbContextFactory<LeasingDbContext> dbFactory, IOptions<LeasingOptions> options, ILogger<LandlordEmailService> logger)
{
    private string MailProfileName => options.Value.MailProfile;

    private static string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    public Task SendInviteAsync(string toEmail, string legalOrCompany, string link) =>
        SendAsync(toEmail, "Landlord Registration – Action Required",
            Wrap("Landlord Registration",
                $"<p style=\"margin:0 0 14px\">Dear Landlord,</p>" +
                $"<p style=\"margin:0 0 14px\">Dubai Investments Business Centre invites you to complete/update the landlord " +
                $"registration for <strong>{E(legalOrCompany)}</strong>. The process takes a few minutes and lets you " +
                $"provide your contact details, upload the required documents and confirm your authorised signatories.</p>" +
                $"<p style=\"margin:0 0 6px;color:#5C6670;font-size:13px\">You will be asked for a one-time verification code sent to this email address.</p>",
                "Complete Registration", link,
                "This link is confidential and unique to you — please do not share it. It expires in 30 days."));

    public Task SendCorrectionAsync(string toEmail, string propertyCode, string comment, string link) =>
        SendAsync(toEmail, "Action Required – Landlord Registration",
            Wrap("Additional Information Required",
                "<p style=\"margin:0 0 14px\">Business Centre has reviewed your registration and needs a small correction " +
                "before it can be completed.</p>" +
                $"{Note("Reviewer comment", comment)}" +
                $"<p style=\"margin:14px 0 6px\">Please reopen your registration for property <strong>{E(propertyCode)}</strong>, " +
                "make the update and resubmit.</p>",
                "Update Registration", link,
                "You will be asked to verify with a one-time code when you open the link."));

    public Task SendRejectionAsync(string toEmail, string propertyCode, string reason) =>
        SendAsync(toEmail, "Landlord Registration – Not Accepted",
            Wrap("Registration Not Accepted",
                $"<p style=\"margin:0 0 14px\">We were unable to accept the landlord registration for property " +
                $"<strong>{E(propertyCode)}</strong>.</p>{Note("Reason", reason)}" +
                "<p style=\"margin:14px 0 0\">If the registration is to proceed, Business Centre will restart the process " +
                "and send you a fresh link.</p>", null, null,
                "Please contact Dubai Investments Business Centre if you have any questions."));

    public Task SendCompletionToLandlordAsync(string toEmail, string propertyCode) =>
        SendAsync(toEmail, "Landlord Registration Completed",
            Wrap("Registration Completed",
                $"<p style=\"margin:0 0 14px\">Good news — your landlord registration for property " +
                $"<strong>{E(propertyCode)}</strong> has been reviewed and <strong>completed</strong> by Business Centre.</p>" +
                "<p style=\"margin:0\">No further action is required. Thank you for registering with Dubai Investments.</p>",
                null, null, null));

    public Task SendCompletionToStaffAsync(IEnumerable<string> staffEmails, string propertyCode, string? company, string completedBy, DateTime completedAt, string? initiatedBy = null)
    {
        var initiatedRow = string.IsNullOrWhiteSpace(initiatedBy) ? ""
            : $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Initiated By</td><td style=\"padding:4px 0\">{E(initiatedBy)}</td></tr>";
        var rows =
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Property Code</td><td style=\"padding:4px 0\"><strong>{E(propertyCode)}</strong></td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Company</td><td style=\"padding:4px 0\">{E(company ?? "—")}</td></tr>" +
            initiatedRow +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Completed By</td><td style=\"padding:4px 0\">{E(completedBy)}</td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Completion Date</td><td style=\"padding:4px 0\">{completedAt:yyyy-MM-dd HH:mm} UTC</td></tr>";
        var body = Wrap("Landlord Registration Completed",
            "<p style=\"margin:0 0 14px\">A landlord registration you are responsible for has been completed:</p>" +
            $"<table style=\"border-collapse:collapse;font-size:14px\">{rows}</table>", null, null, null);
        return Task.WhenAll(staffEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()
            .Select(e => SendAsync(e, $"Landlord Registration Completed – {propertyCode}", body)));
    }

    public Task SendSubmittedToStaffAsync(IEnumerable<string> staffEmails, string propertyCode, string? company, string? reference, string initiatedBy, string? link)
    {
        var rows =
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Property Code</td><td style=\"padding:4px 0\"><strong>{E(propertyCode)}</strong></td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Company</td><td style=\"padding:4px 0\">{E(company ?? "—")}</td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Reference</td><td style=\"padding:4px 0\">{E(reference ?? "—")}</td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Initiated By</td><td style=\"padding:4px 0\">{E(initiatedBy)}</td></tr>";
        var body = Wrap("Landlord Registration Submitted",
            "<p style=\"margin:0 0 14px\">A landlord has submitted their registration and it is ready for review. " +
            "A PDF summary of the full submission (contacts, signatories and attached documents) has been saved " +
            "against the registration.</p>" +
            $"<table style=\"border-collapse:collapse;font-size:14px\">{rows}</table>",
            link is null ? null : "Open Registration", link,
            "Open the registration in the Business Centre portal to review and complete it.");
        return Task.WhenAll(staffEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()
            .Select(e => SendAsync(e, $"Landlord Registration Submitted – {propertyCode}", body)));
    }

    public Task SendSignedToStaffAsync(IEnumerable<string> staffEmails, string propertyCode, string? company, string? signedBy, DateTime signedAt)
    {
        var rows =
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Property Code</td><td style=\"padding:4px 0\"><strong>{E(propertyCode)}</strong></td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Company</td><td style=\"padding:4px 0\">{E(company ?? "—")}</td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Signed By</td><td style=\"padding:4px 0\">{E(signedBy ?? "—")}</td></tr>" +
            $"<tr><td style=\"padding:4px 12px 4px 0;color:#5C6670\">Signed On</td><td style=\"padding:4px 0\">{signedAt:yyyy-MM-dd HH:mm} UTC</td></tr>";
        var body = Wrap("Landlord Registration Signed",
            "<p style=\"margin:0 0 14px\">The primary contact has <strong>signed</strong> the landlord registration document via Circularo. " +
            "The signed copy is available on the registration.</p>" +
            $"<table style=\"border-collapse:collapse;font-size:14px\">{rows}</table>", null, null, null);
        return Task.WhenAll(staffEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct()
            .Select(e => SendAsync(e, $"Landlord Registration Signed – {propertyCode}", body)));
    }

    /// <summary>Wraps content in a branded, email-client-safe HTML shell (inline styles, table layout).</summary>
    private static string Wrap(string heading, string bodyHtml, string? ctaText, string? ctaUrl, string? footnote)
    {
        var cta = ctaText is not null && ctaUrl is not null
            ? $"<tr><td style=\"padding:22px 28px 4px\"><a href=\"{ctaUrl}\" style=\"display:inline-block;background:#092F56;color:#ffffff;text-decoration:none;font-weight:600;font-size:15px;padding:12px 26px;border-radius:4px\">{E(ctaText)}</a></td></tr>"
            : "";
        var foot = footnote is not null
            ? $"<tr><td style=\"padding:6px 28px 0;color:#8A929A;font-size:12px\">{E(footnote)}</td></tr>"
            : "";
        return
$@"<!DOCTYPE html><html><body style=""margin:0;background:#06213D;font-family:Segoe UI,Arial,sans-serif"">
  <table role=""presentation"" width=""100%"" style=""background:#06213D;padding:24px 0"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""600"" style=""max-width:600px;width:100%;background:#ffffff;border-radius:8px;overflow:hidden"">
        <tr><td style=""background:#092F56;padding:20px 28px"">
          <div style=""color:#ffffff;font-size:18px;font-weight:700;letter-spacing:.02em"">Dubai Investments</div>
          <div style=""color:#9DB3C8;font-size:12px;letter-spacing:.08em;text-transform:uppercase;margin-top:2px"">Business Centre · Leasing</div>
        </td></tr>
        <tr><td style=""padding:24px 28px 4px""><h1 style=""margin:0 0 14px;font-size:20px;color:#092F56"">{E(heading)}</h1>{bodyHtml}</td></tr>
        {cta}
        {foot}
        <tr><td style=""padding:24px 28px 22px;color:#8A929A;font-size:12px;border-top:1px solid #E2DCD4;margin-top:16px"">
          This is an automated message from Dubai Investments Business Centre. Please do not reply to this email.
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";
    }

    private static string Note(string label, string text) =>
        $@"<table role=""presentation"" width=""100%"" style=""background:#F6F3EF;border-left:3px solid #706259;border-radius:4px;margin:4px 0""><tr><td style=""padding:12px 14px"">
            <div style=""font-size:11px;letter-spacing:.1em;text-transform:uppercase;color:#706259;font-weight:600;margin-bottom:4px"">{E(label)}</div>
            <div style=""font-size:14px;color:#20262D"">{E(text)}</div></td></tr></table>";

    private async Task SendAsync(string toEmail, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) { logger.LogWarning("Landlord email skipped: no recipient address."); return; }
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
            logger.LogInformation("Landlord email queued via Database Mail profile '{Profile}' to {Email}: {Subject}", MailProfileName, toEmail, subject);
        }
        catch (Exception ex)
        {
            // Best-effort: a mail outage must never block the workflow transition — but log it so a
            // misconfigured Database Mail profile is visible rather than silently swallowed.
            logger.LogError(ex, "Landlord email FAILED via Database Mail profile '{Profile}' to {Email}: {Subject}", MailProfileName, toEmail, subject);
        }
    }
}
