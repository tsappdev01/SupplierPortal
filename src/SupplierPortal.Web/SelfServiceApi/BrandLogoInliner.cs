using Microsoft.Extensions.Caching.Memory;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>
/// Turns a business-unit logo URL into a self-contained data URI by fetching it server-side (the portal
/// can reach the storage account even when it is not publicly reachable from a supplier's browser). This
/// makes the self-service header reliably show the configured logo instead of falling back to the default
/// when the blob URL is network-restricted. If the value is already a data URI, or the fetch fails, the
/// original value is returned unchanged. Results are cached briefly to avoid re-fetching per request.
/// </summary>
public class BrandLogoInliner(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<BrandLogoInliner> logger)
{
    private const long MaxInlineBytes = 2 * 1024 * 1024; // don't inline anything huge

    public async Task<string?> InlineIfUrlAsync(string? src)
    {
        // Already empty or a data: URI — nothing to fetch.
        if (string.IsNullOrWhiteSpace(src) || !src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return src;
        }

        if (cache.TryGetValue($"ss-logo:{src}", out string? cached))
        {
            return cached;
        }

        try
        {
            var http = httpClientFactory.CreateClient("brandlogo");
            using var resp = await http.GetAsync(src);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Brand logo fetch returned {Status} for {Src}; using the URL as-is.", resp.StatusCode, src);
                return src;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0 || bytes.Length > MaxInlineBytes)
            {
                return src;
            }

            var contentType = resp.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                contentType = "image/png";
            }

            var dataUri = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
            cache.Set($"ss-logo:{src}", dataUri, TimeSpan.FromHours(6));
            return dataUri;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Brand logo fetch failed for {Src}; using the URL as-is.", src);
            return src;
        }
    }
}
