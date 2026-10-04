using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds portal-specific sender e-mail addresses to a Business Unit (Supplier / Customer / Landlord /
    /// Broker). Each is nullable; when set, system-generated emails originating from that portal are sent from
    /// this address instead of the generic BusinessUnit.Email, so each business function can be clearly
    /// identified. When blank, the generic Email (then the global default sender) is used, so existing
    /// deployments are unchanged until an address is configured.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261017000000_AddBusinessUnitPortalSenderEmails")]
    public partial class AddBusinessUnitPortalSenderEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplierSenderEmail", table: "BusinessUnits", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CustomerSenderEmail", table: "BusinessUnits", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "LandlordSenderEmail", table: "BusinessUnits", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "BrokerSenderEmail", table: "BusinessUnits", type: "nvarchar(256)", maxLength: 256, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SupplierSenderEmail", table: "BusinessUnits");
            migrationBuilder.DropColumn(name: "CustomerSenderEmail", table: "BusinessUnits");
            migrationBuilder.DropColumn(name: "LandlordSenderEmail", table: "BusinessUnits");
            migrationBuilder.DropColumn(name: "BrokerSenderEmail", table: "BusinessUnits");
        }
    }
}
