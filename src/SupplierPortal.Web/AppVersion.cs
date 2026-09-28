namespace SupplierPortal.Web;

/// <summary>A cache-busting token that changes on every build/deploy. Appended as a <c>?v=</c> query string
/// to the static CSS links in <c>App.razor</c> so browsers fetch the current stylesheet after an update
/// instead of serving a stale cached copy (the Blazor scoped-CSS bundle link has no fingerprint of its own).</summary>
public static class AppVersion
{
    /// <summary>Stable for the life of the process; derived from the built assembly's timestamp so it changes
    /// whenever the app is rebuilt or redeployed.</summary>
    public static string Token { get; } = ComputeToken();

    private static string ComputeToken()
    {
        try
        {
            var path = typeof(AppVersion).Assembly.Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return File.GetLastWriteTimeUtc(path).Ticks.ToString("x");
        }
        catch { /* fall back to a per-process token below */ }
        return Guid.NewGuid().ToString("N");
    }
}
