namespace SupplierPortal.Web.Services;

/// <summary>Custom claim types used across authentication and authorization.</summary>
public static class AppClaims
{
    public const string AppUser = "app_user";
    public const string AppUserId = "app_user_id";
    public const string RoleResolved = "role_resolved";
    public const string BreakGlass = "break_glass";
    public const string BreakGlassExpiresUtc = "break_glass_expires";
}
