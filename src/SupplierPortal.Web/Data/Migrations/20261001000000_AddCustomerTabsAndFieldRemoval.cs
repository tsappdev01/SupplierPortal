using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Brings the Customer field manager to Supplier parity: adds IsRemoved to CustomerFieldConfigs
    /// (remove/restore a field) and a CustomerTabSettings table (admin-editable tab title, order, visibility,
    /// and a per-user edit restriction) keyed by Section + GroupName.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261001000000_AddCustomerTabsAndFieldRemoval")]
    public partial class AddCustomerTabsAndFieldRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRemoved",
                table: "CustomerFieldConfigs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CustomerTabSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    EditableByUserIdsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerTabSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerTabSettings_Section_GroupName",
                table: "CustomerTabSettings",
                columns: new[] { "Section", "GroupName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CustomerTabSettings");
            migrationBuilder.DropColumn(name: "IsRemoved", table: "CustomerFieldConfigs");
        }
    }
}
