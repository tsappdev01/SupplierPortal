using System.ComponentModel.DataAnnotations;

namespace SupplierPortal.Web.Models;

public class Bank
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public List<BankBranch> Branches { get; set; } = [];
}
