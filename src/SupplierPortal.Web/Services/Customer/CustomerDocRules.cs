using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Evaluates a customer document type's optional conditional rule (Depends On a customer field) against
/// the current customer / contact values. A document with no rule always applies; one with a rule applies only
/// when the controlling field's value matches. "Required now" additionally requires the type to be marked
/// Required. Shared by the internal admin form and the in-portal self-service form.</summary>
public static class CustomerDocRules
{
    /// <summary>True when the document should be shown on the checklist for this customer — i.e. it has no
    /// conditional rule, or the controlling field currently holds the triggering value.</summary>
    public static bool Applies(CustomerDocumentType type, Customer customer, CustomerContact? contact,
        IReadOnlyList<CustomerFieldConfig> config)
    {
        if (string.IsNullOrWhiteSpace(type.ControllingFieldKey)) return true;

        // The controlling field may live in either section, so resolve its section from the field config.
        var cfg = config.FirstOrDefault(f => f.FieldKey == type.ControllingFieldKey);
        object entity = cfg?.Section == "Contact" ? (object?)contact ?? customer : customer;
        var current = CustomerFieldAccessor.GetString(entity, type.ControllingFieldKey);
        return string.Equals(current, type.ControllingValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the document is mandatory right now — it is marked Required and its conditional rule
    /// (if any) is satisfied.</summary>
    public static bool RequiredNow(CustomerDocumentType type, Customer customer, CustomerContact? contact,
        IReadOnlyList<CustomerFieldConfig> config)
        => type.IsRequired && Applies(type, customer, contact, config);
}
