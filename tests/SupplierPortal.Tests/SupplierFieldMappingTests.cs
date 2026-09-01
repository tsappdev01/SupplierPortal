using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using Xunit;

namespace SupplierPortal.Tests;

public class SupplierFieldMappingTests
{
    // The registration wizard enforces "mandatory" by reflecting on SupplierFormModel using the
    // field-settings FieldKey. A settings key with no matching form property would silently never be
    // validated, so every seeded key must resolve to a real, readable SupplierFormModel property.
    [Fact]
    public void Every_field_setting_key_maps_to_a_form_model_property()
    {
        var missing = new List<string>();
        foreach (var (fieldKey, _, _, _) in SupplierFieldSettingSeedData.Fields)
        {
            if (typeof(SupplierFormModel).GetProperty(fieldKey) is null)
            {
                missing.Add(fieldKey);
            }
        }

        Assert.True(missing.Count == 0, "SupplierFormModel is missing properties for: " + string.Join(", ", missing));
    }

    [Fact]
    public void Field_setting_keys_are_unique()
    {
        var keys = SupplierFieldSettingSeedData.Fields.Select(f => f.FieldKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void ApplyTo_and_FromSupplier_round_trip_the_extended_fields()
    {
        var model = new SupplierFormModel
        {
            SupplierName = "Acme Trading LLC",
            TaxRegistrationNumber = "100123456700003",
            TradeLicenseNumber = "TL-99",
            ContactEmail = "ap@acme.example",
            Email = "info@acme.example",
            FirstName = "Sara",
            MiddleName = "A.",
            LastName = "Khan",
            CountryNameId = 42,
            VatRegistered = true,
            VatRegistrationTypeId = 7,
            VatGroupEntities = new()
            {
                new VatGroupEntityModel { BusinessLegalName = "Entity One", Address = "Addr 1", EInvoicingWaveId = 1, TrnNumber = "TRN1", TinNumber = "TIN1", BusinessLocationTypeId = 2 },
                new VatGroupEntityModel { BusinessLegalName = "Entity Two", Address = "Addr 2", EInvoicingWaveId = 2, TrnNumber = "TRN2", TinNumber = "TIN2", BusinessLocationTypeId = 1 }
            },
            SupplierTin = "TIN-555",
            BusinessPartnerTypeId = 1,
            BusinessLocationTypeId = 2,
            EInvoicingWaveId = 1,
            TradeLicenseIssueDate = new DateTime(2024, 1, 15),
            TradeLicenseExpiryDate = new DateTime(2026, 1, 14),
            TradeLicenseIssuingBodyId = 9,
            IssuingBodyIfOthers = "Some Free Zone",
            IntermediaryAccountApplicable = true,
            IntermediaryCountryId = 100,
            IntermediaryBankName = "Correspondent Bank",
            IntermediaryAccountNumber = "111222",
            IntermediaryBranchNumber = "BR-1",
            IntermediaryIban = "AE000000000000000000000",
            IntermediaryBicSwiftCode = "CORPUS33"
        };

        var supplier = new Supplier();
        model.ApplyTo(supplier);
        var round = SupplierFormModel.FromSupplier(supplier);

        Assert.Equal("A.", round.MiddleName);
        Assert.Equal("Khan", round.LastName);
        Assert.Equal(42, round.CountryNameId);
        Assert.Equal(true, round.VatRegistered);
        Assert.Equal(7, round.VatRegistrationTypeId);
        // The legacy summary fields are now derived from the structured VAT group entities.
        Assert.Equal(2, round.NumberOfVatGroupEntities);
        Assert.Equal("Entity One\nEntity Two", round.VatGroupEntityNames);
        Assert.Equal(2, round.VatGroupEntities.Count);
        Assert.Equal("Entity One", round.VatGroupEntities[0].BusinessLegalName);
        Assert.Equal("Addr 2", round.VatGroupEntities[1].Address);
        Assert.Equal(2, round.VatGroupEntities[0].BusinessLocationTypeId);
        Assert.Equal("TIN-555", round.SupplierTin);
        Assert.Equal(1, round.BusinessPartnerTypeId);
        Assert.Equal(2, round.BusinessLocationTypeId);
        Assert.Equal(1, round.EInvoicingWaveId);
        Assert.Equal(new DateTime(2024, 1, 15), round.TradeLicenseIssueDate);
        Assert.Equal(new DateTime(2026, 1, 14), round.TradeLicenseExpiryDate);
        Assert.Equal(9, round.TradeLicenseIssuingBodyId);
        Assert.Equal("Some Free Zone", round.IssuingBodyIfOthers);
        Assert.True(round.IntermediaryAccountApplicable);
        Assert.Equal(100, round.IntermediaryCountryId);
        Assert.Equal("Correspondent Bank", round.IntermediaryBankName);
        Assert.Equal("111222", round.IntermediaryAccountNumber);
        Assert.Equal("BR-1", round.IntermediaryBranchNumber);
        Assert.Equal("AE000000000000000000000", round.IntermediaryIban);
        Assert.Equal("CORPUS33", round.IntermediaryBicSwiftCode);
    }
}
