using System.Net.Http.Headers;
using System.Net.Http.Json;
using SupplierPortal.Contracts;

namespace SupplierPortal.SelfService.Services;

/// <summary>
/// The portal's only door to supplier data. Every call carries the portal client-credentials token
/// (Authorization: Bearer) and, for data operations, the supplier session token (X-Supplier-Session).
/// This class holds no supplier data of its own and no database access — it just marshals DTOs to and
/// from the internal API.
/// </summary>
public class SupplierApiClient(
    IHttpClientFactory httpClientFactory, PortalTokenProvider tokenProvider, ILogger<SupplierApiClient> logger)
{
    private async Task<HttpClient> AuthedClientAsync(string? sessionToken = null)
    {
        var http = httpClientFactory.CreateClient("Api");
        var token = await tokenProvider.GetTokenAsync();
        if (token is not null)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (sessionToken is not null)
        {
            http.DefaultRequestHeaders.Add("X-Supplier-Session", sessionToken);
        }
        return http;
    }

    public async Task<OtpRequestResponse> RequestOtpAsync(string linkToken)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/otp/request", new OtpRequestRequest { LinkToken = linkToken });
            if (!res.IsSuccessStatusCode)
            {
                return new OtpRequestResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
            }
            return await res.Content.ReadFromJsonAsync<OtpRequestResponse>() ?? Fail();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RequestOtp failed");
            return new OtpRequestResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
        }

        static OtpRequestResponse Fail() => new() { Success = false, Error = "Unexpected response from the service." };
    }

    public async Task<OtpVerifyResponse> VerifyOtpAsync(string linkToken, string code)
    {
        try
        {
            var http = await AuthedClientAsync();
            var res = await http.PostAsJsonAsync("/api/selfservice/otp/verify",
                new OtpVerifyRequest { LinkToken = linkToken, Code = code });
            if (!res.IsSuccessStatusCode)
            {
                return new OtpVerifyResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
            }
            return await res.Content.ReadFromJsonAsync<OtpVerifyResponse>()
                   ?? new OtpVerifyResponse { Success = false, Error = "Unexpected response from the service." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "VerifyOtp failed");
            return new OtpVerifyResponse { Success = false, Error = "The service is temporarily unavailable. Please try again." };
        }
    }

    public async Task<SupplierFormBootstrapDto?> GetBootstrapAsync(string sessionToken)
    {
        var http = await AuthedClientAsync(sessionToken);
        var res = await http.GetAsync("/api/selfservice/supplier");
        return res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<SupplierFormBootstrapDto>() : null;
    }

    public async Task<SaveSupplierResponse> SaveAsync(string sessionToken, SupplierDto supplier)
    {
        var http = await AuthedClientAsync(sessionToken);
        var res = await http.PutAsJsonAsync("/api/selfservice/supplier", supplier);
        if (!res.IsSuccessStatusCode)
        {
            return new SaveSupplierResponse { Success = false, Errors = ["Could not save. Please try again."] };
        }
        return await res.Content.ReadFromJsonAsync<SaveSupplierResponse>()
               ?? new SaveSupplierResponse { Success = false, Errors = ["Unexpected response."] };
    }

    public async Task<DocumentUploadResponse> UploadDocumentAsync(
        string sessionToken, SelfServiceDocumentType type, Stream content, string fileName, string contentType)
    {
        var http = await AuthedClientAsync(sessionToken);
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(((int)type).ToString()), "documentType");

        var res = await http.PostAsync("/api/selfservice/documents", form);
        if (!res.IsSuccessStatusCode)
        {
            return new DocumentUploadResponse { Success = false, Error = "Upload failed. Please try again." };
        }
        return await res.Content.ReadFromJsonAsync<DocumentUploadResponse>()
               ?? new DocumentUploadResponse { Success = false, Error = "Unexpected response." };
    }

    /// <summary>Fetches a document's bytes for in-form preview. The internal API enforces that the
    /// document belongs to this session's supplier. Returns null on any failure.</summary>
    public async Task<(byte[] Bytes, string ContentType, string FileName)?> GetDocumentAsync(string sessionToken, int documentId)
    {
        try
        {
            var http = await AuthedClientAsync(sessionToken);
            var res = await http.GetAsync($"/api/selfservice/documents/{documentId}");
            if (!res.IsSuccessStatusCode)
            {
                return null;
            }
            var bytes = await res.Content.ReadAsByteArrayAsync();
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = res.Content.Headers.ContentDisposition?.FileNameStar
                ?? res.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "document";
            return (bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetDocument failed");
            return null;
        }
    }

    public async Task<DocumentUploadResponse> DeleteDocumentAsync(string sessionToken, int documentId)
    {
        var http = await AuthedClientAsync(sessionToken);
        var res = await http.DeleteAsync($"/api/selfservice/documents/{documentId}");
        if (!res.IsSuccessStatusCode)
        {
            return new DocumentUploadResponse { Success = false, Error = "Could not remove the document." };
        }
        return await res.Content.ReadFromJsonAsync<DocumentUploadResponse>()
               ?? new DocumentUploadResponse { Success = false, Error = "Unexpected response." };
    }

    public async Task<SubmitResponse> SubmitAsync(string sessionToken)
    {
        var http = await AuthedClientAsync(sessionToken);
        var res = await http.PostAsync("/api/selfservice/submit", null);
        if (!res.IsSuccessStatusCode)
        {
            return new SubmitResponse { Success = false, Error = "Could not submit. Please try again." };
        }
        return await res.Content.ReadFromJsonAsync<SubmitResponse>()
               ?? new SubmitResponse { Success = false, Error = "Unexpected response." };
    }
}
