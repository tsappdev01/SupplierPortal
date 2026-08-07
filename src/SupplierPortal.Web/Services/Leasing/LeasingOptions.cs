namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Configuration for the Landlord / Leasing module (bound to the <c>Leasing</c> section of appsettings).
/// </summary>
public class LeasingOptions
{
    public const string SectionName = "Leasing";

    /// <summary>Dubai Municipality zones exposed as the fixed list of values in the Land Register.
    /// A property code is incomplete without its zone (e.g. <c>597-1000</c>); the zone is the segment
    /// before the first dash. Defaults to the two DIP zones.</summary>
    public string[] Zones { get; set; } = ["597", "598"];
}
