using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Customer self-service access flow (link resolve + OTP). Local = database; DMZ (Http) = internal API.</summary>
public interface ICustomerSelfServiceAccessGateway
{
    Task<SsCustomerLinkInfo> ResolveAsync(string token);
    Task<(bool Success, string? Error)> SendOtpAsync(string token);
    Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code);
}

/// <summary>Customer self-service data flow (form load / save / documents). Local = database; DMZ = API.</summary>
public interface ICustomerSelfServiceDataGateway
{
    Task<SsCustomerFormData?> LoadFormAsync(string token, string session);
    Task<SsCustomerSaveResult> SaveAsync(string token, string session, Customer updated, bool complete);
    Task<List<CustomerDocument>> ListDocumentsAsync(string token, string session);
    Task<SsResult> UploadDocumentAsync(string token, string session, string documentType, string fileName, string contentType, byte[] bytes);
    Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId);
    Task<SsResult> DeleteDocumentAsync(string token, string session, int documentId);
}
