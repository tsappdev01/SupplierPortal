namespace SupplierPortal.Web.Services;

/// <summary>
/// App-wide progress indicator. A slim animated bar at the top of the page is shown automatically
/// whenever a tracked operation runs longer than <see cref="DefaultThresholdMs"/> (3 seconds); quick
/// operations never flash the bar. Mirrors the <see cref="ToastService"/> event pattern — a
/// layout-level <c>LoadingBar</c> component subscribes to <see cref="OnChange"/> and re-renders.
/// Scoped, so each Blazor Server circuit gets its own instance.
/// </summary>
public class LoadingService
{
    /// <summary>Processing must exceed this many milliseconds before the bar appears.</summary>
    public const int DefaultThresholdMs = 3000;

    private int visibleCount;

    /// <summary>True while at least one tracked operation has been running past the threshold.</summary>
    public bool IsVisible => visibleCount > 0;

    public event Action? OnChange;

    /// <summary>
    /// Runs <paramref name="work"/>, showing the global progress bar only if it takes longer than
    /// <paramref name="thresholdMs"/>. Supports nesting/concurrency via reference counting, so several
    /// slow operations can be in flight and the bar stays up until the last one finishes.
    /// </summary>
    public async Task RunAsync(Func<Task> work, int thresholdMs = DefaultThresholdMs)
    {
        using var cts = new CancellationTokenSource();
        var delayedShow = DelayThenShowAsync(thresholdMs, cts.Token);
        try
        {
            await work();
        }
        finally
        {
            cts.Cancel();
            // Await the delayed-show task so we know for certain whether it managed to call Show()
            // before being cancelled; only then do we balance it with Hide().
            if (await delayedShow)
            {
                Hide();
            }
        }
    }

    /// <summary>Result-returning overload of <see cref="RunAsync(Func{Task}, int)"/>.</summary>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, int thresholdMs = DefaultThresholdMs)
    {
        var result = default(T)!;
        await RunAsync(async () => { result = await work(); }, thresholdMs);
        return result;
    }

    private async Task<bool> DelayThenShowAsync(int delayMs, CancellationToken token)
    {
        try
        {
            await Task.Delay(delayMs, token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        Show();
        return true;
    }

    private void Show()
    {
        visibleCount++;
        if (visibleCount == 1)
        {
            OnChange?.Invoke();
        }
    }

    private void Hide()
    {
        if (visibleCount > 0)
        {
            visibleCount--;
        }

        if (visibleCount == 0)
        {
            OnChange?.Invoke();
        }
    }
}
