using System.Text.RegularExpressions;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Validates landlord contact/signatory email and mobile numbers. Emails must be well-formed and mobiles
/// must be valid UAE mobile numbers (<c>+9715XXXXXXXX</c> / <c>05XXXXXXXX</c>). These identifiers are the
/// ones the landlord's UAE Pass account is keyed on; the one-time code sent to the primary email is the
/// current proof of control, and a full UAE Pass account check can be layered on top later.
/// </summary>
public static partial class ContactValidation
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        email = email.Trim();
        if (!EmailRegex().IsMatch(email)) return false;
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }

    /// <summary>Normalises a UAE mobile to canonical <c>+9715XXXXXXXX</c>, or null when it isn't a valid
    /// UAE mobile. Accepts <c>+971 5X XXX XXXX</c>, <c>009715…</c>, <c>05XXXXXXXX</c> and bare <c>5XXXXXXXX</c>,
    /// with spaces, dashes or parentheses.</summary>
    public static string? NormalizeUaeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile)) return null;
        var d = Regex.Replace(mobile, @"[\s\-()]", "");
        d = d.StartsWith('+') ? d[1..] : d;
        if (d.StartsWith("00971")) d = d[5..];
        else if (d.StartsWith("971")) d = d[3..];
        else if (d.StartsWith("0")) d = d[1..];
        // Now expect a 9-digit national number starting with 5 (UAE mobile).
        return Regex.IsMatch(d, @"^5\d{8}$") ? $"+971{d}" : null;
    }

    public static bool IsValidUaeMobile(string? mobile) => NormalizeUaeMobile(mobile) is not null;
}
