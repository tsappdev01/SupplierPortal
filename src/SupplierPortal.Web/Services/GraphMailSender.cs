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

    public async Task<(bool Ok, string? Error)> SendHtmlAsync(
        string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            var request = new SendMailPostRequestBody
            {
                Message = new Message
                {
                    Subject = subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
                    ToRecipients =
                    [
                        new Recipient { EmailAddress = new EmailAddress { Address = toEmail } }
                    ],
                },
                SaveToSentItems = true,   // keep a copy in the sender's Sent Items
            };

            // POST /users/{SenderUpn}/sendMail — the sender must be the mailbox allowed by the
            // Exchange Application Access Policy.
            await Client().Users[_options.SenderUpn].SendMail.PostAsync(request, cancellationToken: ct);
            return (true, null);
        }
        catch (ODataError ex)
        {
            var code = ex.Error?.Code;
            logger.LogError(ex, "Graph email send failed to {Recipient}: {Status} {Code}",
                toEmail, ex.ResponseStatusCode, code);
            return (false, $"Graph email send failed ({ex.ResponseStatusCode} {code}): {ex.Error?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Graph email send failed to {Recipient}", toEmail);
            return (false, $"Graph email send failed: {ex.Message}");
        }
    }
}
