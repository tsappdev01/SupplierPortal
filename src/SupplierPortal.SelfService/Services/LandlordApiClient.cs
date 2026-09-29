using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SupplierPortal.Contracts;

namespace SupplierPortal.SelfService.Services;

/// <summary>The DMZ app's door to landlord self-service data (mirrors <see cref="CustomerApiClient"/>). Carries
/// the portal client-credentials token plus the landlord link + session tokens (X-Landlord-Link /
/// X-Landlord-Session). Holds no data and no DB access — it marshals DTOs to/from /api/selfservice/landlord/*.</summary>
public class LandlordApiClient(
    IHttpClientFactory httpClientFactory, PortalTokenProvider tokenProvider, ILogger<LandlordApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static string Trim(string? s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    private async Task<HttpClient> AuthedClientAsync(string? linkToken = null, string? sessionToken = null)
    {
        var http = httpClientFactory.CreateClient("Api");
        var token = await tokenProvider.GetTokenAsync();
        if (token is not null) http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (linkToken is not null) http.DefaultRequestHeaders.Add("X-Landlord-Link", linkToken);
        if (sessionToken is not null) http.DefaultRequestHeaders.Add("X-Landlord-Session", sessionToken);
        return http;
    }

    public async Task<OtpRequestResponse> RequestOtpAsync(string linkToken, bool resend = false)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/landlord/otp/request",
                new OtpRequestRequest { LinkToken = linkToken, Resend = resend });
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Landlord RequestOtp failed: HTTP {Status}. Body: {Body}", (int)res.StatusCode, Trim(await res.Content.ReadAsStringAsync(), 1000));
                return new OtpRequestResponse { Success = false, Error = $"The service is temporarily unavailable (HTTP {(int)res.StatusCode})." };
            }
            return await res.Content.ReadFromJsonAsync<OtpRequestResponse>() ?? new OtpRequestResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord RequestOtp failed"); return new OtpRequestResponse { Success = false, Error = "The service is temporarily unavailable." }; }
    }

    public async Task<LinkInfoResponse?> GetLinkInfoAsync(string linkToken)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.GetAsync($"/api/selfservice/landlord/link-info?token={Uri.EscapeDataString(linkToken)}");
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<LinkInfoResponse>() : null;
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord GetLinkInfo failed"); return null; }
    }

    public async Task<LandlordOtpVerifyResponse> VerifyOtpAsync(string linkToken, string code)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/landlord/otp/verify",
                new OtpVerifyRequest { LinkToken = linkToken, Code = code });
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Landlord VerifyOtp failed: HTTP {Status}", (int)res.StatusCode);
                return new LandlordOtpVerifyResponse { Success = false, Error = $"The service is temporarily unavailable (HTTP {(int)res.StatusCode})." };
            }
            return await res.Content.ReadFromJsonAsync<LandlordOtpVerifyResponse>() ?? new LandlordOtpVerifyResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord VerifyOtp failed"); return new LandlordOtpVerifyResponse { Success = false, Error = "The service is temporarily unavailable." }; }
    }

    public async Task<LandlordFormBootstrapDto?> GetBootstrapAsync(string linkToken, string sessionToken)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync("/api/selfservice/landlord/form");
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<LandlordFormBootstrapDto>() : null;
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord GetBootstrap failed"); return null; }
    }

    public async Task<SaveLandlordResponse> SaveAsync(string linkToken, string sessionToken, SaveLandlordRequest req)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.PostAsJsonAsync("/api/selfservice/landlord/form/save", req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Landlord SaveAsync failed: HTTP {Status}. Body: {Body}", (int)res.StatusCode, Trim(body, 2000));
                return new SaveLandlordResponse { Success = false, Errors = [$"Could not save (HTTP {(int)res.StatusCode})."] };
            }
            return JsonSerializer.Deserialize<SaveLandlordResponse>(body, JsonOpts) ?? new SaveLandlordResponse { Success = false, Errors = ["Unexpected response."] };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord SaveAsync threw"); return new SaveLandlordResponse { Success = false, Errors = [$"Could not save: {ex.Message}"] }; }
    }

    public async Task<SubmitLandlordResponse> SubmitAsync(string linkToken, string sessionToken, SubmitLandlordRequest req)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.PostAsJsonAsync("/api/selfservice/landlord/submit", req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Landlord SubmitAsync failed: HTTP {Status}. Body: {Body}", (int)res.StatusCode, Trim(body, 2000));
                return new SubmitLandlordResponse { Success = false, Error = $"Could not submit (HTTP {(int)res.StatusCode})." };
            }
            return JsonSerializer.Deserialize<SubmitLandlordResponse>(body, JsonOpts) ?? new SubmitLandlordResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord SubmitAsync failed"); return new SubmitLandlordResponse { Success = false, Error = "Could not submit. Please try again." }; }
    }

    public async Task<List<LandlordDocumentDto>> ListDocumentsAsync(string linkToken, string sessionToken)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync("/api/selfservice/landlord/documents");
            if (!res.IsSuccessStatusCode) return [];
            return (await res.Content.ReadFromJsonAsync<LandlordDocumentUploadResponse>())?.Documents ?? [];
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord ListDocuments failed"); return []; }
    }

    public async Task<LandlordDocumentUploadResponse> UploadDocumentAsync(
        string linkToken, string sessionToken, string documentType, Stream content, string fileName, string contentType)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", fileName);
            form.Add(new StringContent(documentType), "documentType");
            var res = await http.PostAsync("/api/selfservice/landlord/documents", form);
            if (!res.IsSuccessStatusCode) return new LandlordDocumentUploadResponse { Success = false, Error = "Upload failed. Please try again." };
            return await res.Content.ReadFromJsonAsync<LandlordDocumentUploadResponse>() ?? new LandlordDocumentUploadResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord UploadDocument failed"); return new LandlordDocumentUploadResponse { Success = false, Error = "Upload failed. Please try again." }; }
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetDocumentAsync(string linkToken, string sessionToken, int documentId)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync($"/api/selfservice/landlord/documents/{documentId}");
            if (!res.IsSuccessStatusCode) return null;
            var bytes = await res.Content.ReadAsByteArrayAsync();
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = res.Content.Headers.ContentDisposition?.FileNameStar ?? res.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "document";
            return (bytes, contentType, fileName);
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord GetDocument failed"); return null; }
    }

    public async Task<LandlordDocumentUploadResponse> DeleteDocumentAsync(string linkToken, string sessionToken, int documentId)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.PostAsync($"/api/selfservice/landlord/documents/{documentId}/delete", null);
            if (!res.IsSuccessStatusCode) return new LandlordDocumentUploadResponse { Success = false, Error = "Could not remove the document." };
            return await res.Content.ReadFromJsonAsync<LandlordDocumentUploadResponse>() ?? new LandlordDocumentUploadResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex) { logger.LogError(ex, "Landlord DeleteDocument failed"); return new LandlordDocumentUploadResponse { Success = false, Error = "Could not remove the document." }; }
    }
}
