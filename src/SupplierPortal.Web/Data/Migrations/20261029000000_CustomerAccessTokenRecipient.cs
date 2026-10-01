using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds <c>Recipient</c> to CustomerAccessTokens: which of the customer's two notification addresses
    /// a self-service link was emailed to ("p" = account Primary Email, "c" = contact Email). Each notification
    /// now mints one token per recipient stamped with this value, so the OTP is routed back to the same inbox
    /// that opened the link — decided server-side from the token, independent of whether the DMZ app forwards the
    /// link's query tag. Null for admin test links and legacy tokens (fall back to all addresses). Schema only.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261029000000_CustomerAccessTokenRecipient")]
    public partial class CustomerAccessTokenRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Recipient",
                table: "CustomerAccessTokens",
                type: "nvarchar(1)",
                maxLength: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Recipient",
                table: "CustomerAccessTokens");
        }
    }
}
