namespace SupplierPortal.Web.Services;

/// <summary>Authentication configuration, bound from the <c>Auth</c> section. SSO turns on the moment
/// Entra TenantId + ClientId are both present. Real secrets live only in the gitignored
/// appsettings.Local.json on the server, never in committed appsettings.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public EntraIdOptions Entra { get; set; } = new();

    /// <summary>Emails provisioned as Administrator on their first SSO login (bootstrap the first admin).</summary>
    public List<string> BootstrapAdminEmails { get; set; } = [];

    /// <summary>Development-only escape hatch. When true AND no SSO is configured, exposes
    /// <c>/account/dev-login</c> for local sign-in as an administrator with no identity provider.
    /// It is ignored the moment Entra SSO is configured, so it can never bypass SSO where SSO exists.</summary>
    public bool DevBypassEnabled { get; set; }
}

public sealed class EntraIdOptions
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>SSO is on only when both tenant and client id are configured.</summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);

    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}
