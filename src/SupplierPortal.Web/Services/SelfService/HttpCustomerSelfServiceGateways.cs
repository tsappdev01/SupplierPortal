using System.Net.Http.Json;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>DMZ customer self-service access flow: calls the internal Self-Service API. No database access.</summary>
public class HttpCustomerSelfServiceAccessGateway(IHttpClientFactory httpFactory) : ICustomerSelfServiceAccessGateway
{
    private HttpClient Client => httpFactory.CreateClient("SelfServiceApi");

    public async Task<SsCustomerLinkInfo> ResolveAsync(string token)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/customer/resolve", new SsTokenRequest(token));
            if (!resp.IsSuccessStatusCode)
            {
                return new SsCustomerLinkInfo(false, null, null, null, false);
            }
            return await resp.Content.ReadFromJsonAsync<SsCustomerLinkInfo>()
                   ?? new SsCustomerLinkInfo(false, null, null, null, false);
        }
        catch
        {
            return new SsCustomerLinkInfo(false, null, null, null, false);
        }
    }

    public async Task<(bool Success, string? Error)> SendOtpAsync(string token)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/customer/otp/send", new SsTokenRequest(token));
            var result = await resp.Content.ReadFromJsonAsync<SsResult>();
            return result is null ? (false, "The service is unavailable. Please try again.") : (result.Success, result.Error);
        }
        catch
        {
            return (false, "The service is temporarily unavailable. Please try again.");
        }
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code)
    {
        try
        {
            var resp = await Client.PostAsJsonAsync("/api/ss/customer/otp/verify", new SsOtpVerifyRequest(token, code));
            var result = await resp.Content.ReadFromJsonAsync<SsVerifyResult>();
            return result is null
                ? (false, "The service is unavailable. Please try again.", null)
                : (result.Success, result.Error, result.SessionToken);
        }
        catch
        {
            return (false, "The service is temporarily unavailable. Please try again.", null);
        }
    }
}

/// <summary>DMZ customer self-service data flow: calls the internal Self-Service API. No database access.</summary>
public class HttpCustomerSelfServiceDataGateway(IHttpClientFactory httpFactory) : ICustomerSelfServiceDataGateway
{
    private HttpClient Client => httpFactory.CreateClient("SelfServiceApi");

    public async Task<SsCustomerFormData?> LoadFormAsync(string token, string session)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/form", new SsSessionRequest(token, session));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SsCustomerFormData>() : null;
    }

    public async Task<SsCustomerSaveResult> SaveAsync(string token, string session, Customer updated, bool complete)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/submit", new SsCustomerSubmitRequest(token, session, updated, complete));
        return await resp.Content.ReadFromJsonAsync<SsCustomerSaveResult>()
               ?? new SsCustomerSaveResult(false, "The service is temporarily unavailable. Please try again.", null);
    }

    public async Task<List<CustomerDocument>> ListDocumentsAsync(string token, string session)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/documents/list", new SsSessionRequest(token, session));
        return resp.IsSuccessStatusCode ? (await resp.Content.ReadFromJsonAsync<List<CustomerDocument>>() ?? []) : [];
    }

    public async Task<SsResult> UploadDocumentAsync(
        string token, string session, string documentType, string fileName, string contentType, byte[] bytes)
    {
        var req = new SsCustomerUploadRequest(token, session, documentType, fileName, contentType, Convert.ToBase64String(bytes));
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/documents/upload", req);
        return await resp.Content.ReadFromJsonAsync<SsResult>() ?? new SsResult(false, "Upload failed. Please try again.");
    }

    public async Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/documents/file", new SsDocumentRequest(token, session, documentId));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SsFileContent>() : null;
    }

    public async Task<SsResult> DeleteDocumentAsync(string token, string session, int documentId)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/customer/documents/delete", new SsDocumentRequest(token, session, documentId));
        return await resp.Content.ReadFromJsonAsync<SsResult>() ?? new SsResult(false, "Delete failed.");
    }
}
