using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// An additional contact person for a supplier. The supplier's primary contact continues to live in the
/// flat <see cref="Supplier"/> contact columns (FirstName, ContactEmail, Phone…); this collection holds
/// any extra contacts captured via the "Add New Contact" option on the registration/edit form.
/// </summary>
public class SupplierContact
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? PrefixId { get; set; }
    public LookupValue? Prefix { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(200)]
    public string? AdministrativeContact { get; set; }

    [MaxLength(256)]
    public string? ContactEmail { get; set; }

    public int? PhoneCountryCodeId { get; set; }
    public LookupValue? PhoneCountryCode { get; set; }

    [MaxLength(10)]
    public string? PhoneAreaCode { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public int? MobileCountryCodeId { get; set; }
    public LookupValue? MobileCountryCode { get; set; }

    [MaxLength(10)]
    public string? MobileAreaCode { get; set; }

    [MaxLength(50)]
    public string? Mobile { get; set; }

    /// <summary>Preserves the order the contacts were entered in.</summary>
    public int SortOrder { get; set; }
}
