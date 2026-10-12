using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>An organizational department a <see cref="User"/> can belong to.</summary>
public class Department
{
    public int Id { get; set; }

    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
