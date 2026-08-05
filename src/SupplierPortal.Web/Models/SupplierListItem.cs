namespace SupplierPortal.Web.Models;

/// <summary>Lightweight projection of a Supplier for the Manage Supplier dashboard list: only the
/// columns actually displayed, so the list query avoids materializing the ~20 lookup navigation
/// properties (and their joins) that <see cref="Supplier"/>'s full entity graph carries but the
/// list view never renders.</summary>
public class SupplierListItem
{
    public int Id { get; set; }
    public string SupplierNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? TaxOrganizationTypeName { get; set; }
    public string? FirstName { get; set; }
    public string? SupplierTypeName { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? TradeLicenseNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? StatusName { get; set; }
    public bool IsActive { get; set; }
    public bool IsDraft { get; set; }
    public int BusinessUnitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Count of unacknowledged field changes the supplier made via self-service; drives
    /// the pending-review bell icon on the dashboard row.</summary>
    public int PendingSupplierChangeCount { get; set; }
}
