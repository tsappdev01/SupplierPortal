namespace SupplierPortal.Web.Services.SelfService;

/// <summary>What a resolved self-service link exposes to the public pages — the minimum needed to render
/// the OTP screen and brand the header, with no internal identifiers beyond what the page needs.</summary>
public record SsLinkInfo(
    bool Valid,
    string? SupplierName,
    int? BusinessUnitId,
    string? LogoSrc,
    bool NeedsFreshOtp,
    string? BusinessUnitEmail = null);

/// <summary>Gateway for the supplier self-service access flow (link resolve + OTP + session). The Internal
/// implementation calls the domain services directly; the DMZ implementation (added in a later stage)
/// calls the internal Self-Service API. Callers pass only the opaque token/session strings — never
/// internal ids — so the same contract works over HTTP.</summary>
public interface ISupplierSelfServiceAccessGateway
{
    /// <summary>Validates the link token and returns branding/summary info. When <c>Valid</c> is false the
    /// link is unknown or expired.</summary>
    Task<SsLinkInfo> ResolveAsync(string token);

    /// <summary>Generates and emails a fresh OTP for the link. No-op error if the token is invalid.</summary>
    Task<(bool Success, string? Error)> SendOtpAsync(string token);

    /// <summary>Verifies the entered OTP for the link; on success returns an opaque session token to pass
    /// to the form page.</summary>
    Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code);
}
