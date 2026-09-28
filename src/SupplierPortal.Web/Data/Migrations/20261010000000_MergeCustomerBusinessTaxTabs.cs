using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Merges the customer "Tax, Trade License &amp; Registration" tab into the "Business &amp; Organization"
    /// tab (both Account-section) by giving them the same title. Sequence 110 keeps the tax group directly after
    /// the company-identity group and before "Personal Data" (300). Admins can split them again by renaming the
    /// tab in the Customer Field Manager.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261010000000_MergeCustomerBusinessTaxTabs")]
    public partial class MergeCustomerBusinessTaxTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM dbo.CustomerTabSettings WHERE Section = N'Account' AND GroupName = N'Tax, Trade License & Registration')
    UPDATE dbo.CustomerTabSettings SET Title = N'Business & Organization', Sequence = 110 WHERE Section = N'Account' AND GroupName = N'Tax, Trade License & Registration';
ELSE
    INSERT dbo.CustomerTabSettings (Section, GroupName, Title, Sequence, IsVisible)
    SELECT N'Account', N'Tax, Trade License & Registration', N'Business & Organization', 110, 1
     WHERE EXISTS (SELECT 1 FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND GroupName = N'Tax, Trade License & Registration');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerTabSettings SET Title = N'Tax, Trade License & Registration', Sequence = 200 WHERE Section = N'Account' AND GroupName = N'Tax, Trade License & Registration';");
    }
}
