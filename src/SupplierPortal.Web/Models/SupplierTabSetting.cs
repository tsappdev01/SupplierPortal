using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

/// <summary>
/// Admin-configurable name and visibility for a supplier registration wizard tab (step). The wizard's
/// stepper, section headings, navigation and validation are driven by these, and the field manager
/// groups fields under the tab's current title. A hidden tab is skipped by the wizard entirely.
/// </summary>
public class SupplierTabSetting
{
    public int Id { get; set; }

    /// <summary>The wizard step number this tab represents (1-5). Unique.</summary>
    public int Step { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;
}
