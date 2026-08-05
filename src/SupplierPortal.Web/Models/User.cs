using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FullName { get; set; }

    [MaxLength(150)]
    public string? Designation { get; set; }

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    public byte[]? ProfilePhoto { get; set; }

    [MaxLength(100)]
    public string? ProfilePhotoContentType { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserType UserType { get; set; } = UserType.NormalUser;

    /// <summary>A user may be assigned to more than one Business Unit; scopes which suppliers they can see.</summary>
    public List<BusinessUnit> BusinessUnits { get; set; } = [];

    public bool IsActive { get; set; } = true;

    /// <summary>The single seeded admin account; deactivated once a real administrator is created.</summary>
    public bool IsDefaultAdmin { get; set; }

    /// <summary>True after public self-registration, until an Administrator links/completes the account via Add User.</summary>
    public bool IsPendingRegistration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
