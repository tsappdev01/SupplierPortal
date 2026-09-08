using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the CustomerAccessTokens table backing the customer self-service flow (link token, OTP
    /// challenge, and post-verification session), mirroring SupplierAccessTokens.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260907000000_AddCustomerAccessTokens")]
    public partial class AddCustomerAccessTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerAccessTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsAdminTest = table.Column<bool>(type: "bit", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpCodeHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OtpGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpAttemptCount = table.Column<int>(type: "int", nullable: false),
                    OtpSendCount = table.Column<int>(type: "int", nullable: false),
                    OtpVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SessionExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAccessTokens_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_CustomerId",
                table: "CustomerAccessTokens",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAccessTokens_TokenHash",
                table: "CustomerAccessTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAccessTokens");
        }
    }
}
