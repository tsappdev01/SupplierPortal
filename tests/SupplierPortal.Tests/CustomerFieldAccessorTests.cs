using SupplierPortal.Web.Models.Customer;
using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

public class CustomerFieldAccessorTests
{
    [Fact]
    public void Sets_and_gets_string_field()
    {
        var c = new Customer();
        CustomerFieldAccessor.SetString(c, nameof(Customer.LegalName), "AP & T Business Solutions");
        Assert.Equal("AP & T Business Solutions", CustomerFieldAccessor.GetString(c, nameof(Customer.LegalName)));
    }

    [Fact]
    public void Parses_date_field_to_yyyy_MM_dd()
    {
        var c = new CustomerContact();
        CustomerFieldAccessor.SetString(c, nameof(CustomerContact.PassportExpiryDate), "2028-07-23");
        Assert.Equal("2028-07-23", CustomerFieldAccessor.GetString(c, nameof(CustomerContact.PassportExpiryDate)));
        Assert.Equal("23 Jul 2028", CustomerFieldAccessor.GetDisplay(c, nameof(CustomerContact.PassportExpiryDate)));
    }

    [Theory]
    [InlineData("true", "Yes")]
    [InlineData("yes", "Yes")]
    [InlineData("false", "No")]
    public void Parses_yesno_field(string raw, string expectedDisplay)
    {
        var c = new CustomerContact();
        CustomerFieldAccessor.SetString(c, nameof(CustomerContact.PEP), raw);
        Assert.Equal(expectedDisplay, CustomerFieldAccessor.GetDisplay(c, nameof(CustomerContact.PEP)));
    }

    [Fact]
    public void Blank_clears_a_nullable_field()
    {
        var c = new Customer();
        CustomerFieldAccessor.SetString(c, nameof(Customer.LegalName), "x");
        CustomerFieldAccessor.SetString(c, nameof(Customer.LegalName), "   ");
        Assert.Null(CustomerFieldAccessor.GetString(c, nameof(Customer.LegalName)));
    }

    [Fact]
    public void Unknown_field_is_ignored_safely()
    {
        var c = new Customer();
        CustomerFieldAccessor.SetString(c, "NoSuchField", "value");   // must not throw
        Assert.Null(CustomerFieldAccessor.GetString(c, "NoSuchField"));
    }
}
