namespace SupplierPortal.Web.Models;

public enum UserType
{
    Administrator = 1,
    NormalUser = 2
}

public enum LookupCategory
{
    TaxOrganizationType = 1,
    SupplierType = 2,
    Country = 3,
    Province = 5,
    VatCode = 6,
    PaymentMethod = 7,
    PaymentTerms = 8,
    Prefix = 9,
    Currency = 10,
    BusinessRelationship = 11,
    CommunicationMethod = 12
}
