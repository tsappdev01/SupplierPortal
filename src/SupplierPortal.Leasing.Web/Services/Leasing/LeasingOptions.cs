namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Configuration for the Landlord / Leasing module (bound to the <c>Leasing</c> section of appsettings).
/// </summary>
public class LeasingOptions
{
    public const string SectionName = "Leasing";

    /// <summary>Dubai Municipality zones exposed as the fixed list of values in the Land Register.
    /// A property code is incomplete without its zone (e.g. <c>597-1000</c>); the zone is the segment
    /// before the first dash. Left empty here on purpose — the configuration binder <b>appends</b> bound
    /// values to a non-empty default (which duplicated the zones); the service falls back to
    /// <see cref="DefaultZones"/> when config supplies none.</summary>
    public string[] Zones { get; set; } = [];

    public static readonly string[] DefaultZones = ["597", "598"];

    /// <summary>SQL Server Database Mail profile used for all landlord emails (OTP + notifications).
    /// Sent unconditionally in every environment, like the supplier emails.</summary>
    public string MailProfile { get; set; } = "BP";
}
