using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;

namespace SupplierPortal.Web.Services;

/// <summary>Central email sender. Prefers Microsoft Graph when <c>Email:Graph</c> is configured, otherwise
/// uses SMTP when an <c>Email:Smtp:Host</c> is configured (works on Azure SQL, which has no Database Mail),
/// otherwise falls back to SQL Server Database Mail (sp_send_dbmail) so local SQL Server deployments keep
/// working unchanged. All supplier/customer notification and OTP emails route through here so the delivery
/// mechanism is chosen in one place.</summary>
public class EmailDispatcher(
    IConfiguration config, IDbContextFactory<ApplicationDbContext> dbFactory, ILogger<EmailDispatcher> logger,
    GraphMailSender graphMail)
{
    private const string DefaultProfile = "BP";

    /// <summary>Sends an HTML email. Returns (true, null) on success, (false, error) on failure.</summary>
    public async Task<(bool Ok, string? Error)> SendHtmlAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "No recipient email address.");
        }

        // Graph wins when fully configured; otherwise the existing SMTP / Database Mail choice is unchanged.
        if (graphMail.IsConfigured)
        {
            return await graphMail.SendHtmlAsync(toEmail, subject, htmlBody);
        }

        var smtpHost = config["Email:Smtp:Host"];
        return string.IsNullOrWhiteSpace(smtpHost)
            ? await SendViaDatabaseMailAsync(toEmail, subject, htmlBody)
            : await SendViaSmtpAsync(smtpHost.Trim(), toEmail, subject, htmlBody);
    }

    /// <summary>Sends an HTML email with a single file attachment. Used by the scheduled report runner to
    /// deliver the generated PDF/Excel. Graph is preferred when configured, then SMTP. SQL Server Database
    /// Mail cannot attach in-memory bytes, so when neither Graph nor SMTP is configured this returns a clear
    /// error rather than silently dropping the attachment.</summary>
    public async Task<(bool Ok, string? Error)> SendHtmlWithAttachmentAsync(
        string toEmail, string subject, string htmlBody, byte[] attachment, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            return (false, "No recipient email address.");
        }
        if (attachment is null || attachment.Length == 0)
        {
            return (false, "No attachment content.");
        }

        if (graphMail.IsConfigured)
        {
            return await graphMail.SendHtmlWithAttachmentAsync(toEmail, subject, htmlBody, attachment, fileName, contentType);
        }

        var smtpHost = config["Email:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            return await SendViaSmtpAsync(smtpHost.Trim(), toEmail, subject, htmlBody,
                (attachment, fileName, contentType));
        }

        return (false, "Attachments require Microsoft Graph or SMTP to be configured; " +
                       "SQL Server Database Mail cannot attach in-memory files.");
    }

    private async Task<(bool, string?)> SendViaSmtpAsync(string host, string to, string subject, string html,
        (byte[] Bytes, string FileName, string ContentType)? attachment = null)
    {
        try
        {
            var port = int.TryParse(config["Email:Smtp:Port"], out var p) ? p : 587;
            var user = config["Email:Smtp:User"];
            var pass = config["Email:Smtp:Password"];
            var fromAddress = config["Email:Smtp:From"] ?? user ?? "no-reply@localhost";
            var fromName = config["Email:Smtp:FromName"] ?? "Supplier Portal";
            // EnableSsl defaults to true (STARTTLS/implicit TLS); set Email:Smtp:EnableSsl=false only for a
            // plain relay.
            var enableSsl = !bool.TryParse(config["Email:Smtp:EnableSsl"], out var ssl) || ssl;

            using var client = new SmtpClient(host, port) { EnableSsl = enableSsl };
            if (!string.IsNullOrWhiteSpace(user))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, fromName),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            message.To.Add(to);

            // MemoryStream must outlive SendMailAsync, so it's disposed after the send (not with `using`
            // inline, which would dispose it before the attachment is read).
            MemoryStream? attachmentStream = null;
            if (attachment is { } a)
            {
                attachmentStream = new MemoryStream(a.Bytes);
                message.Attachments.Add(new Attachment(attachmentStream, a.FileName, a.ContentType));
            }

            try
            {
                await client.SendMailAsync(message);
            }
            finally
            {
                attachmentStream?.Dispose();
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP email send failed to {Recipient}", to);
            return (false, $"Email send failed: {ex.Message}");
        }
    }

    private async Task<(bool, string?)> SendViaDatabaseMailAsync(string to, string subject, string html)
    {
        try
        {
            var profile = config["Email:MailProfileName"] ?? DefaultProfile;
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($@"
                EXEC msdb.dbo.sp_send_dbmail
                    @profile_name = {profile},
                    @recipients = {to},
                    @subject = {subject},
                    @body = {html},
                    @body_format = 'HTML';");
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database Mail send failed to {Recipient}", to);
            return (false, $"Database Mail send failed: {ex.Message}");
        }
    }
}
