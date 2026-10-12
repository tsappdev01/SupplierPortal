using SupplierPortal.Contracts;

namespace SupplierPortal.SelfService.Services;

/// <summary>
/// Acquires and caches the OAuth2 client-credentials access token the portal uses to authenticate to
/// the internal API. The token is cached app-wide and refreshed shortly before expiry, so a burst of
/// supplier requests does not hammer the token endpoint. The client secret lives only in this DMZ box's
/// gitignored config — it is the portal's single credential and grants no database access on its own.
/// </summary>
public class PortalTokenProvider(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<PortalTokenProvider> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? cachedToken;
    private DateTime expiresAtUtc = DateTime.MinValue;

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        // Refresh 60s before actual expiry to avoid using a token that dies mid-request.
        if (cachedToken is not null && DateTime.UtcNow < expiresAtUtc.AddSeconds(-60))
        {
            return cachedToken;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (cachedToken is not null && DateTime.UtcNow < expiresAtUtc.AddSeconds(-60))
            {
                return cachedToken;
            }

            var clientId = config["Api:ClientId"] ?? "";
            var clientSecret = config["Api:ClientSecret"] ?? "";
            var http = httpClientFactory.CreateClient("Api");

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            });

            using var response = await http.PostAsync("/connect/token", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Token endpoint returned {Status}", response.StatusCode);
                return null;
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (token is null || string.IsNullOrEmpty(token.access_token))
            {
                return null;
            }

            cachedToken = token.access_token;
            expiresAtUtc = DateTime.UtcNow.AddSeconds(token.expires_in);
            return cachedToken;
        }
        finally
        {
            gate.Release();
        }
    }
}
