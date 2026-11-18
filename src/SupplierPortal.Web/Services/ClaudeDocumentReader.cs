using System.Text;
using System.Text.Json;

namespace SupplierPortal.Web.Services;

/// <summary>Generic Anthropic (Claude) document reader: sends an uploaded file (PDF or image) plus a
/// caller-supplied extraction prompt to the Messages API and returns the model's JSON answer as a flat
/// string dictionary. Used for the supporting documents (VAT certificate, certificate of incorporation,
/// bank confirmation letter, proof of address) whose fields auto-fill the form. Shares the same "Claude"
/// configuration block as <see cref="ClaudeTradeLicenseReader"/> (ApiKey / Model / BaseUrl / ApiVersion).
/// Never throws to the caller — any failure returns a null field set with a diagnostic string.</summary>
public interface IDocumentFieldReader
{
    /// <summary>True when an API key is configured so the reader can actually run.</summary>
    bool IsEnabled { get; }

    /// <summary>Best-effort: returns the extracted fields (flat key→value, nulls dropped) and the raw model
    /// text (JSON, or a short error) for diagnostics. Never throws.</summary>
    Task<(Dictionary<string, string>? Fields, string? Raw)> ReadFieldsAsync(
        byte[] fileBytes, string? contentType, string prompt, CancellationToken ct = default);
}

public sealed class ClaudeDocumentReader(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ClaudeDocumentReader> logger) : IDocumentFieldReader
{
    private readonly string? _apiKey = config["Claude:ApiKey"];
    private readonly string _model = string.IsNullOrWhiteSpace(config["Claude:Model"]) ? "claude-sonnet-5-5" : config["Claude:Model"]!;
    private readonly string _baseUrl = (string.IsNullOrWhiteSpace(config["Claude:BaseUrl"]) ? "https://api.anthropic.com" : config["Claude:BaseUrl"]!).TrimEnd('/');
    private readonly string _apiVersion = string.IsNullOrWhiteSpace(config["Claude:ApiVersion"]) ? "2023-06-01" : config["Claude:ApiVersion"]!;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<(Dictionary<string, string>? Fields, string? Raw)> ReadFieldsAsync(
        byte[] fileBytes, string? contentType, string prompt, CancellationToken ct = default)
    {
        if (!IsEnabled || fileBytes is null || fileBytes.Length == 0) { return (null, null); }

        try
        {
            var mediaType = ResolveMediaType(contentType, fileBytes);
            var source = new { type = "base64", media_type = mediaType, data = Convert.ToBase64String(fileBytes) };
            object fileBlock = mediaType == "application/pdf"
                ? new { type = "document", source }
                : new { type = "image", source };

            var body = new
            {
                model = _model,
                max_tokens = 2048,
                messages = new[]
                {
                    new { role = "user", content = new object[] { fileBlock, new { type = "text", text = prompt } } }
                }
            };

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(120);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages");
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", _apiVersion);
            req.Headers.Add("anthropic-beta", "pdfs-2024-09-25"); // PDF documents support
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Claude document read failed ({Code}): {Body}", (int)resp.StatusCode, Truncate(json, 500));
                return (null, $"HTTP {(int)resp.StatusCode}: {Truncate(json, 1500)}");
            }

            var answer = ExtractAnswerText(json);
            var payload = ExtractJsonObject(answer);
            if (string.IsNullOrWhiteSpace(payload)) { return (null, answer); }

            using var parsed = JsonDocument.Parse(payload);
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in parsed.RootElement.EnumerateObject())
            {
                var value = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields[prop.Name] = value!.Trim();
                }
            }
            var pretty = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
            return (fields, pretty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude document read failed.");
            return (null, $"error: {ex.Message}");
        }
    }

    private static string ResolveMediaType(string? contentType, byte[] bytes)
    {
        var ct = (contentType ?? "").ToLowerInvariant();
        if (ct.Contains("pdf")) { return "application/pdf"; }
        if (ct.Contains("png")) { return "image/png"; }
        if (ct.Contains("jpeg") || ct.Contains("jpg")) { return "image/jpeg"; }
        if (ct.Contains("webp")) { return "image/webp"; }
        if (ct.Contains("gif")) { return "image/gif"; }
        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) { return "application/pdf"; } // %PDF
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50) { return "image/png"; }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) { return "image/jpeg"; }
        return "application/pdf";
    }

    private static string ExtractAnswerText(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("content", out var content)) { return ""; }
            var sb = new StringBuilder();
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var t) && t.GetString() == "text"
                    && block.TryGetProperty("text", out var txt))
                {
                    sb.Append(txt.GetString());
                }
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return null; }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text.Substring(start, end - start + 1) : null;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
