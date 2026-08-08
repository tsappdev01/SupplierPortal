namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Circularo e-signature configuration, bound to <c>Leasing:Circularo</c>. When <see cref="Enabled"/> is
/// false (or no API key is set) the integration is skipped — approval still completes normally, it just
/// doesn't route the summary for signature. The endpoint paths are configurable so they can be aligned with
/// the Circularo API version without a code change.
/// See https://developers.circularo.com/getting-started/ and .../authentication/#apikeys.
/// </summary>
public class CircularoOptions
{
    public const string SectionName = "Leasing:Circularo";

    public bool Enabled { get; set; }

    /// <summary>API base, e.g. the sandbox <c>https://app.sandbox.circularo.com/api/v1</c>.</summary>
    public string BaseUrl { get; set; } = "https://app.sandbox.circularo.com/api/v1";

    /// <summary>API key created in Circularo, used as the bearer token for API calls.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Authorization header scheme. "Bearer" → <c>Authorization: Bearer &lt;key&gt;</c>;
    /// empty → the raw key is sent as the header value.</summary>
    public string AuthScheme { get; set; } = "Bearer";

    /// <summary>Multipart upload endpoint (relative to <see cref="BaseUrl"/>).</summary>
    public string UploadPath { get; set; } = "document/upload";

    /// <summary>Form field name for the uploaded file in the multipart request.</summary>
    public string UploadFileField { get; set; } = "file";

    /// <summary>Send-for-signature endpoint. <c>{id}</c> is replaced with the uploaded document id.</summary>
    public string SignInvitePath { get; set; } = "document/{id}/workflow";

    /// <summary>Endpoint to download the signed copy. <c>{id}</c> is replaced with the document id.</summary>
    public string DownloadSignedPath { get; set; } = "document/{id}/download";

    /// <summary>Shared secret to validate incoming signature webhooks (matched against the
    /// <c>X-Webhook-Secret</c> header or a <c>?secret=</c> query value). Empty disables the check.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Subject line for the signature request Circularo emails to the signer.</summary>
    public string RequestSubject { get; set; } = "Signature required — Landlord Registration";

    /// <summary>Message body for the signature request.</summary>
    public string RequestMessage { get; set; } =
        "Please review and sign your landlord registration document for Dubai Investments.";
}
