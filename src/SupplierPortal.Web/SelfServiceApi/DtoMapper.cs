using System.Reflection;
using SupplierPortal.Contracts;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.SelfServiceApi;

/// <summary>
/// Maps between the dependency-free contract DTOs and the internal EF/view models. The supplier payload
/// is bridged through <see cref="SupplierFormModel"/> so the API reuses the domain's field mapping and
/// defaulting rules (e.g. AdministrativeContact ← FirstName) rather than re-implementing them.
/// </summary>
public static class DtoMapper
{
    // SupplierDto and SupplierFormModel mirror the same wizard field set, so matching properties are
    // copied by name — avoiding a brittle 60-line hand-map in each direction.
    private static readonly (PropertyInfo Dto, PropertyInfo Form)[] SupplierMap = BuildSupplierMap();

    private static (PropertyInfo, PropertyInfo)[] BuildSupplierMap()
    {
        var formProps = typeof(SupplierFormModel).GetProperties().ToDictionary(p => p.Name);
        var pairs = new List<(PropertyInfo, PropertyInfo)>();
        foreach (var dtoProp in typeof(SupplierDto).GetProperties())
        {
            if (formProps.TryGetValue(dtoProp.Name, out var formProp)
                && dtoProp is { CanRead: true, CanWrite: true }
                && formProp is { CanRead: true, CanWrite: true }
                && dtoProp.PropertyType == formProp.PropertyType)
            {
                pairs.Add((dtoProp, formProp));
            }
        }
        return pairs.ToArray();
    }

    public static SupplierDto ToDto(Supplier supplier)
    {
        var form = SupplierFormModel.FromSupplier(supplier);
        var dto = new SupplierDto { Id = supplier.Id };
        foreach (var (dtoProp, formProp) in SupplierMap)
        {
            dtoProp.SetValue(dto, formProp.GetValue(form));
        }
        return dto;
    }

    /// <summary>Applies an inbound DTO onto an existing tracked supplier, without touching identity/BU/status.</summary>
    public static void ApplyToSupplier(SupplierDto dto, Supplier supplier)
    {
        var form = new SupplierFormModel();
        foreach (var (dtoProp, formProp) in SupplierMap)
        {
            formProp.SetValue(form, dtoProp.GetValue(dto));
        }
        form.ApplyTo(supplier);
    }

    public static LookupItemDto ToDto(LookupValue l) =>
        new() { Id = l.Id, DisplayName = l.DisplayName, Code = l.Code, PhoneCode = l.PhoneCode };

    public static BankDto ToDto(Bank b) => new()
    {
        Id = b.Id,
        Name = b.Name,
        Branches = b.Branches.Select(x => new BankBranchDto { Id = x.Id, Name = x.Name }).ToList()
    };

    public static BusinessUnitDto ToDto(BusinessUnit b) => new() { Id = b.Id, Name = b.Name, Code = b.Code };

    public static TabSettingDto ToDto(SupplierTabSetting t) => new()
    {
        Step = t.Step,
        Title = t.Title,
        IsVisible = t.IsVisible
    };

    public static FieldSettingDto ToDto(SupplierFieldSetting f) => new()
    {
        FieldKey = f.FieldKey,
        DisplayLabel = f.DisplayLabel,
        Step = f.Step,
        Sequence = f.Sequence,
        IsMandatory = f.IsMandatory,
        IsVisibleToSupplier = f.IsVisibleToSupplier
    };

    public static SupplierDocumentDto ToDto(SupplierDocument d) => new()
    {
        Id = d.Id,
        DocumentType = (SelfServiceDocumentType)(int)d.DocumentType,
        FileName = d.FileName,
        ContentType = d.ContentType,
        UploadedAt = d.UploadedAt,
        FileSizeBytes = d.FileSizeBytes
    };
}
