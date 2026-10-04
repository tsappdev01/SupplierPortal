using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.SendMail;

namespace SupplierPortal.Web.Services;

/// <summary>Settings for the Microsoft Graph (app-only, client-credentials) email sender, bound from
/// the <c>Email:Graph</c> configuration section. The sender mailbox is fixed (<see cref="SenderUpn"/>)
/// and never taken from user input; the client secret must come from a secret store / environment,
/// never committed. Leave any value blank to keep Graph disabled (the dispatcher falls back to SMTP
/// or Database Mail).</summary>
public sealed class GraphMailOptions
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SenderUpn { get; set; } = "";

    /// <summary>True only when all four values are supplied — the signal that Graph should be used.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(SenderUpn);
}

/// <summary>Sends HTML email from the fixed <c>SenderUpn</c> mailbox via Microsoft Graph using the
/// app-only client-credentials flow (no signed-in user). Mailbox scoping is enforced Exchange-side by
/// an Application Access Policy — this code does not (and must not) enforce which mailbox it may use;
/// a 403 <c>ErrorAccessDenied</c> means the policy excludes the sender, not a code bug. Never throws to
/// callers: returns the same <c>(bool Ok, string? Error)</c> contract as the other email providers.</summary>
public sealed class GraphMailSender(IOptions<GraphMailOptions> options, ILogger<GraphMailSender> logger)
{
    private readonly GraphMailOptions _options = options.Value;
    private readonly object _clientLock = new();
    private GraphServiceClient? _graph;

    public bool IsConfigured => _options.IsConfigured;

    // Built lazily so an unconfigured app never constructs a credential. The client caches tokens.
    private GraphServiceClient Client()
    {
        if (_graph is not null)
        {
            return _graph;
        }
        lock (_clientLock)
        {
            _graph ??= new GraphServiceClient(
                new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret));
        }
        return _graph;
    }

    public Task<(bool Ok, string? Error)> SendHtmlAsync(
        string toEmail, string subject, string htmlBody, string? fromMailbox = null, CancellationToken ct = default) =>
        SendAsync(toEmail, subject, htmlBody, attachments: null, fromMailbox, ct);

    /// <summary>Sends an HTML email with a single file attachment (used by the scheduled report runner).</summary>
    public Task<(bool Ok, string? Error)> SendHtmlWithAttachmentAsync(
        string toEmail, string subject, string htmlBody,
        byte[] attachment, string fileName, string contentType, string? fromMailbox = null, CancellationToken ct = default) =>
        SendAsync(toEmail, subject, htmlBody, [(attachment, fileName, contentType)], fromMailbox, ct);

    /// <summary>Sends an HTML email with one or more file attachments (PDF and/or Excel report files).</summary>
    public Task<(bool Ok, string? Error)> SendHtmlWithAttachmentsAsync(
        string toEmail, string subject, string htmlBody,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)> attachments, string? fromMailbox = null, CancellationToken ct = default) =>
        SendAsync(toEmail, subject, htmlBody, attachments, fromMailbox, ct);

    private async Task<(bool Ok, string? Error)> SendAsync(
        string toEmail, string subject, string htmlBody,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)>? attachments, string? fromMailbox, CancellationToken ct)
    {
        // Send AS the business-unit mailbox when one is supplied, so the recipient sees it as the sender;
        // otherwise use the configured default SenderUpn. Both must be permitted by the Exchange Application
        // Access Policy. If sending as the BU mailbox is rejected (not a mailbox / not permitted), fall back
        // to the default sender so the notification is still delivered.
        var requested = string.IsNullOrWhiteSpace(fromMailbox) ? _options.SenderUpn : fromMailbox.Trim();
        var (ok, error, retryWithDefault) = await TrySendAsync(requested, toEmail, subject, htmlBody, attachments, ct);
        if (!ok && retryWithDefault && !string.Equals(requested, _options.SenderUpn, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Graph: sending as '{From}' failed ({Error}); retrying from default sender '{Default}'.",
                requested, error, _options.SenderUpn);
            (ok, error, _) = await TrySendAsync(_options.SenderUpn, toEmail, subject, htmlBody, attachments, ct);
        }
        return (ok, error);
    }

    private async Task<(bool Ok, string? Error, bool RetryWithDefault)> TrySendAsync(
        string senderMailbox, string toEmail, string subject, string htmlBody,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)>? attachments, CancellationToken ct)
    {
        try
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                ToRecipients =
                [
                    new Recipient { EmailAddress = new EmailAddress { Address = toEmail } }
                ],
            };

            if (attachments is not null && attachments.Count > 0)
            {
                message.Attachments = attachments
                    .Where(a => a.Bytes is not null && a.Bytes.Length > 0)
                    .Select(a => (Attachment)new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = a.FileName,
                        ContentType = a.ContentType,
                        ContentBytes = a.Bytes,
                    })
                    .ToList();
            }

            var request = new SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true,   // keep a copy in the sender's Sent Items
            };

            // POST /users/{senderMailbox}/sendMail — the sender must be a mailbox allowed by the
            // Exchange Application Access Policy.
            await Client().Users[senderMailbox].SendMail.PostAsync(request, cancellationToken: ct);
            return (true, null, false);
        }
        catch (ODataError ex)
        {
            var code = ex.Error?.Code;
            logger.LogError(ex, "Graph email send failed from {Sender} to {Recipient}: {Status} {Code}",
                senderMailbox, toEmail, ex.ResponseStatusCode, code);
            // The request was rejected before delivery (bad/forbidden mailbox), so a retry with the default
            // sender is safe (no duplicate email).
            return (false, $"Graph email send failed ({ex.ResponseStatusCode} {code}): {ex.Error?.Message ?? ex.Message}", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Graph email send failed from {Sender} to {Recipient}", senderMailbox, toEmail);
            return (false, $"Graph email send failed: {ex.Message}", false);
        }
    }
}
