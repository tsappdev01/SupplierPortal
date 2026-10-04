namespace SupplierPortal.SelfService.Services;

/// <summary>
/// Circuit-scoped holder for the public header's brand logo. A page resolves the relevant business
/// unit's logo (from the OTP response / form bootstrap) and calls <see cref="SetLogo"/>; the layout
/// subscribes to <see cref="OnChange"/> and re-renders with it, falling back to the default logo.
/// </summary>
public class SelfServiceBranding
{
    /// <summary>The business unit logo image src (a hosted URL), or null to use the default logo.</summary>
    public string? LogoUrl { get; private set; }

    public event Action? OnChange;

    public void SetLogo(string? logoUrl)
    {
        var normalised = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        if (LogoUrl == normalised)
        {
            return;
        }
        LogoUrl = normalised;
        OnChange?.Invoke();
    }
}
