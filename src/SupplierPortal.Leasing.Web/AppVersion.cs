namespace SupplierPortal.Leasing.Web;

/// <summary>
/// Cache-busting token appended to static CSS/JS links in App.razor. Bump the value whenever
/// wwwroot/app.css (or another non-fingerprinted asset) changes, so browsers fetch the new file
/// instead of serving a stale cached copy. The Blazor scoped-styles bundle is fingerprinted
/// automatically and does not need this.
/// </summary>
public static class AppVersion
{
    public const string Token = "20261021a";
}
