namespace SupplierPortal.Web.Services;

/// <summary>
/// Single source of truth for "is this a well-formed e-mail address" across the app. The framework
/// <c>[EmailAddress]</c> attribute only checks for a single '@', which lets vulnerability-scanner and
/// injection payloads through; this enforces a real <c>local@domain.tld</c> shape, a sane length cap and a
/// ReDoS-safe match timeout. Used to reject bad input at registration and to identify junk pending rows.
/// </summary>
public static class EmailValidation
{
    /// <summary>The canonical pattern (keep in sync with the client-side RegularExpression on Register.razor).</summary>
    public const string Pattern = @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$";

    private static readonly System.Text.RegularExpressions.Regex Rx = new(
        Pattern,
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    /// <summary>True when <paramref name="email"/> is a well-formed address of sane length.</summary>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) { return false; }
        try { return Rx.IsMatch(email.Trim()); }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
    }
}
