namespace SupplierPortal.Web.Services;

/// <summary>Circuit-scoped holder for the public layout's brand logo. A public page (e.g. the supplier
/// self-service OTP screen) resolves the relevant business unit's logo and calls <see cref="SetLogo"/>;
/// the interactive header island subscribes to <see cref="OnChange"/> and re-renders with it. Kept tiny
/// and UI-only so it can be shared across the layout island and the page in the same circuit.</summary>
public class PublicBrandingState
{
    /// <summary>The logo image src to show in the header (a hosted URL or an embedded data URI), or null
    /// to use the default Dubai Investments logo.</summary>
    public string? LogoSrc { get; private set; }

    public event Action? OnChange;

    public void SetLogo(string? logoSrc)
    {
        if (LogoSrc == logoSrc)
        {
            return;
        }

        LogoSrc = logoSrc;
        OnChange?.Invoke();
    }
}
