using System.Reflection;

namespace SupplierPortal.Leasing.Web;

/// <summary>
/// Cache-busting token appended to static CSS/JS links in App.razor. Bump the value whenever
/// wwwroot/app.css (or another non-fingerprinted asset) changes, so browsers fetch the new file
/// instead of serving a stale cached copy. The Blazor scoped-styles bundle is fingerprinted
/// automatically and does not need this. The Build* members are shown in the topbar so it's clear
/// which version is live on the site; they derive from the built assembly.
/// </summary>
public static class AppVersion
{
    public const string Token = "20261023a";

    /// <summary>Build/publish timestamp (UTC), from the built assembly's file time.</summary>
    public static DateTime BuildDateUtc { get; } = ComputeBuildDate();

    /// <summary>Human-readable build number, e.g. <c>2026.10.23.0926</c> (UTC).</summary>
    public static string BuildNumber { get; } = BuildDateUtc.ToString("yyyy.MM.dd.HHmm");

    /// <summary>Short git commit the build was made from (empty if git was unavailable at build time).</summary>
    public static string Commit { get; } = ComputeCommit();

    /// <summary>Build date/time shown in Gulf Standard Time (UTC+4, no daylight saving).</summary>
    public static string BuildDateDisplay { get; } = BuildDateUtc.AddHours(4).ToString("dd MMM yyyy, HH:mm") + " GST";

    private static DateTime ComputeBuildDate()
    {
        try
        {
            var path = typeof(AppVersion).Assembly.Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return File.GetLastWriteTimeUtc(path);
        }
        catch { /* fall back below */ }
        return DateTime.UtcNow;
    }

    private static string ComputeCommit()
    {
        try
        {
            var v = typeof(AppVersion).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "GitCommit")?.Value;
            return string.IsNullOrWhiteSpace(v) ? "" : v!.Trim();
        }
        catch { return ""; }
    }
}
