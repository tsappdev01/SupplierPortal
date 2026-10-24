using System.Text;
using System.Text.Json;

namespace SupplierPortal.Web.Services;

/// <summary>What an Emirates ID / Passport document reader can extract. Any field not present on the
/// supplied document is left null. Dates are kept as the raw "DD/MM/YYYY" string and parsed on save.</summary>
public sealed class ParsedIdentityDocument
{
    public string? EmiratesIdNumber { get; set; }
    public string? EmiratesIdExpiry { get; set; }
    public string? PassportNumber { get; set; }
    public string? PassportExpiry { get; set; }

    /// <summary>True when at least one field was read, so callers can skip an empty result.</summary>
    public bool HasData =>
        !string.IsNullOrWhiteSpace(EmiratesIdNumber) || !string.IsNullOrWhiteSpace(EmiratesIdExpiry)
        || !string.IsNullOrWhiteSpace(PassportNumber) || !string.IsNullOrWhiteSpace(PassportExpiry);
}

/// <summary>Reads an Emirates ID and/or Passport (PDF or image, either side, scanned or not) into a
/// <see cref="ParsedIdentityDocument"/>.</summary>
public interface IIdentityDocumentReader
{
    /// <summary>True when an API key is configured so the reader can actually run.</summary>
    bool IsEnabled { get; }

    /// <summary>Best-effort: returns the extracted identity fields, or null on any failure.</summary>
    Task<ParsedIdentityDocument?> ReadAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default);
}

/// <summary>Sends an uploaded Emirates ID / Passport to the Anthropic Messages API and maps the returned
/// JSON. Shares the same "Claude" configuration block as <see cref="ClaudeTradeLicenseReader"/>
/// (ApiKey / Model / BaseUrl / ApiVersion). Never throws to the caller - any failure returns null.</summary>
public sealed class ClaudeIdentityDocumentReader(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ClaudeIdentityDocumentReader> logger) : IIdentityDocumentReader
{
    private readonly string? _apiKey = config["Claude:ApiKey"];
    private readonly string _model = string.IsNullOrWhiteSpace(config["Claude:Model"]) ? "claude-sonnet-5-5" : config["Claude:Model"]!;
    private readonly string _baseUrl = (string.IsNullOrWhiteSpace(config["Claude:BaseUrl"]) ? "https://api.anthropic.com" : config["Claude:BaseUrl"]!).TrimEnd('/');
    private readonly string _apiVersion = string.IsNullOrWhiteSpace(config["Claude:ApiVersion"]) ? "2023-06-01" : config["Claude:ApiVersion"]!;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    private const string Prompt = """
You are extracting identity-document data from an uploaded file that may be a UAE Emirates ID card
(front and/or back), a passport bio-data page, or both together (scanned or photographed, any orientation).
Read the RENDERED page. Return ONLY a single JSON object (no markdown, no commentary) with EXACTLY these
keys; use null when a value is not present. Dates as "DD/MM/YYYY".

{
  "emiratesIdNumber": string|null,   // the 15-digit Emirates ID number, formatted "784-YYYY-XXXXXXX-X" if shown that way
  "emiratesIdExpiry": string|null,   // the Emirates ID card expiry date
  "passportNumber": string|null,     // the passport number from the bio-data page
  "passportExpiry": string|null      // the passport expiry / date of expiry
}

Notes: the Emirates ID number always starts with 784 and has 15 digits. Do not confuse the ID card number
with the passport number. If only one document is present, fill its fields and leave the other two null.
""";

    public async Task<ParsedIdentityDocument?> ReadAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default)
    {
        if (!IsEnabled || fileBytes is null || fileBytes.Length == 0) { return null; }

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
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = new object[] { fileBlock, new { type = "text", text = Prompt } } }
                }
            };

            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(120);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages");
            req.Headers.Add("x-api-key", _apiKey);
            req.Headers.Add("anthropic-version", _apiVersion);
            req.Headers.Add("anthropic-beta", "pdfs-2024-09-25");
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Claude identity-doc read failed ({Code}): {Body}", (int)resp.StatusCode, Truncate(json, 500));
                return null;
            }

            var answer = ExtractAnswerText(json);
            var payload = ExtractJsonObject(answer);
            if (string.IsNullOrWhiteSpace(payload)) { return null; }

            using var parsed = JsonDocument.Parse(payload);
            var o = parsed.RootElement;
            string? S(string key) => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim())
                : null;
            return new ParsedIdentityDocument
            {
                EmiratesIdNumber = S("emiratesIdNumber"),
                EmiratesIdExpiry = S("emiratesIdExpiry"),
                PassportNumber = S("passportNumber"),
                PassportExpiry = S("passportExpiry")
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude identity-document read failed.");
            return null;
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
        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) { return "application/pdf"; }
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
