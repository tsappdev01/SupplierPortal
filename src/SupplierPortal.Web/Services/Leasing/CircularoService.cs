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

    /// <summary>Attempts to download the signed copy of a document. Returns null when unavailable.</summary>
    Task<byte[]?> DownloadSignedAsync(string documentId, CancellationToken ct = default);
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

    private bool UseQueryToken =>
        _o.AuthMode.Equals("Query", StringComparison.OrdinalIgnoreCase) ||
        _o.AuthMode.Equals("Both", StringComparison.OrdinalIgnoreCase);

    private bool UseHeaderToken =>
        _o.AuthMode.Equals("Header", StringComparison.OrdinalIgnoreCase) ||
        _o.AuthMode.Equals("Both", StringComparison.OrdinalIgnoreCase);

    private string Url(string relative)
    {
        var url = $"{_o.BaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";
        if (UseQueryToken && !string.IsNullOrWhiteSpace(_o.ApiKey))
        {
            var sep = url.Contains('?') ? '&' : '?';
            var param = string.IsNullOrWhiteSpace(_o.TokenQueryParam) ? "token" : _o.TokenQueryParam;
            url = $"{url}{sep}{param}={Uri.EscapeDataString(_o.ApiKey)}";
        }
        return url;
    }

    private IEnumerable<string> UploadPaths() =>
        (_o.UploadPathCandidates is { Length: > 0 } c
            ? c
            : new[] { _o.UploadPath, "document/upload", "document/create", "documents", "document", "documents/upload", "share/upload", "file/upload", "file", "upload" })
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>A way of encoding the file in the upload request body. Circularo versions differ on whether
    /// they want multipart (and under which field) or a JSON envelope with base64 content.</summary>
    private sealed record UploadStrategy(string Name, Func<byte[], string, string, HttpContent> Build);

    private static IEnumerable<UploadStrategy> UploadStrategies()
    {
        // multipart/form-data with the configured field name (default "file")
        yield return new UploadStrategy("multipart:file", (pdf, name, field) =>
        {
            var form = new MultipartFormDataContent();
            var fc = new ByteArrayContent(pdf);
            fc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fc, string.IsNullOrWhiteSpace(field) ? "file" : field, name);
            form.Add(new StringContent(name), "name");
            return form;
        });
        // multipart/form-data under the alternate "files" field
        yield return new UploadStrategy("multipart:files", (pdf, name, _) =>
        {
            var form = new MultipartFormDataContent();
            var fc = new ByteArrayContent(pdf);
            fc.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fc, "files", name);
            form.Add(new StringContent(name), "name");
            return form;
        });
    }

    private void Authorize(HttpRequestMessage req)
    {
        if (!UseHeaderToken || string.IsNullOrWhiteSpace(_o.ApiKey)) return;
        req.Headers.TryAddWithoutValidation("Authorization",
            string.IsNullOrWhiteSpace(_o.AuthScheme) ? _o.ApiKey : $"{_o.AuthScheme} {_o.ApiKey}");
    }

    public async Task<CircularoResult> SendForSignatureAsync(
        byte[] pdf, string fileName, string signerName, string signerEmail, CancellationToken ct = default)
    {
        if (!Enabled) return new(false, null, null, "Circularo is not configured.");
        if (string.IsNullOrWhiteSpace(signerEmail)) return new(false, null, null, "No signer email.");

        try
        {
            // Build the signer / signing workflow. Circularo's POST documents creates the document together
            // with its workflow (it validates that both "definitionType" and "workflow" are present), so the
            // file, the document type and the workflow all go in one create call. The exact field shapes vary
            // by Circularo version and are refined by the server's own validation messages, which are surfaced
            // to the UI verbatim.
            var signer = new Dictionary<string, object?>
            {
                ["email"] = signerEmail, ["name"] = signerName, ["role"] = "signer", ["action"] = "sign", ["order"] = 1
            };
            if (_o.RequireUaePassSignature) signer["signatureProvider"] = _o.SignatureProvider;
            if (_o.RequireUaePassVerification)
                signer["authentication"] = new Dictionary<string, object?>
                {
                    ["method"] = _o.VerificationMethod, ["factor"] = _o.VerificationFactor, ["email"] = signerEmail
                };
            var workflow = new[] { signer };

            var title = Path.GetFileNameWithoutExtension(fileName);
            var b64 = Convert.ToBase64String(pdf);
            var createBody = new Dictionary<string, object?>
            {
                ["definitionType"] = _o.DefinitionType,
                ["title"] = title,
                ["name"] = fileName,
                ["workflow"] = workflow,
                ["subject"] = _o.RequestSubject,
                ["message"] = _o.RequestMessage,
                // the file carried under a few likely keys (extras are ignored server-side)
                ["fileName"] = fileName,
                ["contentType"] = "application/pdf",
                ["data"] = b64,
                ["file"] = b64,
                ["content"] = b64
            };
            if (_o.RequireUaePassSignature) createBody["signatureProvider"] = _o.SignatureProvider;

            // The create-document JSON body (carries the workflow) is tried first; multipart uploads are kept
            // as fallbacks for versions that want a plain upload before a separate workflow call.
            var strategies = new List<UploadStrategy>
            {
                new("create:json", (_, _, _) =>
                    new StringContent(JsonSerializer.Serialize(createBody), Encoding.UTF8, "application/json"))
            };
            strategies.AddRange(UploadStrategies());

            // Probe each candidate path with each strategy until one returns 2xx. A 404 means the route doesn't
            // exist (skip other strategies for that path); a 400/415/422 means the route exists but the body was
            // wrong — keep that response body, it's Circularo's own validation message.
            string? documentId = null;
            string? okBody = null;
            string? winningStrategy = null;
            var tried = new List<string>();
            string? bestHint = null; // most-informative (non-404) server response, surfaced to the UI

            foreach (var path in UploadPaths())
            {
                var routeMissing = false;
                foreach (var strategy in strategies)
                {
                    using var upReq = new HttpRequestMessage(HttpMethod.Post, Url(path))
                    {
                        Content = strategy.Build(pdf, fileName, _o.UploadFileField)
                    };
                    Authorize(upReq);
                    using var upResp = await http.SendAsync(upReq, ct);
                    var upBody = await upResp.Content.ReadAsStringAsync(ct);
                    tried.Add($"{path}[{strategy.Name}]→{(int)upResp.StatusCode}");

                    if (upResp.IsSuccessStatusCode)
                    {
                        documentId = ExtractId(upBody);
                        okBody = upBody;
                        winningStrategy = strategy.Name;
                        logger.LogInformation("Circularo create succeeded via '{Path}' [{Strategy}], document {DocId}.",
                            path, strategy.Name, documentId);
                        break;
                    }

                    logger.LogWarning("Circularo create via '{Path}' [{Strategy}] failed {Status}: {Body}",
                        path, strategy.Name, upResp.StatusCode, Trunc(upBody));

                    if (upResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        routeMissing = true; // route doesn't exist — no point trying other body shapes here
                        break;
                    }
                    // Route exists but body was rejected: remember the server's explanation for the UI.
                    if (!string.IsNullOrWhiteSpace(upBody))
                        bestHint = $"{path}[{strategy.Name}] {(int)upResp.StatusCode}: {Trunc(upBody)}";
                }
                if (documentId is not null) break;
                if (routeMissing) continue;
            }
            if (string.IsNullOrWhiteSpace(documentId))
            {
                var detail = bestHint is not null
                    ? $"Create failed — {bestHint}"
                    : $"Create failed. Tried: {string.Join(", ", tried)}";
                return new(false, null, null, detail);
            }

            // If the create call carried the workflow (create:json), the document is already routed for
            // signature — Circularo emails the signer. Otherwise (a plain multipart upload won), create the
            // signature workflow in a separate call.
            if (winningStrategy != "create:json" && !string.IsNullOrWhiteSpace(_o.SignInvitePath))
            {
                var field = new Dictionary<string, object?>
                {
                    ["type"] = "signature", ["recipientEmail"] = signerEmail, ["page"] = -1, ["anchor"] = "Sign here", ["required"] = true
                };
                if (_o.RequireUaePassSignature) field["provider"] = _o.SignatureProvider;

                var payload = new Dictionary<string, object?>
                {
                    ["subject"] = _o.RequestSubject,
                    ["message"] = _o.RequestMessage,
                    ["workflow"] = workflow,
                    ["signers"] = workflow,
                    ["recipients"] = workflow,
                    ["fields"] = new[] { field }
                };
                if (_o.RequireUaePassSignature) payload["signatureProvider"] = _o.SignatureProvider;
                using var signReq = new HttpRequestMessage(HttpMethod.Post, Url(_o.SignInvitePath.Replace("{id}", documentId)))
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                Authorize(signReq);
                using var signResp = await http.SendAsync(signReq, ct);
                var signBody = await signResp.Content.ReadAsStringAsync(ct);
                if (!signResp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Circularo send-for-signature failed {Status}: {Body}", signResp.StatusCode, Trunc(signBody));
                    return new(false, documentId, null, $"Send for signature failed ({(int)signResp.StatusCode}): {Trunc(signBody)}");
                }
                okBody = signBody;
            }

            var signingUrl = ExtractString(okBody ?? "", "signingUrl", "signUrl", "url", "link");
            logger.LogInformation("Circularo: document {DocId} sent to {Email} for signature.", documentId, signerEmail);
            return new(true, documentId, signingUrl, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo send-for-signature error.");
            return new(false, null, null, ex.Message);
        }
    }

    public async Task<byte[]?> DownloadSignedAsync(string documentId, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(documentId)) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Url(_o.DownloadSignedPath.Replace("{id}", documentId)));
            Authorize(req);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Circularo download signed failed {Status} for {Doc}", resp.StatusCode, documentId);
                return null;
            }
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Circularo download signed error for {Doc}", documentId);
            return null;
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
