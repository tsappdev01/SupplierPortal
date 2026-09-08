namespace SupplierPortal.Web.Services;

public enum ToastLevel { Success, Error, Warning, Info }

public record ToastMessage(Guid Id, ToastLevel Level, string Text);

public class ToastService
{
    private readonly List<ToastMessage> toasts = [];

    public IReadOnlyList<ToastMessage> Toasts => toasts;

    public event Action? OnChange;

    public void ShowSuccess(string text) => Show(ToastLevel.Success, text);
    public void ShowError(string text) => Show(ToastLevel.Error, text);
    public void ShowWarning(string text) => Show(ToastLevel.Warning, text);
    public void ShowInfo(string text) => Show(ToastLevel.Info, text);

    /// <summary>Shows each message as its own error toast (one per line) rather than one long concatenated
    /// toast, so a list of validation errors stays readable. Blank entries are skipped.</summary>
    public void ShowErrors(IEnumerable<string> texts)
    {
        foreach (var text in texts)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                Show(ToastLevel.Error, text);
            }
        }
    }

    private void Show(ToastLevel level, string text)
    {
        toasts.Add(new ToastMessage(Guid.NewGuid(), level, text));
        OnChange?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        toasts.RemoveAll(t => t.Id == id);
        OnChange?.Invoke();
    }
}
