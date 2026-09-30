namespace SupplierPortal.Web.Services;

/// <summary>
/// Drives the application-wide blocking overlay (DiBlockingOverlay in
/// MainLayout). Scoped, so each Blazor circuit has its own instance.
/// Use for critical operations that must complete before the user does
/// anything else (document generation, submission, long saves).
/// </summary>
public class LoadingService
{
    public bool IsVisible { get; private set; }
    public string? Title { get; private set; }
    public string? Sub { get; private set; }

    public event Action? OnChange;

    /// <summary>Show the blocking overlay with a caption and optional sub-line.</summary>
    public void Show(string? title = "Processing…", string? sub = "Please do not close this page.")
    {
        Title = title;
        Sub = sub;
        IsVisible = true;
        OnChange?.Invoke();
    }

    /// <summary>Hide the overlay.</summary>
    public void Hide()
    {
        IsVisible = false;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Run an async action with the overlay shown for its duration — the
    /// overlay is always hidden afterwards, even if the action throws.
    /// </summary>
    public async Task RunAsync(Func<Task> action, string? title = "Processing…", string? sub = "Please do not close this page.")
    {
        Show(title, sub);
        try
        {
            await action();
        }
        finally
        {
            Hide();
        }
    }
}
