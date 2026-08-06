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

    public static bool IsAdministrator(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Models.UserType.Administrator.ToString());
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
