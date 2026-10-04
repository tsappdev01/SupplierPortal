using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the CustomerDocumentTypes master table and seeds the UAE KYC document checklist with its
    /// mandatory/optional requirement.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260909000000_AddCustomerDocumentTypes")]
    public partial class AddCustomerDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Requirement = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDocumentTypes", x => x.Id);
                });

            migrationBuilder.Sql(@"INSERT INTO [CustomerDocumentTypes] ([Name],[Requirement],[IsRequired],[Sequence],[IsActive]) VALUES
('Emirates ID','Mandatory for UAE residents',1,1,1),
('Passport','Mandatory / alternative identity document',1,2,1),
('Visa / Residence Permit','For UAE residents, where applicable',0,3,1),
('Proof of Address','Utility bill, tenancy contract, government correspondence, etc.',1,4,1),
('Source of Funds / Wealth','Required where applicable',0,5,1),
('Purpose of Relationship / Transaction','Required where applicable',0,6,1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerDocumentTypes");
        }
    }
}
