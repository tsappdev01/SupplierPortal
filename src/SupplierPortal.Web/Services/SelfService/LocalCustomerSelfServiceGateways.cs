using Microsoft.Extensions.Configuration;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Internal-zone customer self-service access flow: talks to the database directly.</summary>
public class LocalCustomerSelfServiceAccessGateway(
    CustomerAccessTokenService accessTokenService, EmailBrandingService branding)
    : ICustomerSelfServiceAccessGateway
{
    public async Task<SsCustomerLinkInfo> ResolveAsync(string token)
    {
        // Resolve even if expired so the "link not available" page can show the business unit's logo + e-mail.
        var t = await accessTokenService.GetTokenForDisplayAsync(token);
        if (t is null)
        {
            return new SsCustomerLinkInfo(false, null, null, null, false);
        }
        var buId = t.Customer?.BusinessUnitId;
        var logo = await branding.GetBusinessUnitUploadedLogoSrcAsync(buId);
        var email = await branding.GetBusinessUnitEmailAsync(buId);
        var valid = t.ExpiresAt >= DateTime.UtcNow;
        var needsFresh = valid && (t.OtpGeneratedAt is null || t.OtpExpiresAt is null || t.OtpExpiresAt < DateTime.UtcNow);
        return new SsCustomerLinkInfo(valid, t.Customer?.LegalName, buId, logo, needsFresh, email);
    }

    public async Task<(bool Success, string? Error)> SendOtpAsync(string token)
    {
        var t = await accessTokenService.GetValidTokenAsync(token);
        return t is null
            ? (false, "This link is invalid or has expired.")
            : await accessTokenService.GenerateAndSendOtpAsync(t.Id);
    }

    public async Task<(bool Success, string? Error, string? SessionToken)> VerifyOtpAsync(string token, string code)
    {
        var t = await accessTokenService.GetValidTokenAsync(token);
        return t is null
            ? (false, "This link is invalid or has expired.", null)
            : await accessTokenService.VerifyOtpAsync(t.Id, code);
    }
}

/// <summary>Internal-zone customer self-service data flow: talks to the database and services directly.</summary>
public class LocalCustomerSelfServiceDataGateway(
    CustomerAccessTokenService accessTokenService,
    CustomerService customerService,
    CustomerFieldConfigService fieldConfigService,
    CustomerDocumentService documentService,
    EmailBrandingService branding,
    CustomerEmailService emailService,
    IConfiguration configuration)
    : ICustomerSelfServiceDataGateway
{
    private const string Actor = "Customer Self-Service";

    private async Task<CustomerAccessToken?> ResolveAsync(string token, string session)
        => await accessTokenService.ValidateSessionAsync(token, session);

    public async Task<SsCustomerFormData?> LoadFormAsync(string token, string session)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return null;
        }
        var customer = await customerService.GetAsync(access.CustomerId);
        if (customer is null)
        {
            return null;
        }
        return new SsCustomerFormData
        {
            Customer = customer,
            LogoSrc = await branding.GetBusinessUnitUploadedLogoSrcAsync(customer.BusinessUnitId),
            Config = await fieldConfigService.GetAllAsync(),
            Documents = await documentService.GetDocumentsAsync(access.CustomerId),
            DocTypes = await documentService.GetTypesAsync(),
        };
    }

    public async Task<SsCustomerSaveResult> SaveAsync(string token, string session, Customer updated, bool complete)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsCustomerSaveResult(false, "Your session has expired. Please use the link from your email again.", null);
        }
        if (updated.Id != access.CustomerId)
        {
            return new SsCustomerSaveResult(false, "This record does not match your session.", null);
        }

        await customerService.SaveAsync(updated, Actor);

        if (complete)
        {
            await customerService.SetStatusAsync(updated.Id, CustomerStatuses.ForReview, Actor);
            await accessTokenService.MarkUsedAsync(access.Id);
            var internalBase = configuration["SelfService:InternalUrl"] ?? "";
            try { await emailService.NotifyRequestorOfSubmissionAsync(updated, access.CreatedBy, internalBase); } catch { }
        }

        var saved = await customerService.GetAsync(access.CustomerId);
        return new SsCustomerSaveResult(true, null, saved);
    }

    public async Task<List<CustomerDocument>> ListDocumentsAsync(string token, string session)
    {
        var access = await ResolveAsync(token, session);
        return access is null ? [] : await documentService.GetDocumentsAsync(access.CustomerId);
    }

    public async Task<SsResult> UploadDocumentAsync(
        string token, string session, string documentType, string fileName, string contentType, byte[] bytes)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsResult(false, "Your session has expired.");
        }
        var (success, error) = await documentService.UploadBytesAsync(
            access.CustomerId, documentType, bytes, fileName, contentType, Actor);
        return new SsResult(success, error);
    }

    public async Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return null;
        }
        var docs = await documentService.GetDocumentsAsync(access.CustomerId);
        if (docs.All(d => d.Id != documentId))
        {
            return null;
        }
        var file = await documentService.GetFileAsync(documentId);
        return file is null ? null : new SsFileContent(Convert.ToBase64String(file.Value.Bytes), file.Value.ContentType, file.Value.FileName);
    }

    public async Task<SsResult> DeleteDocumentAsync(string token, string session, int documentId)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsResult(false, "Your session has expired.");
        }
        var docs = await documentService.GetDocumentsAsync(access.CustomerId);
        if (docs.All(d => d.Id != documentId))
        {
            return new SsResult(false, "Document not found.");
        }
        await documentService.DeleteAsync(documentId, Actor);
        return new SsResult(true, null);
    }
}
