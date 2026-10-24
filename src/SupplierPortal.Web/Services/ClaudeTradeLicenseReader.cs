using System.Text;
using System.Text.Json;

namespace SupplierPortal.Web.Services;

/// <summary>Reads a Trade License (PDF or image, text-layer or scanned) into a <see cref="ParsedLicense"/>.
/// The Claude-vision implementation handles DED mainland and free-zone layouts alike; the regex
/// <see cref="TradeLicenseParser"/> remains the fallback when no API key is configured.</summary>
public interface ITradeLicenseReader
{
    /// <summary>True when an API key is configured so the reader can actually run.</summary>
    bool IsEnabled { get; }

    /// <summary>Best-effort: returns the extracted license, or null on any failure (caller falls back).</summary>
    Task<ParsedLicense?> ReadAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default);

    /// <summary>Like <see cref="ReadAsync"/> but also returns the raw model response (the JSON the model
    /// produced, or a short error description) for diagnostics and the admin preview. Never throws.</summary>
    Task<(ParsedLicense? License, string? Raw)> ReadWithRawAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default);
}

/// <summary>Sends the uploaded license to the Anthropic Messages API and maps the returned JSON to a
/// <see cref="ParsedLicense"/>. Configuration (appsettings.Local.json):
///   "Claude": { "ApiKey": "...", "Model": "claude-sonnet-5-5", "BaseUrl": "https://api.anthropic.com" }
/// Needs outbound HTTPS to api.anthropic.com. Never throws to the caller - any failure returns null.</summary>
public sealed class ClaudeTradeLicenseReader(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ClaudeTradeLicenseReader> logger) : ITradeLicenseReader
{
    private readonly string? _apiKey = config["Claude:ApiKey"];
    private readonly string _model = string.IsNullOrWhiteSpace(config["Claude:Model"]) ? "claude-sonnet-5-5" : config["Claude:Model"]!;
    private readonly string _baseUrl = (string.IsNullOrWhiteSpace(config["Claude:BaseUrl"]) ? "https://api.anthropic.com" : config["Claude:BaseUrl"]!).TrimEnd('/');
    private readonly string _apiVersion = string.IsNullOrWhiteSpace(config["Claude:ApiVersion"]) ? "2023-06-01" : config["Claude:ApiVersion"]!;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_apiKey);

    private const string Prompt = """
You are extracting data from a UAE trade/commercial licence. It may be a Dubai DET/DED mainland licence
(a "Commercial / Industrial / Professional License" from the Department of Economy and Tourism), or a
free-zone licence (JAFZA, DAFZA, DMCC, Creative City, Dubai Production/Media/Internet City, DSO, RAKEZ,
SAIF, ...). It is usually bilingual Arabic-English and may be a scan. Read the RENDERED page, not just the
text layer. Prefer the ENGLISH value for every field. Return ONLY a single JSON object (no markdown, no
commentary) with EXACTLY these keys; use null when a value is not present on this licence. Dates as
"DD/MM/YYYY". paidCapital as digits only (no commas). parties is the combined list of
owners/partners/managers/members/general-managers.

Field guidance:
- licenseKind: the licence heading - "Commercial", "Industrial", "Professional", "Tourism", etc.
- issuingAuthority: the issuer/free zone (e.g. "Dubai DET", "JAFZA", "DMCC", "Dubai Production City").
- legalType: the legal form (maps from "Legal Type" or a free-zone "Legal Status"), e.g.
  "Limited Liability Company", "Free Zone LLC", "Sole Establishment".
- tradeName: the trade / business / operating name; legalName: the registered company name.
- issueDate: the ORIGINAL first-issue date (labelled "Issue Date" or "First Issue Date"); do NOT use the
  "Current Issue Date" or the "Print Date". expiryDate: the licence expiry date. On the Dubai layout the
  expiry date is the later date and the issue date is the earlier one.
- mainLicenseNumber: "Main License No." (falls back to the licence number when not separately printed).
- registerNumber: "Register No." / commercial register; dcciNumber: "DCCI No." / chamber membership.
- parcelId: "Parcel ID" / "Plot"; premises: the premises/area/business-unit description.

{
  "licenseNumber": string|null,
  "mainLicenseNumber": string|null,
  "legalName": string|null,
  "tradeName": string|null,
  "legalType": string|null,
  "licenseCategory": string|null,
  "issuingAuthority": string|null,
  "licenseKind": string|null,
  "issueDate": string|null,
  "expiryDate": string|null,
  "registerNumber": string|null,
  "dcciNumber": string|null,
  "dunsNumber": string|null,
  "paidCapital": string|null,
  "capitalCurrency": string|null,
  "activities": [string, ...],
  "poBox": string|null,
  "parcelId": string|null,
  "premises": string|null,
  "email": string|null,
  "phone": string|null,
  "mobile": string|null,
  "address": string|null,
  "parties": [ { "name": string, "personNo": string|null, "nationality": string|null, "role": string|null, "share": string|null } ]
}
""";

    public async Task<ParsedLicense?> ReadAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default)
        => (await ReadWithRawAsync(fileBytes, contentType, ct)).License;

    public async Task<(ParsedLicense? License, string? Raw)> ReadWithRawAsync(byte[] fileBytes, string? contentType, CancellationToken ct = default)
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
                    new { role = "user", content = new object[] { fileBlock, new { type = "text", text = Prompt } } }
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
                logger.LogWarning("Claude license read failed ({Code}): {Body}", (int)resp.StatusCode, Truncate(json, 500));
                return (null, $"HTTP {(int)resp.StatusCode}: {Truncate(json, 1500)}");
            }

            var answer = ExtractAnswerText(json);
            var payload = ExtractJsonObject(answer);
            if (string.IsNullOrWhiteSpace(payload)) { return (null, answer); }

            using var parsed = JsonDocument.Parse(payload);
            // Pretty-print the extracted JSON so the admin preview renders it legibly.
            var pretty = JsonSerializer.Serialize(parsed.RootElement, new JsonSerializerOptions { WriteIndented = true });
            return (Map(parsed.RootElement), pretty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude trade-license read failed; falling back.");
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
        // Sniff by magic bytes when the content type is missing/generic.
        if (bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46) { return "application/pdf"; } // %PDF
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50) { return "image/png"; }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) { return "image/jpeg"; }
        return "application/pdf";
    }

    /// <summary>Concatenates the text blocks of an Anthropic Messages API response.</summary>
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

    /// <summary>Pulls the first {...} JSON object out of the model's answer (tolerates stray prose/fences).</summary>
    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) { return null; }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return (start >= 0 && end > start) ? text.Substring(start, end - start + 1) : null;
    }

    private static ParsedLicense Map(JsonElement o)
    {
        string? S(string key) => o.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim())
            : null;

        var license = new ParsedLicense
        {
            LicenseNumber = S("licenseNumber"),
            MainLicenseNumber = S("mainLicenseNumber"),
            LegalName = S("legalName"),
            TradeName = S("tradeName"),
            BusinessName = S("tradeName") ?? S("legalName"),
            LegalType = S("legalType"),
            LicenseCategory = S("licenseCategory"),
            IssuingAuthority = S("issuingAuthority"),
            LicenseKind = S("licenseKind"),
            IssueDate = S("issueDate"),
            ExpiryDate = S("expiryDate"),
            RegisterNumber = S("registerNumber"),
            DcciNumber = S("dcciNumber"),
            DunsNumber = S("dunsNumber"),
            PaidCapital = S("paidCapital"),
            CapitalCurrency = S("capitalCurrency"),
            PoBox = S("poBox"),
            ParcelId = S("parcelId"),
            Premises = S("premises"),
            Email = S("email"),
            Phone = S("phone"),
            Mobile = S("mobile"),
            Address = S("address")
        };

        if (o.TryGetProperty("activities", out var acts) && acts.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in acts.EnumerateArray())
            {
                var name = a.ValueKind == JsonValueKind.String ? a.GetString()
                    : a.TryGetProperty("name", out var an) ? an.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name)) { license.Activities.Add(new LicenseActivity(name!.Trim(), null)); }
            }
        }

        if (o.TryGetProperty("parties", out var parties) && parties.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in parties.EnumerateArray())
            {
                string? PS(string key) => p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                    ? (string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim())
                    : null;
                var name = PS("name");
                if (string.IsNullOrWhiteSpace(name)) { continue; }
                license.Members.Add(new LicenseMember(name!, PS("personNo"), PS("nationality"), PS("role"), PS("share")));
            }
        }

        return license;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
