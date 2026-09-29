using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the CustomerVatGroupEntities table: the entities a customer lists under its VAT Group,
    /// captured on the Business &amp; Organization tab when VAT Structure = "VAT Group" (mirrors the supplier's
    /// SupplierVatGroupEntities). Wave and location-type are plain string columns (the customer portal is
    /// LOV/string-driven, unlike the supplier's LookupValue ids).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261015000000_AddCustomerVatGroupEntities")]
    public partial class AddCustomerVatGroupEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerVatGroupEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    BusinessLegalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EInvoicingWave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrnNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TinNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BusinessLocationType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerVatGroupEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerVatGroupEntities_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVatGroupEntities_CustomerId",
                table: "CustomerVatGroupEntities",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "CustomerVatGroupEntities");
    }
}
