namespace SupplierPortal.Web.Services;

/// <summary>
/// Single sign-on (OpenID Connect) configuration, bound from the <c>Sso</c> config section. Real
/// values (client id/secret) live in the gitignored appsettings.Local.json on the server, never in
/// committed appsettings. When enabled, the login screen federates to the identity provider
/// (e.g. Microsoft Entra ID) and the signed-in email is matched to an existing app user.
/// </summary>
public class SsoOptions
{
    public const string SectionName = "Sso";

    /// <summary>Master switch. When false (or Authority/ClientId missing) SSO is off.</summary>
    public bool Enabled { get; set; }

    /// <summary>OIDC authority, e.g. https://login.microsoftonline.com/&lt;tenant-id&gt;/v2.0.</summary>
    public string Authority { get; set; } = "";

    /// <summary>Application (client) id registered with the identity provider.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Client secret for the confidential-client authorization-code flow.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>Redirect path registered with the IdP for sign-in.</summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>Redirect path registered with the IdP for sign-out.</summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>True only when a usable OIDC configuration is present; otherwise SSO is skipped and
    /// the app falls back to the local administrator sign-in form.</summary>
    public bool IsEnabled =>
        Enabled && !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}
