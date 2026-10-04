using System.Net.Http.Json;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>DMZ implementation of <see cref="ISupplierSelfServiceAccessGateway"/>: calls the internal
/// Self-Service API over HTTPS (the "SelfServiceApi" typed HttpClient carries the base URL and shared
/// key). No database access.</summary>
public class HttpSupplierSelfServiceAccessGateway(IHttpClientFactory httpFactory)
    : ISupplierSelfServiceAccessGateway
{
    private HttpClient Client => httpFactory.CreateClient("SelfServiceApi");

    public async Task<SsLinkInfo> ResolveAsync(string token)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/supplier/resolve", new SsTokenRequest(token));
            if (!resp.IsSuccessStatusCode)
            {
                return new SsLinkInfo(false, null, null, null, false);
            }
            return await resp.Content.ReadFromJsonAsync<SsLinkInfo>()
                   ?? new SsLinkInfo(false, null, null, null, false);
        }
        catch
        {
            return new SsLinkInfo(false, null, null, null, false);
        }
    }

    public async Task<(bool Success, string? Error)> SendOtpAsync(string token)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/supplier/otp/send", new SsTokenRequest(token));
            var result = await resp.Content.ReadFromJsonAsync<SsResult>();
            return result is null ? (false, "The service is unavailable. Please try again.") : (result.Success, result.Error);
        }
        catch
        {
            return (false, "The service is temporarily unavailable. Please try again.");
        }
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/supplier/otp/verify", new SsOtpVerifyRequest(token, code));
            var result = await resp.Content.ReadFromJsonAsync<SsVerifyResult>();
            return result is null
                ? (false, "The service is unavailable. Please try again.", null)
                : (result.Success, result.Error, result.SessionToken);
        }
        catch
        {
            return (false, "The service is temporarily unavailable. Please try again.", null);
        }
    }
}
