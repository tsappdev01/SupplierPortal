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

    /// <summary>How the API key/token is transmitted. Per the Circularo docs the token is passed as a
    /// <c>?token=&lt;key&gt;</c> query parameter on every request (see developers.circularo.com/latest/authentication
    /// and .../1-creating-and-using-api-tokens — API keys are used the same way as session tokens). Values:
    /// <c>"Query"</c> (query param only), <c>"Header"</c> (Authorization header only), or <c>"Both"</c>
    /// (send both — the safe default, since an extra header is harmless but a missing token yields 401/404).</summary>
    public string AuthMode { get; set; } = "Both";

    /// <summary>Query-string parameter name carrying the token when <see cref="AuthMode"/> includes the query mode.</summary>
    public string TokenQueryParam { get; set; } = "token";

    /// <summary>File-storage endpoint (relative to <see cref="BaseUrl"/>). Circularo stores the raw file first
    /// and returns a file id that the document then references. Discovered from the web app: <c>files/saveFile</c>.</summary>
    public string FileUploadPath { get; set; } = "files/saveFile";

    /// <summary>Form field name for the file in the <see cref="FileUploadPath"/> multipart request.</summary>
    public string FileUploadField { get; set; } = "file";

    /// <summary>Document-creation endpoint (relative to <see cref="BaseUrl"/>). Circularo's <c>POST documents</c>
    /// creates the document together with its signing workflow (it validates <c>definitionType</c> + <c>workflow</c>).</summary>
    public string UploadPath { get; set; } = "documents";

    /// <summary>Circularo document definition type. The default catalogue type is <c>d_default</c>.</summary>
    public string DefinitionType { get; set; } = "d_default";

    /// <summary>Candidate upload endpoints tried in order until one succeeds (self-discovery when the exact
    /// path isn't known). When empty, a built-in candidate list is used. Set this once the working path is
    /// known to avoid probing.</summary>
    public string[] UploadPathCandidates { get; set; } = [];

    /// <summary>Form field name for the uploaded file in the multipart request.</summary>
    public string UploadFileField { get; set; } = "file";

    /// <summary>Send-for-signature endpoint. <c>{id}</c> is replaced with the uploaded document id.</summary>
    public string SignInvitePath { get; set; } = "document/{id}/workflow";

    /// <summary>Endpoint to download the signed copy. <c>{id}</c> is replaced with the document id.</summary>
    public string DownloadSignedPath { get; set; } = "document/{id}/download";

    /// <summary>Shared secret to validate incoming signature webhooks (matched against the
    /// <c>X-Webhook-Secret</c> header or a <c>?secret=</c> query value). Empty disables the check.</summary>
    public string WebhookSecret { get; set; } = "";

    // ---- UAE Pass signing / identity verification (per the Circularo ↔ UAE Pass integration) ----
    /// <summary>Require the signer to sign with UAE Pass (UAE Pass certificate).</summary>
    public bool RequireUaePassSignature { get; set; } = true;

    /// <summary>Require the external recipient to verify their identity via UAE Pass to open the document.</summary>
    public bool RequireUaePassVerification { get; set; } = true;

    /// <summary>Signature provider value sent to Circularo when UAE Pass signing is required.</summary>
    public string SignatureProvider { get; set; } = "uaepass";

    /// <summary>Identity-verification method value for the recipient (UAE Pass).</summary>
    public string VerificationMethod { get; set; } = "uaepass";

    /// <summary>Verification factor to match the recipient on (email must match their UAE Pass account).</summary>
    public string VerificationFactor { get; set; } = "email";

    /// <summary>Subject line for the signature request Circularo emails to the signer.</summary>
    public string RequestSubject { get; set; } = "Signature required — Landlord Registration";

    /// <summary>Message body for the signature request.</summary>
    public string RequestMessage { get; set; } =
        "Please review and sign your landlord registration document for Dubai Investments.";
}
