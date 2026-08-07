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

    /// <summary>SQL Server Database Mail profile used for all landlord emails (OTP + notifications).</summary>
    public string MailProfile { get; set; } = "BP";

    /// <summary>Whether landlord emails are actually sent through <see cref="MailProfile"/>. When null
    /// (the default) emails send outside Development and are skipped in Development (so the OTP is surfaced
    /// on-screen for local testing). Set <c>true</c> to force real sends via the BP profile in any
    /// environment, or <c>false</c> to always skip.</summary>
    public bool? SendEmails { get; set; }
}
