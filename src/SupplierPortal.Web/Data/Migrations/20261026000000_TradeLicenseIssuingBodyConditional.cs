using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Makes "Trade License Issuing Body" a conditional field: it is shown (and required) only when the
    /// "Licensing Authority Name" LOV equals "Others"; otherwise it is hidden. Uses the generic field "Depends
    /// On" rule (ControllingFieldKey / ControllingValue), now that LOV-driven conditions are supported.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261026000000_TradeLicenseIssuingBodyConditional")]
    public partial class TradeLicenseIssuingBodyConditional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] " +
                "SET [ControllingFieldKey] = 'LicensingAuthorityName', [ControllingValue] = 'Others' " +
                "WHERE [FieldKey] = 'TradeLicenseIssuingBody' AND [Section] = 'Account';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [CustomerFieldConfigs] " +
                "SET [ControllingFieldKey] = NULL, [ControllingValue] = NULL " +
                "WHERE [FieldKey] = 'TradeLicenseIssuingBody' AND [Section] = 'Account';");
        }
    }
}
