using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models.Leasing;

/// <summary>Per-field control values for the landlord Field Manager (mirrors the customer/supplier controls).</summary>
public static class LandlordFieldControls
{
    public const string Editable = "Editable";
    public const string Readonly = "Readonly";
    public const string Lookup = "Lookup";
    public static readonly string[] All = [Editable, Readonly, Lookup];
}

/// <summary>Admin-configurable per-field settings for the landlord registration form — control per role
/// (Business Centre vs the Landlord self-service), visibility, required, help, length limits, tab/order and
/// conditional visibility. Mirrors <c>CustomerFieldConfig</c>. Drives how the internal detail and the
/// self-service wizard render each landlord field, with no code change.</summary>
public class LandlordFieldConfig
{
    public int Id { get; set; }

    /// <summary>The entity property this row governs (e.g. "FullName" on the contact). Unique within a section.</summary>
    [Required, MaxLength(100)] public string FieldKey { get; set; } = string.Empty;

    [Required, MaxLength(200)] public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>Optional short help/description shown as an info (ℹ) tooltip next to the field label when not blank.</summary>
    [MaxLength(500)] public string? HelpText { get; set; }

    /// <summary>Optional min/max character length for an editable text field (null = no limit).</summary>
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }

    /// <summary>"Landlord" (registration-level fields) or "Contact" (the primary contact person).</summary>
    [MaxLength(20)] public string Section { get; set; } = "Contact";

    /// <summary>The tab/group this field belongs to (e.g. "Primary Contact").</summary>
    [MaxLength(80)] public string GroupName { get; set; } = "";

    public int Sequence { get; set; }

    /// <summary>How the field renders: text | lookup | date | yesno | phone.</summary>
    [MaxLength(20)] public string FieldKind { get; set; } = "text";

    /// <summary>Control for the Business Centre (internal). See <see cref="LandlordFieldControls"/>.</summary>
    [MaxLength(20)] public string BusinessControl { get; set; } = LandlordFieldControls.Editable;

    /// <summary>Control for the Landlord (self-service). See <see cref="LandlordFieldControls"/>.</summary>
    [MaxLength(20)] public string LandlordControl { get; set; } = LandlordFieldControls.Editable;

    public bool IsVisible { get; set; } = true;
    public bool IsRequired { get; set; }

    /// <summary>Removed from the form entirely but kept so an admin can restore it.</summary>
    public bool IsRemoved { get; set; }

    /// <summary>Optional conditional rule: the FieldKey of a controlling Yes/No field (same section). When set,
    /// this field is shown (and required, if <see cref="IsRequired"/>) only while the controlling field equals
    /// <see cref="ControllingValue"/>. Null = always shown.</summary>
    [MaxLength(100)] public string? ControllingFieldKey { get; set; }

    [MaxLength(40)] public string? ControllingValue { get; set; }
}

/// <summary>Admin-configurable title, order, visibility and per-user edit restriction for one landlord form tab
/// (a distinct <see cref="LandlordFieldConfig.GroupName"/> within a section). Mirrors <c>CustomerTabSetting</c>.</summary>
public class LandlordTabSetting
{
    public int Id { get; set; }

    [Required, MaxLength(20)] public string Section { get; set; } = "Contact";

    [Required, MaxLength(80)] public string GroupName { get; set; } = "";

    [Required, MaxLength(150)] public string Title { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>Comma-separated internal user Ids permitted to EDIT this tab on the internal form. Null/empty =
    /// no restriction; otherwise other internal users see it read-only. Does not affect self-service.</summary>
    [MaxLength(1000)] public string? EditableByUserIdsCsv { get; set; }
}
