using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Country Subdivision now stores the subdivision CODE (AUH, DXB, …) while the dropdown shows the
    /// name. Convert existing stored emirate NAMES to their codes so the value, the dropdown selection and the
    /// Excel template all agree. Unknown / already-coded values are left untouched.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261025000000_CustomerSubdivisionToCode")]
    public partial class CustomerSubdivisionToCode : Migration
    {
        private static readonly (string Name, string Code)[] Map =
        {
            ("Abu Dhabi", "AUH"), ("Dubai", "DXB"), ("Sharjah", "SHJ"), ("Umm Al Quwain", "UAQ"),
            ("Fujairah", "FUJ"), ("Ajman", "AJM"), ("Ras Al Khaimah", "RAK"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, code) in Map)
            {
                migrationBuilder.Sql(
                    $"UPDATE [Customers] SET [CountrySubdivision] = '{code}' WHERE [CountrySubdivision] = '{name}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (name, code) in Map)
            {
                migrationBuilder.Sql(
                    $"UPDATE [Customers] SET [CountrySubdivision] = '{name}' WHERE [CountrySubdivision] = '{code}';");
            }
        }
    }
}
