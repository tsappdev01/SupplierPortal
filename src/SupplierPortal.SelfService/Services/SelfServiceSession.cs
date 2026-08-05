namespace SupplierPortal.SelfService.Services;

/// <summary>
/// Per-circuit holder for the authenticated supplier session. Lives only in server-side Blazor circuit
/// memory on the DMZ box — never written to the browser, never persisted, gone when the circuit ends.
/// </summary>
public class SelfServiceSession
{
    public string? SessionToken { get; set; }
    public string? SupplierName { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsActive =>
        !string.IsNullOrEmpty(SessionToken) && (ExpiresAtUtc is null || ExpiresAtUtc > DateTime.UtcNow);

    public void Clear()
    {
        SessionToken = null;
        SupplierName = null;
        ContactEmail = null;
        ExpiresAtUtc = null;
    }
}
