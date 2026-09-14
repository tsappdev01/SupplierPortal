using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Links a supplier to a department so users in that department can manage it.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260919000000_AddSupplierDepartment")]
    public partial class AddSupplierDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_DepartmentId",
                table: "Suppliers",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Departments_DepartmentId",
                table: "Suppliers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Suppliers_Departments_DepartmentId", table: "Suppliers");
            migrationBuilder.DropIndex(name: "IX_Suppliers_DepartmentId", table: "Suppliers");
            migrationBuilder.DropColumn(name: "DepartmentId", table: "Suppliers");
        }
    }
}
