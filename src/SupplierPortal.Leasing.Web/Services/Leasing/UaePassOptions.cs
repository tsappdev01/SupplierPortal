namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// UAE Pass configuration for landlord identity verification, bound to <c>Leasing:UaePass</c> and shaped to
/// match the UAE Pass integration pack (base URL, client credentials and the <c>idshub</c> / <c>trustedx</c>
/// endpoint set). When <see cref="Enabled"/> is false the portal runs in <b>simulation</b> mode: the UAE Pass
/// step still appears and gates the form, but the profile is derived locally instead of calling the live IdP —
/// so the flow is fully demonstrable before a client with our redirect URI is provisioned.
///
/// Identity auto-population (this feature) uses the OpenID Connect authorization-code flow:
/// <c>idshub/authorize</c> → <c>Endpoints.AccessToken</c> (idshub/token) → <c>Endpoints.UserInfo</c>.
/// The <c>trustedx-*</c> endpoints are kept for the future e-signature feature and are not used here.
/// </summary>
public class UaePassOptions
{
    public const string SectionName = "Leasing:UaePass";

    /// <summary>False → simulation mode (no external IdP call). True → real UAE Pass OIDC redirect flow.</summary>
    public bool Enabled { get; set; }

    /// <summary>IdP base URL. Staging: <c>https://stg-id.uaepass.ae/</c>; production: <c>https://id.uaepass.ae/</c>.</summary>
    public string BaseUrl { get; set; } = "https://stg-id.uaepass.ae/";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string GrantType { get; set; } = "authorization_code";

    /// <summary>OIDC scope for the identity/userinfo flow. Name/email/mobile come from the general profile
    /// scope — the e-signature scope in the integration pack is a different product and not used here.</summary>
    public string Scope { get; set; } = "urn:uae:digitalid:profile:general";

    /// <summary>Authentication level (ACR). Low allows both the UAE Pass app and the visitor SOP.</summary>
    public string AcrValues { get; set; } = "urn:safelayer:tws:policies:authentication:level:low";

    /// <summary>Relative path of the authorize endpoint (not always present in the integration pack).</summary>
    public string AuthorizePath { get; set; } = "idshub/authorize";

    public UaePassEndpoints Endpoints { get; set; } = new();
}

/// <summary>Relative endpoint paths from the UAE Pass integration pack (joined onto <see cref="UaePassOptions.BaseUrl"/>).</summary>
public class UaePassEndpoints
{
    /// <summary>Client-credentials token endpoint for the e-signature (trustedx) service — future use.</summary>
    public string Token { get; set; } = "trustedx-authserver/oauth/main-as/token";

    /// <summary>Authorization-code → access-token exchange (identity). Any query template is ignored; the path is used.</summary>
    public string AccessToken { get; set; } = "idshub/token";

    public string CreateSignProcess { get; set; } = "trustedx-resources/esignsp/v2/signer_processes";
    public string FetchSignedDocument { get; set; } = "trustedx-resources/esignsp/v2/documents/";
    public string UserInfo { get; set; } = "idshub/userinfo";
}
