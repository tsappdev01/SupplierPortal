using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Result of routing a document to Circularo for signature.</summary>
public record CircularoResult(bool Success, string? DocumentId, string? SigningUrl, string? Error);

/// <summary>Current signing state of a Circularo document, resolved by polling (used when the completion
/// webhook can't reach the server). <paramref name="Reachable"/> is false when Circularo couldn't be
/// contacted, so the caller can tell "not signed yet" apart from "couldn't check".</summary>
public record CircularoStatus(bool Reachable, bool Signed, bool Declined, string? RawStatus, byte[]? SignedPdf);

public interface ICircularoService
{
    /// <summary>True when Circularo is configured (enabled + API token present).</summary>
    bool Enabled { get; }

    /// <summary>Uploads <paramref name="pdf"/> to Circularo and sends it to the given signer for signature.
    /// Circularo emails the signer the request. Best-effort — errors are returned, never thrown.</summary>
    Task<CircularoResult> SendForSignatureAsync(
        byte[] pdf, string fileName, string signerName, string signerEmail, CancellationToken ct = default);

    /// <summary>Attempts to download the signed copy of a document. Returns null when unavailable.</summary>
    Task<byte[]?> DownloadSignedAsync(string documentId, CancellationToken ct = default);

    /// <summary>Polls Circularo for a document's current signing status (and downloads the signed PDF when
    /// complete). Lets the portal reflect a completed signature without relying on the inbound webhook.</summary>
    Task<CircularoStatus> GetSignatureStatusAsync(string documentId, CancellationToken ct = default);
}

/// <summary>
/// Circularo e-signature client (see https://developers.circularo.com). Mirrors the proven Broker-Portal
/// integration: authentication is a <c>?token=</c> query parameter, and signing is a three-step flow —
/// <list type="number">
///   <item><c>POST /files/saveFile</c> — upload the PDF, receive a content hash.</item>
///   <item><c>POST /documents</c> — create a document from that hash (<c>definitionType:"ext"</c>,
///     <c>workflow:"wf_archive"</c>), receive a document id.</item>
///   <item><c>POST /share</c> — send the document to the signer for signature (Circularo emails them).</item>
/// </list>
/// The signed copy is retrieved via <c>GET /documents/{id}</c> (→ <c>pdfFile.content</c> hash) then
/// <c>GET /files/loadFile/hash/{hash}/</c>.
/// </summary>
public class CircularoService(HttpClient http, IOptions<CircularoOptions> options, ILogger<CircularoService> logger) : ICircularoService
{
    private readonly CircularoOptions _o = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public bool Enabled => _o.Enabled && !string.IsNullOrWhiteSpace(_o.ApiKey);

    private string Url(string relative)
    {
        var url = $"{_o.BaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";
        if (string.IsNullOrWhiteSpace(_o.ApiKey)) return url;
        var sep = url.Contains('?') ? '&' : '?';
        return $"{url}{sep}token={Uri.EscapeDataString(_o.ApiKey)}";
    }

    private static StringContent JsonBody(object payload)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        // Circularo expects a bare "application/json" content type (no charset).
        content.Headers.ContentType!.CharSet = string.Empty;
        return content;
    }

    public async Task<CircularoResult> SendForSignatureAsync(
        byte[] pdf, string fileName, string signerName, string signerEmail, CancellationToken ct = default)
    {
        if (!Enabled) return new(false, null, null, "Circularo is not configured.");
        if (string.IsNullOrWhiteSpace(signerEmail)) return new(false, null, null, "No signer email.");

        try
        {
            // 1) Upload the file → content hash.
            var (hash, saveErr) = await SaveFileAsync(pdf, fileName, ct);
            if (string.IsNullOrWhiteSpace(hash))
                return new(false, null, null, $"Upload failed. {saveErr}");

            // 2) Create the document from the hash → document id.
            var (documentId, createErr) = await CreateDocumentAsync(hash!, Path.GetFileNameWithoutExtension(fileName), ct);
            if (string.IsNullOrWhiteSpace(documentId))
                return new(false, null, null, $"Create document failed. {createErr}");

            // 3) Share the document with the signer for signature (Circularo emails them).
            var page = _o.SignaturePage > 0 ? _o.SignaturePage : Math.Max(1, LocalPdfText.PageCount(pdf));
            var shareErr = await ShareAsync(documentId!, signerName, signerEmail, page, ct);
            if (shareErr is not null)
                return new(false, documentId, null, $"Send for signature failed. {shareErr}");

            logger.LogInformation("Circularo: document {DocId} sent to {Email} for signature.", documentId, signerEmail);
            return new(true, documentId, null, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo send-for-signature error.");
            return new(false, null, null, ex.Message);
        }
    }

    // ── 1. Save file ─────────────────────────────────────────────────────────
    private async Task<(string? hash, string? error)> SaveFileAsync(byte[] pdf, string fileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(fileName), "fileName");

        using var resp = await http.PostAsync(Url("files/saveFile"), form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Circularo SaveFile failed {Status}: {Body}", resp.StatusCode, Trunc(body));
            return (null, $"({(int)resp.StatusCode}) {Trunc(body)}");
        }

        // Response is either { file: { hash, id } } or a raw string hash.
        var hash = ExtractString(body, "hash") ?? ExtractString(body, "id");
        if (string.IsNullOrWhiteSpace(hash))
            hash = body.Trim().Trim('"');
        logger.LogInformation("Circularo SaveFile succeeded, hash {Hash}.", hash);
        return (string.IsNullOrWhiteSpace(hash) ? null : hash, null);
    }

    // ── 2. Create document ───────────────────────────────────────────────────
    private async Task<(string? documentId, string? error)> CreateDocumentAsync(string hash, string title, CancellationToken ct)
    {
        var payload = new
        {
            body = new
            {
                documentType = _o.DocumentType,
                documentTitle = title,
                pdfFile = new { content = hash }
            },
            definitionType = _o.DefinitionType,
            workflow = _o.Workflow,
            optionalData = new { folder = "" }
        };

        using var resp = await http.PostAsync(Url("documents"), JsonBody(payload), ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Circularo CreateDocument failed {Status}: {Body}", resp.StatusCode, Trunc(body));
            return (null, $"({(int)resp.StatusCode}) {Trunc(body)}");
        }

        // Response: { results: [ { documentId } ] }
        var documentId = ExtractString(body, "documentId") ?? ExtractString(body, "_id", "id");
        if (string.IsNullOrWhiteSpace(documentId))
        {
            logger.LogWarning("Circularo CreateDocument: no document id in response: {Body}", Trunc(body));
            return (null, "no document id in response");
        }
        logger.LogInformation("Circularo CreateDocument succeeded, document {DocId}.", documentId);
        return (documentId, null);
    }

    // ── 3. Share ─────────────────────────────────────────────────────────────
    private async Task<string?> ShareAsync(string documentId, string signerName, string signerEmail, int page, CancellationToken ct)
    {
        var payload = new
        {
            objectType = "document",
            type = _o.DocumentType,
            id = documentId,
            sequential = _o.Sequential,
            message = _o.RequestMessage,
            data = new[]
            {
                new
                {
                    sharePurpose = "sign",
                    shareTo = signerEmail,
                    mail = signerEmail,
                    message = _o.RequestMessage,
                    order = 0,
                    sendAttachmentsOnComplete = _o.SendAttachmentsOnComplete
                }
            },
            signatureFields = new[]
            {
                new
                {
                    user = new[] { signerEmail },
                    type = _o.SignatureType,
                    required = true,
                    pages = new[] { page },
                    position = new
                    {
                        percentX = _o.SignPercentX,
                        percentY = _o.SignPercentY,
                        percentWidth = _o.SignPercentWidth,
                        percentHeight = _o.SignPercentHeight
                    },
                    order = 0
                }
            }
        };

        using var resp = await http.PostAsync(Url("share"), JsonBody(payload), ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Circularo Share failed {Status}: {Body}", resp.StatusCode, Trunc(body));
            return $"({(int)resp.StatusCode}) {Trunc(body)}";
        }
        logger.LogInformation("Circularo Share succeeded for document {DocId}.", documentId);
        return null;
    }

    // ── Download signed copy ─────────────────────────────────────────────────
    public async Task<byte[]?> DownloadSignedAsync(string documentId, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(documentId)) return null;
        try
        {
            // Resolve the (signed) file hash from the document, then download the file by hash.
            using var docResp = await http.GetAsync(Url($"documents/{documentId}"), ct);
            var docBody = await docResp.Content.ReadAsStringAsync(ct);
            if (!docResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Circularo GetDocument failed {Status} for {Doc}", docResp.StatusCode, documentId);
                return null;
            }
            var hash = ExtractContentHash(docBody);
            if (string.IsNullOrWhiteSpace(hash))
            {
                logger.LogWarning("Circularo GetDocument: no pdfFile.content for {Doc}", documentId);
                return null;
            }

            using var fileResp = await http.GetAsync(Url($"files/loadFile/hash/{hash}/"), ct);
            if (!fileResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Circularo DownloadFile failed {Status} for hash {Hash}", fileResp.StatusCode, hash);
                return null;
            }
            return await fileResp.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo download signed error for {Doc}", documentId);
            return null;
        }
    }

    // ── Poll signing status ──────────────────────────────────────────────────
    public async Task<CircularoStatus> GetSignatureStatusAsync(string documentId, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(documentId))
            return new(false, false, false, null, null);

        try
        {
            using var docResp = await http.GetAsync(Url($"documents/{documentId}"), ct);
            var body = await docResp.Content.ReadAsStringAsync(ct);
            if (!docResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Circularo GetStatus failed {Status} for {Doc}: {Body}", docResp.StatusCode, documentId, Trunc(body));
                return new(false, false, false, null, null);
            }

            // Circularo doesn't publish a single canonical status field name, so read any of the common ones.
            var raw = ExtractString(body,
                "signStatus", "signatureStatus", "documentStatus", "workflowState", "currentState",
                "state", "status", "phase", "stage");

            var s = raw?.ToLowerInvariant() ?? "";
            var declined = s.Contains("declin") || s.Contains("reject") || s.Contains("cancel") || s.Contains("void");
            var signed = !declined && (s.Contains("sign") || s.Contains("complet") || s.Contains("finish")
                                       || s.Contains("done") || s.Contains("closed") || s.Contains("archiv"));

            byte[]? pdf = null;
            if (signed)
            {
                var hash = ExtractContentHash(body);
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    using var fileResp = await http.GetAsync(Url($"files/loadFile/hash/{hash}/"), ct);
                    if (fileResp.IsSuccessStatusCode)
                        pdf = await fileResp.Content.ReadAsByteArrayAsync(ct);
                }
            }

            return new(true, signed, declined, raw, pdf);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo GetStatus error for {Doc}", documentId);
            return new(false, false, false, null, null);
        }
    }

    // ---- response helpers ----

    /// <summary>Reads <c>results[0].pdfFile.content</c> — the signed file hash.</summary>
    private static string? ExtractContentHash(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0 &&
                results[0].TryGetProperty("pdfFile", out var pdfFile) &&
                pdfFile.TryGetProperty("content", out var content))
                return content.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

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

    private static string Trunc(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 500 ? s[..500] : s);
}
