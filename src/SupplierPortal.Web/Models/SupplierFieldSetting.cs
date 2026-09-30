using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// Admin-configurable Mandatory/Optional flag for a single field on the supplier registration
/// wizard. <see cref="FieldKey"/> matches a <see cref="SupplierFormModel"/> property name and is
/// the single source of truth consulted by both the wizard's step validation and the Excel bulk
/// importer's required-field checks.
/// </summary>
public class SupplierFieldSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FieldKey { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>Which wizard step (1-4) this field appears on.</summary>
    public int Step { get; set; }

    /// <summary>Display order within the step.</summary>
    public int Sequence { get; set; }

    public bool IsMandatory { get; set; }
}
