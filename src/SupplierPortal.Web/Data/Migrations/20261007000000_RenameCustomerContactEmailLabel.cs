using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Renames the contact "Email" field label to "Contact Email" on the customer form.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261007000000_RenameCustomerContactEmailLabel")]
    public partial class RenameCustomerContactEmailLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = 'Contact Email' WHERE Section = 'Contact' AND FieldKey = 'Email';");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = 'Email' WHERE Section = 'Contact' AND FieldKey = 'Email';");
    }
}
