using System.Net.Http.Json;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>DMZ implementation of <see cref="ISupplierSelfServiceDataGateway"/>: calls the internal
/// Self-Service API over HTTPS (the "SelfServiceApi" typed HttpClient). No database access.</summary>
public class HttpSupplierSelfServiceDataGateway(IHttpClientFactory httpFactory)
    : ISupplierSelfServiceDataGateway
{
    private HttpClient Client => httpFactory.CreateClient("SelfServiceApi");

    public async Task<SsFormData?> LoadFormAsync(string token, string session)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/form", new SsSessionRequest(token, session));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SsFormData>() : null;
    }

    public async Task<SsSaveResult> SaveAsync(string token, string session, Supplier updated, bool complete)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/submit", new SsSubmitRequest(token, session, updated, complete));
        return await resp.Content.ReadFromJsonAsync<SsSaveResult>()
               ?? new SsSaveResult(false, "The service is temporarily unavailable. Please try again.", null);
    }

    public async Task<List<SupplierDocument>> ListDocumentsAsync(string token, string session)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/documents/list", new SsSessionRequest(token, session));
        return resp.IsSuccessStatusCode
            ? (await resp.Content.ReadFromJsonAsync<List<SupplierDocument>>() ?? [])
            : [];
    }

    public async Task<SsUploadResult> UploadDocumentAsync(
        string token, string session, int documentType, string fileName, string contentType, byte[] bytes)
    {
        var req = new SsUploadRequest(token, session, documentType, fileName, contentType, Convert.ToBase64String(bytes));
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/documents/upload", req);
        return await resp.Content.ReadFromJsonAsync<SsUploadResult>()
               ?? new SsUploadResult(false, "Upload failed. Please try again.", null, null);
    }

    public async Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/documents/file", new SsDocumentRequest(token, session, documentId));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SsFileContent>() : null;
    }

    public async Task<SsResult> DeleteDocumentAsync(string token, string session, int documentId)
    {
        var resp = await Client.PostAsJsonAsync("/api/ss/supplier/documents/delete", new SsDocumentRequest(token, session, documentId));
        return await resp.Content.ReadFromJsonAsync<SsResult>() ?? new SsResult(false, "Delete failed.");
    }
}
