using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class BankBranch
{
    public int Id { get; set; }

    [Required]
    public int BankId { get; set; }
    public Bank? Bank { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}
