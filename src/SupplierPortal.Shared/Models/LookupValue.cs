using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class LookupValue
{
    public int Id { get; set; }

    public LookupCategory Category { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Phone dialing code, only populated for Category == Country (e.g. "971").</summary>
    [MaxLength(10)]
    public string? PhoneCode { get; set; }

    public bool IsActive { get; set; } = true;
}
