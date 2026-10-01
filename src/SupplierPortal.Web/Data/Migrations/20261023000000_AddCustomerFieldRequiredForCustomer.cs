using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds a separate "required for the customer" flag to the customer field configuration, so a field
    /// can be mandatory for internal users (<c>IsRequired</c>) and optional for the customer self-service form
    /// (or vice-versa). Existing rows are seeded to match <c>IsRequired</c> so current behaviour is unchanged
    /// until an admin adjusts the new toggle.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261023000000_AddCustomerFieldRequiredForCustomer")]
    public partial class AddCustomerFieldRequiredForCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRequiredForCustomer",
                table: "CustomerFieldConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Back-compat: seed the new customer-required flag from the existing (internal) required flag so the
            // self-service form keeps validating exactly the same fields until an admin changes the toggles.
            migrationBuilder.Sql("UPDATE [CustomerFieldConfigs] SET [IsRequiredForCustomer] = [IsRequired];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsRequiredForCustomer", table: "CustomerFieldConfigs");
        }
    }
}
