using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierWorkflowStatusAndFieldVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisibleToSupplier",
                table: "SupplierFieldSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_StatusId",
                table: "Suppliers",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_StatusId",
                table: "Suppliers",
                column: "StatusId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_StatusId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_StatusId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsVisibleToSupplier",
                table: "SupplierFieldSettings");
        }
    }
}
