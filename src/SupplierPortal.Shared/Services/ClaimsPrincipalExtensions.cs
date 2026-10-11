using System.Security.Claims;

namespace SupplierPortal.Web.Services;

public static class ClaimsPrincipalExtensions
{
    public static List<int> GetBusinessUnitIds(this ClaimsPrincipal principal)
    {
        return principal.FindAll("BusinessUnitId")
            .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }

    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AppClaims.AppUserId)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }

    /// <summary>The Business Unit / company the user belongs to, or null if none is set.</summary>
    public static int? GetCompanyBusinessUnitId(this ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirstValue(AppClaims.CompanyBusinessUnitId), out var id) ? id : null;
    }

    /// <summary>Admin-level access used for data visibility and operational gating: true for a full
    /// Administrator AND for an Application Administrator (a miniature admin with everything except Settings).
    /// Both see all records within the business units assigned to them, unscoped by department/assignee.</summary>
    public static bool IsAdministrator(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Models.UserType.Administrator.ToString())
            || principal.IsInRole(Models.UserType.ApplicationAdministrator.ToString());
    }

    /// <summary>True only for a FULL Administrator — gates the Settings area (users, business units, field
    /// settings, documents, notification groups, scheduled reports, break-glass, audit log), which an
    /// Application Administrator must not access.</summary>
    public static bool IsFullAdministrator(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Models.UserType.Administrator.ToString());
    }

    /// <summary>True when the user has been granted the given portal (from the <c>portal</c> claims).</summary>
    public static bool CanAccessPortal(this ClaimsPrincipal principal, Models.PortalAccess portal)
    {
        return principal.FindAll(AppClaims.Portal).Any(c =>
            Enum.TryParse<Models.PortalAccess>(c.Value, out var p) && p == portal);
    }

    /// <summary>Access to the Leasing / Landlord module — now driven by the Landlord portal grant.</summary>
    public static bool IsLeasingUser(this ClaimsPrincipal principal)
    {
        return principal.CanAccessPortal(Models.PortalAccess.Landlord);
    }

    public static string GetFullName(this ClaimsPrincipal principal)
    {
        return principal.FindFirst("FullName")?.Value ?? principal.Identity?.Name ?? "";
    }

    public static string? GetDesignation(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("Designation")?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool HasPhoto(this ClaimsPrincipal principal)
    {
        return bool.TryParse(principal.FindFirst("HasPhoto")?.Value, out var hasPhoto) && hasPhoto;
    }
}
