using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Globalises the customer wizard tab order. Tab sequences were numbered per section (Account 1..n
    /// and Contact 1..n), so the wizard — which orders steps by a single global sequence — interleaved Company
    /// and Individual tabs by number and did not match the Manage Tabs list. Renumber all tabs into one
    /// contiguous sequence (Company tabs first, then Individual, preserving each section's current order) so the
    /// form matches the modal, and an admin can then move a tab across the Company/Individual boundary. Data-only.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261021000000_CustomerGlobalizeTabSequences")]
    public partial class CustomerGlobalizeTabSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
;WITH ordered AS (
    SELECT Id, ROW_NUMBER() OVER (
        ORDER BY CASE WHEN Section = N'Account' THEN 0 ELSE 1 END, Sequence, GroupName) AS rn
    FROM dbo.CustomerTabSettings
)
UPDATE t SET t.Sequence = o.rn
FROM dbo.CustomerTabSettings t
JOIN ordered o ON t.Id = o.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore per-section 1..n numbering.
            migrationBuilder.Sql(@"
;WITH ordered AS (
    SELECT Id, ROW_NUMBER() OVER (PARTITION BY Section ORDER BY Sequence, GroupName) AS rn
    FROM dbo.CustomerTabSettings
)
UPDATE t SET t.Sequence = o.rn
FROM dbo.CustomerTabSettings t
JOIN ordered o ON t.Id = o.Id;");
        }
    }
}
