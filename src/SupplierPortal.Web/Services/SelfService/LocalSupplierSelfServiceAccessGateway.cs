namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Internal-zone implementation of <see cref="ISupplierSelfServiceAccessGateway"/>: resolves the
/// token against the database and drives OTP directly through <see cref="SupplierAccessTokenService"/> and
/// <see cref="EmailBrandingService"/>. Used when the instance is deployed Internal.</summary>
public class LocalSupplierSelfServiceAccessGateway(
    SupplierAccessTokenService accessTokenService, EmailBrandingService branding)
    : ISupplierSelfServiceAccessGateway
{
    public async Task<SsLinkInfo> ResolveAsync(string token)
    {
        var t = await accessTokenService.GetValidTokenAsync(token);
        if (t is null)
        {
            return new SsLinkInfo(false, null, null, null, false);
        }

        var buId = t.Supplier?.BusinessUnitId;
        var logo = buId is null ? null : await branding.GetBusinessUnitLogoSrcAsync(buId);
        var needsFresh = t.OtpGeneratedAt is null || t.OtpExpiresAt is null || t.OtpExpiresAt < DateTime.UtcNow;
        return new SsLinkInfo(true, t.Supplier?.SupplierName, buId, logo, needsFresh);
    }

    public async Task<(bool Success, string? Error)> SendOtpAsync(string token)
    {
        var t = await accessTokenService.GetValidTokenAsync(token);
        return t is null
            ? (false, "This link is invalid or has expired.")
            : await accessTokenService.GenerateAndSendOtpAsync(t.Id);
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code)
    {
        var t = await accessTokenService.GetValidTokenAsync(token);
        return t is null
            ? (false, "This link is invalid or has expired.", null)
            : await accessTokenService.VerifyOtpAsync(t.Id, code);
    }
}
