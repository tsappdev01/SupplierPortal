using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Merges the customer contact-related tabs into a single "Contact & Address" wizard step by
    /// giving them the same tab title (tabs sharing a title merge in the form). Covers the account
    /// "Contact & Address" plus the contact "Personal Details", "Contact & UAE Pass" and "Identification".
    /// Admins can re-split them later by editing the tab titles in the Customer Field Manager.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261008000000_MergeCustomerContactTabs")]
    public partial class MergeCustomerContactTabs : Migration
    {
        private static string Upsert(string section, string group, string title) => $@"
IF EXISTS (SELECT 1 FROM dbo.CustomerTabSettings WHERE Section = N'{section}' AND GroupName = N'{group}')
    UPDATE dbo.CustomerTabSettings SET Title = N'{title}' WHERE Section = N'{section}' AND GroupName = N'{group}';
ELSE
    INSERT dbo.CustomerTabSettings (Section, GroupName, Title, Sequence, IsVisible)
    SELECT N'{section}', N'{group}', N'{title}', ISNULL(MIN(Sequence), 0), 1
      FROM dbo.CustomerFieldConfigs WHERE Section = N'{section}' AND GroupName = N'{group}';";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Upsert("Account", "Contact & Address", "Contact & Address"));
            migrationBuilder.Sql(Upsert("Contact", "Personal Details", "Contact & Address"));
            migrationBuilder.Sql(Upsert("Contact", "Contact & UAE Pass", "Contact & Address"));
            migrationBuilder.Sql(Upsert("Contact", "Identification", "Contact & Address"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.CustomerTabSettings SET Title = N'Personal Details' WHERE Section = N'Contact' AND GroupName = N'Personal Details';");
            migrationBuilder.Sql("UPDATE dbo.CustomerTabSettings SET Title = N'Contact & UAE Pass' WHERE Section = N'Contact' AND GroupName = N'Contact & UAE Pass';");
            migrationBuilder.Sql("UPDATE dbo.CustomerTabSettings SET Title = N'Identification' WHERE Section = N'Contact' AND GroupName = N'Identification';");
        }
    }
}
