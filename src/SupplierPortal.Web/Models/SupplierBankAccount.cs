using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// An additional bank account for a supplier. The supplier's primary bank account continues to live in
/// the flat <see cref="Supplier"/> banking columns (BankId, AccountNumber, Iban…); this collection holds
/// any extra accounts captured via the "Add New Bank Account" option on the Payment step.
/// </summary>
public class SupplierBankAccount
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int? BankId { get; set; }
    public Bank? Bank { get; set; }

    public int? BankBranchId { get; set; }
    public BankBranch? BankBranch { get; set; }

    public int? AccountCountryCodeId { get; set; }
    public LookupValue? AccountCountryCode { get; set; }

    [MaxLength(200)]
    public string? AccountName { get; set; }

    [MaxLength(50)]
    public string? AccountNumber { get; set; }

    public int? AccountCurrencyCodeId { get; set; }
    public LookupValue? AccountCurrencyCode { get; set; }

    [MaxLength(50)]
    public string? SwiftCode { get; set; }

    [MaxLength(50)]
    public string? Iban { get; set; }

    public DateTime? AccountStartDate { get; set; }

    /// <summary>Date the bank account is closed/ended. Visible to administrators only.</summary>
    public DateTime? AccountEndDate { get; set; }

    /// <summary>Preserves the order the accounts were entered in.</summary>
    public int SortOrder { get; set; }
}
