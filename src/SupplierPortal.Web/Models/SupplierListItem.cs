namespace SupplierPortal.Web.Models;

/// <summary>Lightweight projection of a Supplier for the Manage Supplier dashboard list: only the
/// columns actually displayed, so the list query avoids materializing the ~20 lookup navigation
/// properties (and their joins) that <see cref="Supplier"/>'s full entity graph carries but the
/// list view never renders.</summary>
public class SupplierListItem
{
    public int Id { get; set; }
    public string SupplierNumber { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? TaxOrganizationTypeName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Contact full name (first + last) for the dashboard "Full Name" column, print and export.</summary>
    public string FullName =>
        string.Join(" ", new[] { FirstName, LastName }.Where(n => !string.IsNullOrWhiteSpace(n)));

    public string? SupplierTypeName { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public string? TradeLicenseNumber { get; set; }
    public string? ContactEmail { get; set; }
    public string? StatusName { get; set; }
    public string? ReviewRemarks { get; set; }
    public bool IsActive { get; set; }
    public bool IsDraft { get; set; }
    public int BusinessUnitId { get; set; }

    /// <summary>The user this supplier is assigned to (from Manage Users), and a display name for the
    /// dashboard column. Null when unassigned.</summary>
    public int? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }

    /// <summary>The department that owns this supplier, and its name for the dashboard column.</summary>
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Count of unacknowledged field changes the supplier made via self-service; drives
    /// the pending-review bell icon on the dashboard row.</summary>
    public int PendingSupplierChangeCount { get; set; }

    /// <summary>Number of documents attached to this supplier; drives the dashboard's
    /// "view attached documents" action (enabled only when there is at least one).</summary>
    public int DocumentCount { get; set; }
}
