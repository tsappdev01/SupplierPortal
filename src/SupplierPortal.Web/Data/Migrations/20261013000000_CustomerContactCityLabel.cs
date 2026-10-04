using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Renames the contact (Personal Data) "Emirate/City" field label to "City". The field then renders
    /// as a UAE-emirate dropdown when the contact's country is the UAE, or a free-text box otherwise (handled in
    /// the forms).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261013000000_CustomerContactCityLabel")]
    public partial class CustomerContactCityLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'City' WHERE Section = N'Contact' AND FieldKey = N'EmirateCity';");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Emirate/City' WHERE Section = N'Contact' AND FieldKey = N'EmirateCity';");
    }
}
