using System.Linq;
using SupplierPortal.Web.Models.Customer;
using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

/// <summary>Regression tests for the customer workflow status set, LOVs and the reflection field accessor
/// that backs the dynamic customer form (internal + self-service).</summary>
public class CustomerWorkflowTests
{
    [Fact]
    public void Active_statuses_are_the_expected_workflow()
    {
        Assert.Equal(
            new[]
            {
                CustomerStatuses.NotSubmitted, CustomerStatuses.AwaitingCustomerResponse,
                CustomerStatuses.ForReview, CustomerStatuses.Complete
            },
            CustomerStatuses.All);
    }

    [Fact]
    public void Returned_status_is_retired()
    {
        Assert.DoesNotContain(CustomerStatuses.Returned, CustomerStatuses.All);
    }

    [Fact]
    public void Nationality2_uses_the_same_country_list_as_Nationality()
    {
        var nationality = CustomerLookups.For("Contact", "Nationality");
        var nationality2 = CustomerLookups.For("Contact", "Nationality2");
        Assert.NotEmpty(nationality2);
        Assert.Equal(nationality, nationality2);
        Assert.Contains("United Arab Emirates", nationality2);
    }

    [Fact]
    public void Phone_countries_are_uae_first()
    {
        Assert.NotEmpty(CustomerLookups.PhoneCountries);
        Assert.Equal("971", CustomerLookups.PhoneCountries[0].Dial);
    }

    [Fact]
    public void Unknown_lookup_returns_empty_so_the_form_falls_back_to_text()
    {
        Assert.Empty(CustomerLookups.For("Account", "NoSuchField"));
    }

    [Fact]
    public void Setting_blank_clears_the_field()
    {
        var c = new Customer();
        CustomerFieldAccessor.SetString(c, nameof(Customer.LegalName), "Tajeer PJSC");
        CustomerFieldAccessor.SetString(c, nameof(Customer.LegalName), "");
        Assert.Null(CustomerFieldAccessor.GetString(c, nameof(Customer.LegalName)));
    }

    [Fact]
    public void Account_and_contact_dictionaries_round_trip_through_the_accessor()
    {
        // Mirrors how the DMZ form applies FieldKey->value dictionaries onto the entities.
        var customer = new Customer();
        var contact = new CustomerContact();
        var account = new System.Collections.Generic.Dictionary<string, string?>
        {
            [nameof(Customer.LegalName)] = "Tajeer PJSC",
            [nameof(Customer.TRNNumber)] = "123456789012345",
        };
        var contactValues = new System.Collections.Generic.Dictionary<string, string?>
        {
            [nameof(CustomerContact.FirstName)] = "Nayyar",
            [nameof(CustomerContact.LastName)] = "Khan",
        };

        foreach (var (k, v) in account) { CustomerFieldAccessor.SetString(customer, k, v); }
        foreach (var (k, v) in contactValues) { CustomerFieldAccessor.SetString(contact, k, v); }

        Assert.Equal("Tajeer PJSC", customer.LegalName);
        Assert.Equal("123456789012345", customer.TRNNumber);
        Assert.Equal("Nayyar", contact.FirstName);
        Assert.Equal("Khan", contact.LastName);
    }

    [Fact]
    public void Vat_group_grid_lovs_are_the_expected_values()
    {
        Assert.Equal(new[] { "Wave 1", "Wave 2" }, CustomerLookups.EInvoicingWaves);
        Assert.Equal(new[] { "Mainland", "Free Zone" }, CustomerLookups.BusinessLocationTypes);
    }

    [Fact]
    public void Vat_group_entities_default_to_an_empty_collection()
    {
        // The grid binds directly to Customer.VatGroupEntities; it must never be null on a fresh record.
        Assert.NotNull(new Customer().VatGroupEntities);
        Assert.Empty(new Customer().VatGroupEntities);
    }
}
