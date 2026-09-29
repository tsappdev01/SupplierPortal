using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds configurable per-field minimum / maximum character length to the customer field
    /// configuration. Both are nullable (null = no limit) and only apply to editable text fields; the customer
    /// forms validate the entered text against them and surface a Toast when the length is out of range.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261016000000_AddCustomerFieldLengthLimits")]
    public partial class AddCustomerFieldLengthLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinLength",
                table: "CustomerFieldConfigs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxLength",
                table: "CustomerFieldConfigs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MinLength", table: "CustomerFieldConfigs");
            migrationBuilder.DropColumn(name: "MaxLength", table: "CustomerFieldConfigs");
        }
    }
}
