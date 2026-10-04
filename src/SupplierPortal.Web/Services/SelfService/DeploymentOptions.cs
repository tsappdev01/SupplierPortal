namespace SupplierPortal.Web.Services.SelfService;

/// <summary>How this instance is deployed. Drives which self-service gateway implementation is used and
/// (later stages) which routes are served.</summary>
public enum DeploymentMode
{
    /// <summary>Full application inside the trusted zone: portal UI, database, and the Self-Service API.
    /// Self-service uses the database directly (the default).</summary>
    Internal = 0,

    /// <summary>Public DMZ front-end: serves only the self-service pages and reaches the trusted zone
    /// through the internal Self-Service API (no database, file share or directory access).</summary>
    Dmz = 1,
}

/// <summary>Bound from the "Deployment" configuration section.</summary>
public class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>Instance role. Defaults to <see cref="DeploymentMode.Internal"/> so existing single-server
    /// deployments are unaffected when the section is absent.</summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.Internal;

    public bool IsDmz => Mode == DeploymentMode.Dmz;
}
