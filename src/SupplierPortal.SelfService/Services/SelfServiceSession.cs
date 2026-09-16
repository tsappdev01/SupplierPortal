using System.Security.Claims;

namespace SupplierPortal.SelfService.Services;

/// <summary>
/// Holds the authenticated supplier session for the current request/circuit. The session is persisted
/// in an encrypted, HttpOnly, Secure, SameSite=Strict authentication cookie (set server-side via the
/// /account/ss-signin endpoint), so a page refresh keeps the verified session without re-entering the
/// OTP, and the value is never readable or forgeable from the browser. This type just hydrates the
/// session from the signed-in principal's claims and builds the claims for sign-in.
/// </summary>
public class SelfServiceSession
{
    public const string ClaimToken = "ss:token";
    public const string ClaimLink = "ss:link";
    public const string ClaimSupplier = "ss:supplier";
    public const string ClaimEmail = "ss:email";
    public const string ClaimExpires = "ss:expires";

    public string? SessionToken { get; set; }
    /// <summary>The raw link token this session was verified against, so a stale cookie from a different
    /// link (same browser, another supplier) never short-circuits that link's OTP.</summary>
    public string? LinkToken { get; set; }
    public string? SupplierName { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsActive =>
        !string.IsNullOrEmpty(SessionToken) && (ExpiresAtUtc is null || ExpiresAtUtc > DateTime.UtcNow);

    /// <summary>True when this session is active AND was verified against the given link token.</summary>
    public bool IsActiveFor(string linkToken) =>
        IsActive && !string.IsNullOrEmpty(LinkToken) && LinkToken == linkToken;

    /// <summary>Hydrates this session from the signed-in cookie principal, if present and unexpired.</summary>
    public void PopulateFrom(ClaimsPrincipal? user)
    {
        var token = user?.FindFirst(ClaimToken)?.Value;
        if (string.IsNullOrEmpty(token))
        {
            Clear();
            return;
        }

        SessionToken = token;
        LinkToken = user!.FindFirst(ClaimLink)?.Value;
        SupplierName = user.FindFirst(ClaimSupplier)?.Value;
        ContactEmail = user.FindFirst(ClaimEmail)?.Value;
        ExpiresAtUtc = DateTime.TryParse(
            user.FindFirst(ClaimExpires)?.Value, null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var exp)
            ? exp
            : null;
    }

    /// <summary>Builds the claims stored in the auth cookie for a freshly verified session.</summary>
    public static IEnumerable<Claim> BuildClaims(string sessionToken, string? linkToken, string? supplierName, string? contactEmail, DateTime? expiresAtUtc)
    {
        yield return new Claim(ClaimToken, sessionToken);
        if (!string.IsNullOrEmpty(linkToken)) { yield return new Claim(ClaimLink, linkToken); }
        if (!string.IsNullOrEmpty(supplierName)) { yield return new Claim(ClaimSupplier, supplierName); }
        if (!string.IsNullOrEmpty(contactEmail)) { yield return new Claim(ClaimEmail, contactEmail); }
        if (expiresAtUtc is not null) { yield return new Claim(ClaimExpires, expiresAtUtc.Value.ToString("o")); }
    }

    public void Clear()
    {
        SessionToken = null;
        LinkToken = null;
        SupplierName = null;
        ContactEmail = null;
        ExpiresAtUtc = null;
    }
}
