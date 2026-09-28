using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SupplierPortal.Contracts;

namespace SupplierPortal.SelfService.Services;

/// <summary>The portal's only door to customer data (mirrors <see cref="SupplierApiClient"/>). Every call
/// carries the portal client-credentials token (Authorization: Bearer) and, for data operations, the
/// customer's link + session tokens (X-Customer-Link / X-Customer-Session). Holds no customer data and no
/// database access — it just marshals DTOs to and from the internal API's /api/selfservice/customer/* routes.</summary>
public class CustomerApiClient(
    IHttpClientFactory httpClientFactory, PortalTokenProvider tokenProvider, ILogger<CustomerApiClient> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private static string Trim(string? s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    private async Task<HttpClient> AuthedClientAsync(string? linkToken = null, string? sessionToken = null)
    {
        var http = httpClientFactory.CreateClient("Api");
        var token = await tokenProvider.GetTokenAsync();
        if (token is not null)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (linkToken is not null) { http.DefaultRequestHeaders.Add("X-Customer-Link", linkToken); }
        if (sessionToken is not null) { http.DefaultRequestHeaders.Add("X-Customer-Session", sessionToken); }
        return http;
    }

    public async Task<OtpRequestResponse> RequestOtpAsync(string linkToken, bool resend = false)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/customer/otp/request",
                new OtpRequestRequest { LinkToken = linkToken, Resend = resend });
            if (!res.IsSuccessStatusCode)
            {
                return new OtpRequestResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
            }
            return await res.Content.ReadFromJsonAsync<OtpRequestResponse>()
                   ?? new OtpRequestResponse { Success = false, Error = "Unexpected response from the service." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer RequestOtp failed");
            return new OtpRequestResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
        }
    }

    public async Task<LinkInfoResponse?> GetLinkInfoAsync(string linkToken)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.GetAsync($"/api/selfservice/customer/link-info?token={Uri.EscapeDataString(linkToken)}");
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<LinkInfoResponse>() : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer GetLinkInfo failed");
            return null;
        }
    }

    public async Task<CustomerOtpVerifyResponse> VerifyOtpAsync(string linkToken, string code)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/customer/otp/verify",
                new OtpVerifyRequest { LinkToken = linkToken, Code = code });
            if (!res.IsSuccessStatusCode)
            {
                return new CustomerOtpVerifyResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
            }
            return await res.Content.ReadFromJsonAsync<CustomerOtpVerifyResponse>()
                   ?? new CustomerOtpVerifyResponse { Success = false, Error = "Unexpected response from the service." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer VerifyOtp failed");
            return new CustomerOtpVerifyResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
        }
    }

    public async Task<CustomerFormBootstrapDto?> GetBootstrapAsync(string linkToken, string sessionToken)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync("/api/selfservice/customer/form");
            return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<CustomerFormBootstrapDto>() : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer GetBootstrap failed");
            return null;
        }
    }

    public async Task<SaveCustomerResponse> SaveAsync(string linkToken, string sessionToken, SaveCustomerRequest req)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.PutAsJsonAsync("/api/selfservice/customer/form", req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Customer SaveAsync failed: HTTP {Status}. Body: {Body}", (int)res.StatusCode, Trim(body, 2000));
                return new SaveCustomerResponse { Success = false, Errors = [$"Could not save (HTTP {(int)res.StatusCode})."] };
            }
            return JsonSerializer.Deserialize<SaveCustomerResponse>(body, JsonOpts)
                   ?? new SaveCustomerResponse { Success = false, Errors = ["Unexpected response."] };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer SaveAsync threw");
            return new SaveCustomerResponse { Success = false, Errors = [$"Could not save: {ex.Message}"] };
        }
    }

    public async Task<SubmitCustomerResponse> SubmitAsync(string linkToken, string sessionToken, SubmitCustomerRequest req)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.PostAsJsonAsync("/api/selfservice/customer/submit", req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                logger.LogError("Customer SubmitAsync failed: HTTP {Status}. Body: {Body}", (int)res.StatusCode, Trim(body, 2000));
                return new SubmitCustomerResponse { Success = false, Error = $"Could not submit (HTTP {(int)res.StatusCode})." };
            }
            return JsonSerializer.Deserialize<SubmitCustomerResponse>(body, JsonOpts)
                   ?? new SubmitCustomerResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer SubmitAsync failed");
            return new SubmitCustomerResponse { Success = false, Error = "Could not submit. Please check your connection and try again." };
        }
    }

    public async Task<List<CustomerDocumentDto>> ListDocumentsAsync(string linkToken, string sessionToken)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync("/api/selfservice/customer/documents");
            if (!res.IsSuccessStatusCode) { return []; }
            var wrap = await res.Content.ReadFromJsonAsync<CustomerDocumentUploadResponse>();
            return wrap?.Documents ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer ListDocuments failed");
            return [];
        }
    }

    public async Task<CustomerDocumentUploadResponse> UploadDocumentAsync(
        string linkToken, string sessionToken, string documentType, Stream content, string fileName, string contentType)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(content);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
            form.Add(fileContent, "file", fileName);
            form.Add(new StringContent(documentType), "documentType");

            var res = await http.PostAsync("/api/selfservice/customer/documents", form);
            if (!res.IsSuccessStatusCode)
            {
                return new CustomerDocumentUploadResponse { Success = false, Error = "Upload failed. Please try again." };
            }
            return await res.Content.ReadFromJsonAsync<CustomerDocumentUploadResponse>()
                   ?? new CustomerDocumentUploadResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer UploadDocument failed");
            return new CustomerDocumentUploadResponse { Success = false, Error = "Upload failed. Please try again." };
        }
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetDocumentAsync(
        string linkToken, string sessionToken, int documentId)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.GetAsync($"/api/selfservice/customer/documents/{documentId}");
            if (!res.IsSuccessStatusCode) { return null; }
            var bytes = await res.Content.ReadAsByteArrayAsync();
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = res.Content.Headers.ContentDisposition?.FileNameStar
                ?? res.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "document";
            return (bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer GetDocument failed");
            return null;
        }
    }

    public async Task<CustomerDocumentUploadResponse> DeleteDocumentAsync(string linkToken, string sessionToken, int documentId)
    {
        try
        {
            var http = await AuthedClientAsync(linkToken, sessionToken);
            var res = await http.DeleteAsync($"/api/selfservice/customer/documents/{documentId}");
            if (!res.IsSuccessStatusCode)
            {
                return new CustomerDocumentUploadResponse { Success = false, Error = "Could not remove the document." };
            }
            return await res.Content.ReadFromJsonAsync<CustomerDocumentUploadResponse>()
                   ?? new CustomerDocumentUploadResponse { Success = false, Error = "Unexpected response." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Customer DeleteDocument failed");
            return new CustomerDocumentUploadResponse { Success = false, Error = "Could not remove the document." };
        }
    }
}
