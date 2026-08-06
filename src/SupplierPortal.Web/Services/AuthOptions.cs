namespace SupplierPortal.Web.Services;

/// <summary>How users sign in.</summary>
public enum AuthMode
{
    /// <summary>Local username/password (and Active Directory) sign-in against the Users table.</summary>
    Local = 0,

    /// <summary>Federated single sign-on via OpenID Connect (Microsoft Entra ID).</summary>
    Sso = 1
}

/// <summary>Authentication configuration, bound from the <c>Auth</c> section. <see cref="Mode"/> chooses
/// between local sign-in and SSO. Real secrets live only in the gitignored appsettings.Local.json on the
/// server, never in committed appsettings.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Sign-in mode. <c>Local</c> (default) shows the username/password form; <c>Sso</c> uses
    /// Entra ID. If <c>Sso</c> is selected but Entra is not configured, the app falls back to local
    /// sign-in so no one is locked out.</summary>
    public AuthMode Mode { get; set; } = AuthMode.Local;

    public EntraIdOptions Entra { get; set; } = new();

    /// <summary>Emails provisioned as Administrator on their first SSO login (bootstrap the first admin).</summary>
    public List<string> BootstrapAdminEmails { get; set; } = [];

    /// <summary>Development-only escape hatch. When true AND SSO is not the active mode, exposes
    /// <c>/account/dev-login</c> for one-click local sign-in as an administrator (no password).
    /// It is ignored whenever SSO is the active mode, so it can never bypass SSO where SSO is on.</summary>
    public bool DevBypassEnabled { get; set; }

    /// <summary>True when SSO is both selected and actually configured; otherwise the app uses local sign-in.</summary>
    public bool UseSso => Mode == AuthMode.Sso && Entra.Enabled;
}

public sealed class EntraIdOptions
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Entra is configured only when both tenant and client id are present.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);

    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
