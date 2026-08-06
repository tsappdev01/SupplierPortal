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

    /// <summary>Local password hash. Empty for Active Directory users (their password lives in AD).</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Whether this account signs in with a local password or against Active Directory.</summary>
    public AuthSource AuthSource { get; set; } = AuthSource.Local;

    /// <summary>The AD login the user types (UPN e.g. user@dipark.local, or DOMAIN\user). Only set for
    /// <see cref="AuthSource.ActiveDirectory"/> accounts; used to match the sign-in and to bind to AD.</summary>
    [MaxLength(256)]
    public string? AdLogin { get; set; }

    /// <summary>Identity-provider object id (Entra oid), captured on first SSO login and used to match
    /// the user on later logins. Null until an admin-pre-created account is first claimed via SSO.</summary>
    [MaxLength(100)]
    public string? Subject { get; set; }

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
