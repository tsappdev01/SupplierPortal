using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services;

/// <summary>OCR via Azure AI Document Intelligence (prebuilt-read). Posts the file to the async
/// analyze endpoint, polls the returned Operation-Location until it succeeds, and returns the
/// concatenated <c>analyzeResult.content</c>. Best-effort: returns null when disabled or on any error,
/// so a failed OCR never breaks a document upload.</summary>
public class AzureDocumentIntelligenceOcrService(
    HttpClient http, IOptions<OcrOptions> options, ILogger<AzureDocumentIntelligenceOcrService> logger)
    : IDocumentOcrService
{
    private readonly OcrOptions _options = options.Value;

    public bool IsEnabled => _options.IsEnabled;

    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!_options.IsEnabled || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var endpoint = _options.Endpoint.TrimEnd('/');
            var analyzeUrl =
                $"{endpoint}/documentintelligence/documentModels/{_options.ModelId}:analyze?api-version={_options.ApiVersion}";

            using var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, cancellationToken));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var request = new HttpRequestMessage(HttpMethod.Post, analyzeUrl) { Content = content };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);

            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Azure OCR analyze returned {Status} for {File}", response.StatusCode, Path.GetFileName(filePath));
                return null;
            }

            // Async operation: the result is fetched from the Operation-Location header once it succeeds.
            if (!response.Headers.TryGetValues("Operation-Location", out var locations))
            {
                return null;
            }
            var resultUrl = locations.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(resultUrl))
            {
                return null;
            }

            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(5, _options.TimeoutSeconds));
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(1000, cancellationToken);

                using var poll = new HttpRequestMessage(HttpMethod.Get, resultUrl);
                poll.Headers.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);
                using var pollResponse = await http.SendAsync(poll, cancellationToken);
                if (!pollResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                await using var stream = await pollResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = json.RootElement;
                var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;

                if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    if (root.TryGetProperty("analyzeResult", out var ar) && ar.TryGetProperty("content", out var c))
                    {
                        return c.GetString();
                    }
                    return null;
                }
                if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Azure OCR analyze failed for {File}", Path.GetFileName(filePath));
                    return null;
                }
                // running / notStarted -> keep polling until the deadline
            }

            logger.LogWarning("Azure OCR analyze timed out for {File}", Path.GetFileName(filePath));
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure OCR call failed for {File}", Path.GetFileName(filePath));
            return null;
        }
    }
}
