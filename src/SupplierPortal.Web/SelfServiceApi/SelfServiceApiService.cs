using System.ComponentModel.DataAnnotations;
using SupplierPortal.Contracts;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>
/// Composes the internal domain services into the exact operations the DMZ self-service portal needs,
/// returning only DTOs. This is the sole place the API touches supplier data, so all supplier-facing
/// authorization is funneled through a validated session (the caller passes the resolved
/// <see cref="SupplierAccessToken"/>, never a raw id from the request body).
/// </summary>
public class SelfServiceApiService(
    SupplierService supplierService,
    SupplierFieldSettingService fieldSettingService,
    SupplierDocumentService documentService,
    BusinessUnitService businessUnitService,
    SupplierEmailService emailService,
    EmailBrandingService brandingService,
    BrandLogoInliner logoInliner)
{
    // Categories the self-service wizard renders (everything except the internal SupplierStatus list).
    private static readonly LookupCategory[] FormCategories =
    [
        LookupCategory.TaxOrganizationType, LookupCategory.SupplierType, LookupCategory.Country,
        LookupCategory.Province, LookupCategory.VatCode, LookupCategory.PaymentMethod,
        LookupCategory.PaymentTerms, LookupCategory.Prefix, LookupCategory.Currency,
        LookupCategory.BusinessRelationship, LookupCategory.CommunicationMethod,
        LookupCategory.LicensingAuthority, LookupCategory.VatRegistrationType,
        LookupCategory.BusinessPartnerType, LookupCategory.BusinessLocationType,
        LookupCategory.EInvoicingWave
    ];

    // Email fields and their labels (server-side format validation — the DMZ boundary never trusts the client).
    private static readonly (Func<SupplierDto, string?> Get, string Label)[] EmailFields =
    [
        (d => d.Email, "Business E-Mail"),
        (d => d.ContactEmail, "Contact Email"),
        (d => d.RemitAdviceEmail, "Remit Advice Email"),
        (d => d.CommunicationEmail, "Communication Email"),
        (d => d.RemittanceEmail, "Remittance E-mail")
    ];

    private static readonly EmailAddressAttribute EmailValidator = new();

    public async Task<SupplierFormBootstrapDto?> BuildBootstrapAsync(int supplierId)
    {
        var supplier = await supplierService.GetByIdAsync(supplierId);
        if (supplier is null)
        {
            return null;
        }

        var lookups = new Dictionary<string, List<LookupItemDto>>();
        foreach (var category in FormCategories)
        {
            var values = await supplierService.GetLookupsAsync(category);
            lookups[category.ToString()] = values.Select(DtoMapper.ToDto).ToList();
        }

        var banks = await supplierService.GetBanksAsync();
        var fieldSettings = await fieldSettingService.GetAllAsync();
        var tabs = await fieldSettingService.GetTabsAsync();
        var documents = await documentService.GetDocumentsAsync(supplierId);
        var pending = await supplierService.GetPendingSupplierEditedFieldsAsync(supplierId);
        var businessUnits = await businessUnitService.GetAllAsync();
        var businessUnit = businessUnits.FirstOrDefault(b => b.Id == supplier.BusinessUnitId);
        var businessUnitDto = businessUnit is null ? null : DtoMapper.ToDto(businessUnit);
        if (businessUnitDto is not null)
        {
            // Same logo source as the OTP response (fetched + inlined so it renders on the form page even
            // when the storage account isn't publicly reachable from the supplier's browser).
            businessUnitDto.LogoUrl = await logoInliner.InlineIfUrlAsync(
                await brandingService.GetBusinessUnitLogoSrcAsync(supplier.BusinessUnitId));
        }

        return new SupplierFormBootstrapDto
        {
            Supplier = DtoMapper.ToDto(supplier),
            BusinessUnit = businessUnit is null ? null : DtoMapper.ToDto(businessUnit),
            BusinessUnits = businessUnits.Select(DtoMapper.ToDto).ToList(),
            Lookups = lookups,
            Banks = banks.Select(DtoMapper.ToDto).ToList(),
            FieldSettings = fieldSettings.Select(DtoMapper.ToDto).ToList(),
            Tabs = tabs.Select(DtoMapper.ToDto).ToList(),
            Documents = documents.Select(DtoMapper.ToDto).ToList(),
            PendingEditedFieldKeys = pending.ToList()
        };
    }

    /// <summary>
    /// Persists a supplier edit that originated from the self-service portal. The write is always scoped
    /// to the supplier bound to the validated session token, and routed through the domain's
    /// change-tracking (accessTokenId) so it is recorded as supplier-originated and flips the record to
    /// "For Review". Malformed emails are rejected server-side regardless of any client validation.
    /// </summary>
    public async Task<SaveSupplierResponse> SaveAsync(SupplierAccessToken session, SupplierDto dto, string actor)
    {
        var errors = ValidateEmails(dto);
        if (errors.Count > 0)
        {
            return new SaveSupplierResponse { Success = false, Errors = errors };
        }

        var supplier = await supplierService.GetByIdAsync(session.SupplierId);
        if (supplier is null)
        {
            return new SaveSupplierResponse { Success = false, Errors = ["Supplier not found."] };
        }

        DtoMapper.ApplyToSupplier(dto, supplier);
        await supplierService.UpdateAsync(supplier, actor, session.Id);

        var updated = await supplierService.GetByIdAsync(session.SupplierId);
        var pending = await supplierService.GetPendingSupplierEditedFieldsAsync(session.SupplierId);
        return new SaveSupplierResponse
        {
            Success = true,
            Supplier = updated is null ? null : DtoMapper.ToDto(updated),
            PendingEditedFieldKeys = pending.ToList()
        };
    }

    public async Task<SubmitResponse> SubmitAsync(SupplierAccessToken session, string baseUrl, SubmitRequest declaration, string actor)
    {
        if (string.IsNullOrWhiteSpace(declaration.SignatureName))
        {
            return new SubmitResponse { Success = false, Error = "Please type your full name as your signature." };
        }
        if (string.IsNullOrWhiteSpace(declaration.SignatureImageBase64))
        {
            return new SubmitResponse { Success = false, Error = "Please draw your signature before submitting." };
        }

        var supplier = await supplierService.GetByIdAsync(session.SupplierId);
        if (supplier is null)
        {
            return new SubmitResponse { Success = false, Error = "Supplier not found." };
        }

        byte[]? signatureImage;
        try { signatureImage = Convert.FromBase64String(declaration.SignatureImageBase64); }
        catch (FormatException) { return new SubmitResponse { Success = false, Error = "The signature image was invalid." }; }

        // Record the signed declaration on the supplier, then complete the submission the same way the
        // internal supplier form does (markComplete = true → status Completed + signed-declaration PDF).
        supplier.DisclaimerAcknowledged = true;
        supplier.DisclaimerAcknowledgedAt = DateTime.UtcNow;
        supplier.DisclaimerAcknowledgedBy = actor;
        supplier.DisclaimerText = declaration.DeclarationText;
        supplier.DisclaimerSignature = declaration.SignatureName.Trim();
        supplier.DisclaimerSignatureImage = signatureImage;

        await supplierService.UpdateAsync(supplier, actor, session.Id, markComplete: true);

        await emailService.NotifyRequestorOfSubmissionAsync(supplier, session.CreatedBy, baseUrl);
        return new SubmitResponse { Success = true };
    }

    private static List<string> ValidateEmails(SupplierDto dto)
    {
        var errors = new List<string>();
        foreach (var (get, label) in EmailFields)
        {
            var value = get(dto);
            if (!string.IsNullOrWhiteSpace(value) && !EmailValidator.IsValid(value))
            {
                errors.Add($"{label} is not a valid email address.");
            }
        }
        return errors;
    }
}
