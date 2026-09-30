using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>Tracks one supplier self-service notification send (or admin test link):
/// the opaque link token, its OTP challenge state, and the short-lived session token
/// minted after a successful OTP verification.</summary>
public class SupplierAccessToken
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public bool IsAdminTest { get; set; }
    public DateTime? UsedAt { get; set; }

    [MaxLength(256)]
    public string? OtpCodeHash { get; set; }

    public DateTime? OtpGeneratedAt { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttemptCount { get; set; }
    public int OtpSendCount { get; set; }
    public DateTime? OtpVerifiedAt { get; set; }

    [MaxLength(64)]
    public string? SessionTokenHash { get; set; }

    public DateTime? SessionExpiresAt { get; set; }
}
