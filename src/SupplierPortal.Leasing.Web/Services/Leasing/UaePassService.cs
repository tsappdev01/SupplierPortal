using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>The subset of the UAE Pass user profile the landlord form consumes.</summary>
public record UaePassProfile(string Sub, string? FullName, string? Email, string? Mobile, string? EmiratesId);

public interface IUaePassService
{
    /// <summary>False when running in simulation mode (no live UAE Pass credentials configured).</summary>
    bool Enabled { get; }

    /// <summary>Builds the UAE Pass authorize URL to redirect the browser to (real mode only).</summary>
    string BuildAuthorizeUrl(string redirectUri, string state);

    /// <summary>Exchanges the returned authorization code for the user's UAE Pass profile (real mode).</summary>
    Task<UaePassProfile?> ExchangeAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>Produces a deterministic stand-in profile for simulation mode, seeded from the registration
    /// so the auto-populated form is coherent (the invitation email flows through as the UAE Pass email).</summary>
    UaePassProfile Simulate(Landlord landlord);
}

/// <summary>
/// UAE Pass OpenID Connect client. In real mode it runs the standard authorization-code flow
/// (authorize → token → userinfo). Until client credentials are provisioned it stays disabled and the
/// caller uses <see cref="Simulate"/> so the end-to-end journey (OTP → UAE Pass → locked profile) works.
/// </summary>
public class UaePassService(HttpClient http, IOptions<UaePassOptions> options, ILogger<UaePassService> logger) : IUaePassService
{
    private readonly UaePassOptions _o = options.Value;

    public bool Enabled => _o.Enabled
        && !string.IsNullOrWhiteSpace(_o.ClientId)
        && !string.IsNullOrWhiteSpace(_o.ClientSecret);

    /// <summary>Joins a relative endpoint path onto the configured base URL, dropping any query template.</summary>
    private string Url(string relative)
    {
        var path = relative.Split('?', 2)[0].TrimStart('/');
        return $"{_o.BaseUrl.TrimEnd('/')}/{path}";
    }

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var q = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _o.ClientId,
            ["scope"] = _o.Scope,
            ["state"] = state,
            ["redirect_uri"] = redirectUri,
            ["acr_values"] = _o.AcrValues,
            ["ui_locales"] = "en"
        };
        var query = string.Join("&", q.Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return $"{Url(_o.AuthorizePath)}?{query}";
    }

    public async Task<UaePassProfile?> ExchangeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        try
        {
            // 1) Authorization code → access token (client credentials via HTTP Basic).
            using var tokenReq = new HttpRequestMessage(HttpMethod.Post, Url(_o.Endpoints.AccessToken))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["redirect_uri"] = redirectUri
                })
            };
            var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_o.ClientId}:{_o.ClientSecret}"));
            tokenReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using var tokenResp = await http.SendAsync(tokenReq, ct);
            if (!tokenResp.IsSuccessStatusCode)
            {
                logger.LogWarning("UAE Pass token exchange failed: {Status}", tokenResp.StatusCode);
                return null;
            }
            using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
            if (!tokenDoc.RootElement.TryGetProperty("access_token", out var atEl)) return null;
            var accessToken = atEl.GetString();
            if (string.IsNullOrWhiteSpace(accessToken)) return null;

            // 2) Access token → user info.
            using var infoReq = new HttpRequestMessage(HttpMethod.Get, Url(_o.Endpoints.UserInfo));
            infoReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var infoResp = await http.SendAsync(infoReq, ct);
            if (!infoResp.IsSuccessStatusCode)
            {
                logger.LogWarning("UAE Pass userinfo failed: {Status}", infoResp.StatusCode);
                return null;
            }
            using var infoDoc = JsonDocument.Parse(await infoResp.Content.ReadAsStringAsync(ct));
            var r = infoDoc.RootElement;

            string? S(params string[] names)
            {
                foreach (var n in names)
                    if (r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();
                return null;
            }

            var sub = S("sub", "uuid") ?? Guid.NewGuid().ToString("N");
            var full = S("fullnameEN", "name")
                ?? string.Join(' ', new[] { S("firstnameEN"), S("lastnameEN") }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new UaePassProfile(sub,
                string.IsNullOrWhiteSpace(full) ? null : full,
                S("email"), S("mobile"), S("idn"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UAE Pass exchange error");
            return null;
        }
    }

    public UaePassProfile Simulate(Landlord landlord) => new(
        Sub: $"SIM-{landlord.Id}",
        FullName: landlord.Type == LandlordType.Company
            ? (landlord.CompanyName ?? "Authorised Representative")
            : (landlord.CompanyName ?? "Landlord Owner"),
        Email: landlord.Email,
        Mobile: "+971500000000",
        EmiratesId: null);
}
