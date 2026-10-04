using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds an optional conditional rule to a customer document type so a document can be shown /
    /// required only when a controlling customer field (a Yes/No or LOV field) holds a given value — e.g. the
    /// VAT Certificate required only when the VAT-registered field is Yes, or the Power of Attorney required
    /// only when "Power of Attorney" is Yes. Null = the document always applies (existing behaviour).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261024000000_AddCustomerDocumentTypeConditional")]
    public partial class AddCustomerDocumentTypeConditional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControllingFieldKey",
                table: "CustomerDocumentTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControllingValue",
                table: "CustomerDocumentTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ControllingFieldKey", table: "CustomerDocumentTypes");
            migrationBuilder.DropColumn(name: "ControllingValue", table: "CustomerDocumentTypes");
        }
    }
}
