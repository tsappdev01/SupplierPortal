namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Circularo e-signature configuration, bound to <c>Leasing:Circularo</c>. When <see cref="Enabled"/> is
/// false (or no API key is set) the integration is skipped — approval still completes normally, it just
/// doesn't route the summary for signature.
/// <para>
/// The endpoint paths and payload shapes mirror the proven Broker-Portal integration
/// (see https://developers.circularo.com). Authentication is a <c>?token=&lt;key&gt;</c> query parameter on
/// every request. The three-step flow is: <c>POST /files/saveFile</c> (upload → content hash) →
/// <c>POST /documents</c> (create from hash → document id) → <c>POST /share</c> (send for signature).
/// </para>
/// </summary>
public class CircularoOptions
{
    public const string SectionName = "Leasing:Circularo";

    public bool Enabled { get; set; }

    /// <summary>API base including the version, e.g. the sandbox <c>https://app.sandbox.circularo.com/api/v1</c>.</summary>
    public string BaseUrl { get; set; } = "https://app.sandbox.circularo.com/api/v1";

    /// <summary>API token/key created in Circularo. Sent as the <c>?token=</c> query parameter on every request.</summary>
    public string ApiKey { get; set; } = "";

    // ---- create-document shape ----
    /// <summary>Document catalogue type. The default is <c>d_default</c>.</summary>
    public string DocumentType { get; set; } = "d_default";

    /// <summary>Definition type sent at the top level of the create-document body. Uploaded docs use <c>ext</c>.</summary>
    public string DefinitionType { get; set; } = "ext";

    /// <summary>Workflow definition name (a string, not a list). The default archive workflow is <c>wf_archive</c>.</summary>
    public string Workflow { get; set; } = "wf_archive";

    // ---- share / signature-field placement ----
    /// <summary>Signature field type: <c>signature</c> (a hand/UAE Pass signature) or <c>stamp</c> (e-seal).</summary>
    public string SignatureType { get; set; } = "signature";

    /// <summary>1-based page the signature field is placed on. 0 (default) means the document's last page.</summary>
    public int SignaturePage { get; set; }

    /// <summary>Signature box placement on the page, as fractions (0–1) of page width/height, with the
    /// origin at the TOP-LEFT (X →right, Y →down). These defaults overlay the fixed "Sign here" box printed
    /// at the bottom-right of the landlord summary (see LandlordSummaryPdfService for the derivation). If the
    /// field still lands slightly off in Circularo, nudge these four values in appsettings — no code change
    /// needed.</summary>
    public double SignPercentX { get; set; } = 0.583;
    public double SignPercentY { get; set; } = 0.862;
    public double SignPercentWidth { get; set; } = 0.370;
    public double SignPercentHeight { get; set; } = 0.083;

    /// <summary>Whether signers must sign in order (true) or in parallel (false).</summary>
    public bool Sequential { get; set; } = true;

    /// <summary>How often (minutes) the background poller checks Circularo for completed signatures, so signed
    /// documents land in the portal automatically even when the completion webhook can't reach the server.
    /// 0 disables polling (rely on the webhook / manual "Check signature status" only). Default 5.</summary>
    public int PollIntervalMinutes { get; set; } = 5;

    /// <summary>Attach the completed/signed document to the completion email Circularo sends.</summary>
    public bool SendAttachmentsOnComplete { get; set; } = true;

    /// <summary>Subject line recorded for the signature request (kept for reference/emailing).</summary>
    public string RequestSubject { get; set; } = "Signature required — Landlord Registration";

    /// <summary>Message body Circularo shows/sends to the signer.</summary>
    public string RequestMessage { get; set; } =
        "Please review and sign your landlord registration document for Dubai Investments.";

    // ---- webhook ----
    /// <summary>Shared secret used to validate incoming signature webhooks. Circularo signs the raw body with
    /// HMAC-SHA256 and sends the lowercase hex digest in the <c>X-Circularo-Signature</c> header. Empty disables
    /// the check (also matched against a legacy <c>X-Webhook-Secret</c> header / <c>?secret=</c> value).</summary>
    public string WebhookSecret { get; set; } = "";

    // ---- UAE Pass (identity of the signer is bound via the signer email; Circularo handles UAE Pass signing) ----
    /// <summary>Retained for configuration visibility. In the proven share payload the signer's identity is bound
    /// through their (UAE-Pass-verified) email — Circularo drives the UAE Pass signing experience itself.</summary>
    public bool RequireUaePassSignature { get; set; } = true;
}
