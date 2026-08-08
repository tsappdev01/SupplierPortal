using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Result of routing a document to Circularo for signature.</summary>
public record CircularoResult(bool Success, string? DocumentId, string? SigningUrl, string? Error);

public interface ICircularoService
{
    /// <summary>True when Circularo is configured (enabled + API key present).</summary>
    bool Enabled { get; }

    /// <summary>Uploads <paramref name="pdf"/> to Circularo and sends it to the given signer for signature.
    /// Circularo emails the signer the request. Best-effort — errors are returned, never thrown.</summary>
    Task<CircularoResult> SendForSignatureAsync(
        byte[] pdf, string fileName, string signerName, string signerEmail, CancellationToken ct = default);
}

/// <summary>
/// Circularo e-signature client (see https://developers.circularo.com). Uses an API key as the bearer token,
/// uploads the document, then creates a signature workflow addressed to the signer's email — Circularo then
/// notifies the signer. Endpoints and the auth scheme are configurable (<see cref="CircularoOptions"/>) so the
/// integration can track the Circularo API version without code changes.
/// </summary>
public class CircularoService(HttpClient http, IOptions<CircularoOptions> options, ILogger<CircularoService> logger) : ICircularoService
{
    private readonly CircularoOptions _o = options.Value;

    public bool Enabled => _o.Enabled && !string.IsNullOrWhiteSpace(_o.ApiKey);

    private string Url(string relative) => $"{_o.BaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";

    private void Authorize(HttpRequestMessage req) =>
        req.Headers.TryAddWithoutValidation("Authorization",
            string.IsNullOrWhiteSpace(_o.AuthScheme) ? _o.ApiKey : $"{_o.AuthScheme} {_o.ApiKey}");

    public async Task<CircularoResult> SendForSignatureAsync(
        byte[] pdf, string fileName, string signerName, string signerEmail, CancellationToken ct = default)
    {
        if (!Enabled) return new(false, null, null, "Circularo is not configured.");
        if (string.IsNullOrWhiteSpace(signerEmail)) return new(false, null, null, "No signer email.");

        try
        {
            // 1) Upload the document (multipart/form-data).
            string? documentId;
            using (var form = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(pdf);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(fileContent, _o.UploadFileField, fileName);

                using var upReq = new HttpRequestMessage(HttpMethod.Post, Url(_o.UploadPath)) { Content = form };
                Authorize(upReq);
                using var upResp = await http.SendAsync(upReq, ct);
                var upBody = await upResp.Content.ReadAsStringAsync(ct);
                if (!upResp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Circularo upload failed {Status}: {Body}", upResp.StatusCode, Trunc(upBody));
                    return new(false, null, null, $"Upload failed ({(int)upResp.StatusCode}).");
                }
                documentId = ExtractId(upBody);
                if (string.IsNullOrWhiteSpace(documentId))
                    return new(false, null, null, "Upload succeeded but no document id was returned.");
            }

            // 2) Create the signature workflow addressed to the signer (Circularo emails them).
            var payload = new
            {
                subject = _o.RequestSubject,
                message = _o.RequestMessage,
                signers = new[] { new { name = signerName, email = signerEmail, role = "signer", order = 1 } },
                recipients = new[] { new { name = signerName, email = signerEmail, action = "sign", order = 1 } }
            };
            var json = JsonSerializer.Serialize(payload);
            using var signReq = new HttpRequestMessage(HttpMethod.Post, Url(_o.SignInvitePath.Replace("{id}", documentId)))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            Authorize(signReq);
            using var signResp = await http.SendAsync(signReq, ct);
            var signBody = await signResp.Content.ReadAsStringAsync(ct);
            if (!signResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Circularo send-for-signature failed {Status}: {Body}", signResp.StatusCode, Trunc(signBody));
                return new(false, documentId, null, $"Send for signature failed ({(int)signResp.StatusCode}).");
            }

            var signingUrl = ExtractString(signBody, "signingUrl", "signUrl", "url", "link");
            logger.LogInformation("Circularo: document {DocId} sent to {Email} for signature.", documentId, signerEmail);
            return new(true, documentId, signingUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo send-for-signature error.");
            return new(false, null, null, ex.Message);
        }
    }

    // ---- response helpers (Circularo returns JSON; ids/urls appear under a few possible keys) ----

    private static string? ExtractId(string json) =>
        ExtractString(json, "_id", "docId", "documentId", "id");

    private static string? ExtractString(string json, params string[] names)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Find(doc.RootElement, names);
        }
        catch { return null; }
    }

    private static string? Find(JsonElement el, string[] names)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    if (names.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        if (p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString();
                        if (p.Value.ValueKind == JsonValueKind.Number) return p.Value.ToString();
                    }
                }
                foreach (var p in el.EnumerateObject())
                {
                    var nested = Find(p.Value, names);
                    if (nested is not null) return nested;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    var nested = Find(item, names);
                    if (nested is not null) return nested;
                }
                break;
        }
        return null;
    }

    private static string Trunc(string s) => s.Length > 500 ? s[..500] : s;
}
