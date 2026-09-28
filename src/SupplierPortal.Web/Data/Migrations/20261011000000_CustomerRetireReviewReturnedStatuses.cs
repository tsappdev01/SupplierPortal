using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Retires the customer "For Review" and "Returned" statuses (the customer workflow now mirrors the
    /// supplier: a submission goes straight to Completed). Migrates existing records: "For Review" (customer had
    /// submitted, awaiting approval) → "Completed"; "Returned" (had been sent back) → "Awaiting Customer Response".</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261011000000_CustomerRetireReviewReturnedStatuses")]
    public partial class CustomerRetireReviewReturnedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.Customers SET Status = N'Completed' WHERE Status = N'For Review';");
            migrationBuilder.Sql("UPDATE dbo.Customers SET Status = N'Awaiting Customer Response' WHERE Status = N'Returned';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data alignment; the retired statuses are not restored on rollback.
        }
    }
}
