using Microsoft.Extensions.Configuration;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services.SelfService;

/// <summary>Internal-zone implementation of <see cref="ISupplierSelfServiceDataGateway"/>: resolves the
/// session against the database and drives the form load / submit / document operations directly through
/// the domain services. Used when the instance is deployed Internal (and behind the Self-Service API).</summary>
public class LocalSupplierSelfServiceDataGateway(
    SupplierAccessTokenService accessTokenService,
    SupplierService supplierService,
    SupplierFieldSettingService fieldSettingService,
    SupplierDocumentService documentService,
    BusinessUnitService businessUnitService,
    EmailBrandingService branding,
    SupplierEmailService emailService,
    IConfiguration configuration)
    : ISupplierSelfServiceDataGateway
{
    private const string Actor = "Supplier Self-Service";

    /// <summary>Validates the session and returns the supplier id it is scoped to, or null. The specific
    /// rejection reason is written to the server log by the token service (not shown to the supplier).</summary>
    private async Task<SupplierAccessToken?> ResolveAsync(string token, string session)
        => await accessTokenService.ValidateSessionAsync(token, session);

    public async Task<SsFormData?> LoadFormAsync(string token, string session)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return null;
        }

        var supplier = await supplierService.GetByIdAsync(access.SupplierId);
        if (supplier is null)
        {
            return null;
        }

        var fieldSettings = await fieldSettingService.GetAllAsync();
        var data = new SsFormData
        {
            Supplier = supplier,
            LogoSrc = await branding.GetBusinessUnitUploadedLogoSrcAsync(supplier.BusinessUnitId),
            TaxOrgTypes = await supplierService.GetLookupsAsync(LookupCategory.TaxOrganizationType),
            SupplierTypes = await supplierService.GetLookupsAsync(LookupCategory.SupplierType),
            Countries = await supplierService.GetLookupsAsync(LookupCategory.Country),
            BusinessRelationships = await supplierService.GetLookupsAsync(LookupCategory.BusinessRelationship),
            Provinces = await supplierService.GetLookupsAsync(LookupCategory.Province),
            VatCodes = await supplierService.GetLookupsAsync(LookupCategory.VatCode),
            PaymentMethods = await supplierService.GetLookupsAsync(LookupCategory.PaymentMethod),
            PaymentTerms = await supplierService.GetLookupsAsync(LookupCategory.PaymentTerms),
            Prefixes = await supplierService.GetLookupsAsync(LookupCategory.Prefix),
            CommunicationMethods = await supplierService.GetLookupsAsync(LookupCategory.CommunicationMethod),
            Currencies = await supplierService.GetLookupsAsync(LookupCategory.Currency),
            VatRegistrationTypes = await supplierService.GetLookupsAsync(LookupCategory.VatRegistrationType),
            BusinessPartnerTypes = await supplierService.GetLookupsAsync(LookupCategory.BusinessPartnerType),
            BusinessLocationTypes = await supplierService.GetLookupsAsync(LookupCategory.BusinessLocationType),
            EInvoicingWaves = await supplierService.GetLookupsAsync(LookupCategory.EInvoicingWave),
            LicensingAuthorities = await supplierService.GetLookupsAsync(LookupCategory.LicensingAuthority),
            Banks = await supplierService.GetBanksAsync(),
            BusinessUnits = await businessUnitService.GetAllAsync(),
            FieldSettings = fieldSettings,
            TabSettings = await fieldSettingService.GetTabsAsync(),
            Documents = await documentService.GetDocumentsAsync(access.SupplierId),
            ChangedFieldKeys = (await supplierService.GetPendingSupplierEditedFieldsAsync(access.SupplierId)).ToList(),
        };
        return data;
    }

    public async Task<SsSaveResult> SaveAsync(string token, string session, Supplier updated, bool complete)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsSaveResult(false, "Your session has expired. Please use the link from your email again.", null);
        }
        // Security: the record being saved must be the one this session is scoped to.
        if (updated.Id != access.SupplierId)
        {
            return new SsSaveResult(false, "This record does not match your session.", null);
        }

        await supplierService.UpdateAsync(updated, Actor, access.Id, markComplete: complete);

        if (complete)
        {
            await accessTokenService.MarkUsedAsync(access.Id);

            // Best-effort notifications (must not fail the submission). The requestor link points at the
            // internal portal (SelfService:InternalUrl), not the public DMZ host.
            var internalBase = configuration["SelfService:InternalUrl"] ?? "";
            try { await emailService.NotifyRequestorOfSubmissionAsync(updated, access.CreatedBy, internalBase); } catch { }
            try
            {
                await emailService.NotifySupplierCompletedAsync(
                    updated.Id, updated.SupplierName, updated.ContactEmail, Actor, updated.BusinessUnitId);
            }
            catch { }
        }

        // Return the reloaded record so the form can sync database-generated child (contact/account) ids.
        var saved = await supplierService.GetByIdAsync(access.SupplierId);
        return new SsSaveResult(true, null, saved);
    }

    public async Task<List<SupplierDocument>> ListDocumentsAsync(string token, string session)
    {
        var access = await ResolveAsync(token, session);
        return access is null ? [] : await documentService.GetDocumentsAsync(access.SupplierId);
    }

    public async Task<SsUploadResult> UploadDocumentAsync(
        string token, string session, int documentType, string fileName, string contentType, byte[] bytes)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return new SsUploadResult(false, "Your session has expired.", null, null);
        }

        // Suppliers may only upload their own supporting documents. The Signed Declaration is
        // generated server-side from the drawn signature and must never be supplied by the client,
        // otherwise a crafted upload could overwrite the genuine declaration (non-Other types are
        // single-slot / replace).
        var type = (SupplierDocumentType)documentType;
        if (type is not (SupplierDocumentType.TradeLicense or SupplierDocumentType.VatCertification
            or SupplierDocumentType.BankConfirmationLetter or SupplierDocumentType.Other))
        {
            return new SsUploadResult(false, "That document type cannot be uploaded.", null, null);
        }

        await using var stream = new MemoryStream(bytes);
        var result = await documentService.UploadFromStreamAsync(
            access.SupplierId, type, stream, fileName, contentType, bytes.Length, Actor);
        return new SsUploadResult(result.Success, result.Error, result.ExtractedNumber, result.DunsNumber);
    }

    public async Task<SsFileContent?> GetDocumentFileAsync(string token, string session, int documentId)
    {
        var access = await ResolveAsync(token, session);
        if (access is null)
        {
            return null;
        }
        // Only documents attached to this session's supplier may be read.
        var docs = await documentService.GetDocumentsAsync(access.SupplierId);
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
        var docs = await documentService.GetDocumentsAsync(access.SupplierId);
        if (docs.All(d => d.Id != documentId))
        {
            return new SsResult(false, "Document not found.");
        }
        var (success, error) = await documentService.DeleteAsync(documentId, Actor);
        return new SsResult(success, error);
    }
}
