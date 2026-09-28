using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the declaration / digital-signature fields to Customers so the customer self-service
    /// submit can capture a signed declaration (mirrors the supplier). The hand-drawn signature image itself
    /// is not stored on the row — it is rendered into a signed declaration PDF stored as a customer document.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261003000000_AddCustomerDeclaration")]
    public partial class AddCustomerDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DisclaimerAcknowledged", table: "Customers", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<DateTime>(
                name: "DisclaimerAcknowledgedAt", table: "Customers", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DisclaimerAcknowledgedBy", table: "Customers", type: "nvarchar(150)", maxLength: 150, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DisclaimerText", table: "Customers", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "DisclaimerSignature", table: "Customers", type: "nvarchar(200)", maxLength: 200, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DisclaimerSignature", table: "Customers");
            migrationBuilder.DropColumn(name: "DisclaimerText", table: "Customers");
            migrationBuilder.DropColumn(name: "DisclaimerAcknowledgedBy", table: "Customers");
            migrationBuilder.DropColumn(name: "DisclaimerAcknowledgedAt", table: "Customers");
            migrationBuilder.DropColumn(name: "DisclaimerAcknowledged", table: "Customers");
        }
    }
}
