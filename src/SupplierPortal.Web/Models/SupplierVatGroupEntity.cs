using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// One legal entity within a supplier's VAT group. Captured on the Business / Organization tab when
/// the supplier's VAT Registration Type is "VAT Group" — the supplier lists every entity covered by
/// the group's single TRN. Replaces the earlier free-text <see cref="Supplier.VatGroupEntityNames"/>.
/// </summary>
public class SupplierVatGroupEntity
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [MaxLength(300)]
    public string? BusinessLegalName { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    /// <summary>E-Invoicing Wave applicability (LookupCategory.EInvoicingWave — Wave 1 / Wave 2).</summary>
    public int? EInvoicingWaveId { get; set; }

    [MaxLength(100)]
    public string? TrnNumber { get; set; }

    [MaxLength(100)]
    public string? TinNumber { get; set; }

    /// <summary>Business Location Type (LookupCategory.BusinessLocationType — Mainland / Free Zone).</summary>
    public int? BusinessLocationTypeId { get; set; }

    /// <summary>Preserves the row order the entities were entered in.</summary>
    public int SortOrder { get; set; }
}
