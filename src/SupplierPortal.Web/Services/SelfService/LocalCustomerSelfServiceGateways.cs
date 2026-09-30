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
        var email = await branding.GetPortalSenderEmailAsync(buId, Models.PortalType.Customer);
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
    CustomerDeclarationPdfService declarationPdf,
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
            Tabs = (await fieldConfigService.GetTabsAsync())
                .Select(t => new SsCustomerTab(t.Section, t.GroupName, t.Title, t.Sequence, t.IsVisible))
                .ToList(),
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

        var persisted = await customerService.SaveAsync(updated, Actor);
        if (!persisted)
        {
            return new SsCustomerSaveResult(false, "We couldn't save your information (record not found). Please use the link from your email again.", null);
        }

        if (complete)
        {
            // Render the signed declaration into a PDF and store it with the customer's documents (mirrors
            // the supplier). Best-effort: a PDF failure must not fail the submission.
            if (updated.DisclaimerAcknowledged && updated.DisclaimerSignatureImage is { Length: > 0 })
            {
                try
                {
                    var pdf = declarationPdf.Build(updated, updated.DisclaimerSignatureImage);
                    var name = updated.LegalName ?? updated.CustomerCode ?? updated.CustomerId ?? updated.Id.ToString();
                    await documentService.UploadBytesAsync(updated.Id, "Signed Declaration", pdf,
                        $"Signed Declaration - {name}.pdf", "application/pdf", Actor);
                }
                catch { /* declaration PDF is best-effort */ }
            }

            // A customer submission goes to "For Review": the initiator is notified to review the details, then
            // an internal user completes the record (status → Completed) after reviewing.
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

    /// <summary>The reserved, system-generated document type — a self-service customer must never be able to
    /// upload (and thereby forge / overwrite) it.</summary>
    private const string SignedDeclarationType = "Signed Declaration";

    public async Task<SsResult> UploadDocumentAsync(
        string token, string session, string documentType, string fileName, string contentType, byte[] bytes)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsResult(false, "Your session has expired.");
        }

        // Only allow uploads against an active, customer-facing KYC document type. This blocks a crafted
        // request from forging or overwriting the server-generated "Signed Declaration" (single-slot replace
        // by type would otherwise let a customer replace the genuine signed PDF).
        if (string.Equals(documentType, SignedDeclarationType, StringComparison.OrdinalIgnoreCase))
        {
            return new SsResult(false, "That document type cannot be uploaded.");
        }
        var activeTypes = await documentService.GetTypesAsync();
        if (!activeTypes.Any(t => string.Equals(t.Name, documentType, StringComparison.OrdinalIgnoreCase)))
        {
            return new SsResult(false, "Invalid document type.");
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
