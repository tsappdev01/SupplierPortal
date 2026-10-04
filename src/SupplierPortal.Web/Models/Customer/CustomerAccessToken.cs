using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models.Customer;

/// <summary>Tracks one customer self-service notification send: the opaque link token, its OTP challenge
/// state, and the short-lived session token minted after a successful OTP verification. Mirrors the
/// supplier <see cref="SupplierPortal.Web.Models.SupplierAccessToken"/> so the customer self-service flow
/// works identically.</summary>
public class CustomerAccessToken
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    [Required, MaxLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    public bool IsAdminTest { get; set; }
    public DateTime? UsedAt { get; set; }

    /// <summary>Which of the customer's two notification addresses this link was emailed to, so the OTP is
    /// routed back to the same inbox that opened the link — "p" = account Primary Email (Contact &amp; Address),
    /// "c" = contact Email (Personal Data). Null for an admin test link (or a legacy single-link send), which
    /// falls back to delivering the code to every address on file. Decided server-side at send time, so routing
    /// does not depend on the DMZ app forwarding a query-string tag.</summary>
    [MaxLength(1)]
    public string? Recipient { get; set; }

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
