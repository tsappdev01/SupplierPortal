namespace SupplierPortal.SelfService.Services;

public enum ToastLevel { Success, Error, Info }

public record ToastMessage(Guid Id, ToastLevel Level, string Text);

/// <summary>
/// Minimal circuit-scoped toast queue for the self-service front-end. Pages/components raise
/// <see cref="ShowSuccess"/> / <see cref="ShowError"/> / <see cref="ShowInfo"/>; the <c>Toaster</c>
/// component subscribes to <see cref="OnChange"/> and renders the active toasts, auto-dismissing them.
/// </summary>
public class ToastService
{
    private readonly List<ToastMessage> _messages = new();

    public IReadOnlyList<ToastMessage> Messages => _messages;

    public event Action? OnChange;

    public void ShowSuccess(string? text) => Add(ToastLevel.Success, text);
    public void ShowError(string? text) => Add(ToastLevel.Error, text);
    public void ShowInfo(string? text) => Add(ToastLevel.Info, text);

    /// <summary>Raises one error toast per message (used for form-validation problem lists).</summary>
    public void ShowErrors(IEnumerable<string> texts)
    {
        foreach (var t in texts) { ShowError(t); }
    }

    private void Add(ToastLevel level, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        _messages.Add(new ToastMessage(Guid.NewGuid(), level, text));
        OnChange?.Invoke();
    }

    public void Remove(Guid id)
    {
        var removed = _messages.RemoveAll(m => m.Id == id) > 0;
        if (removed)
        {
            OnChange?.Invoke();
        }
    }
}
