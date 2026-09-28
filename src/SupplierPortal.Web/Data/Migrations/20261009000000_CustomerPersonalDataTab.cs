using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Splits the customer contact person's fields into their own "Personal Data" wizard tab, placed
    /// just before "Contact & Address" (the company contact/address tab). Sets a canonical title and an explicit
    /// order (Sequence) for every customer tab so the wizard, which now orders tabs purely by Sequence, shows:
    /// Business &amp; Organization → Tax, Trade License &amp; Registration → Personal Data → Contact &amp; Address →
    /// Ownership, Banking &amp; Payment → Employment &amp; Other. Also removes the (duplicate) contact IBAN field.
    /// Admins can re-title / reorder tabs afterwards in the Customer Field Manager.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261009000000_CustomerPersonalDataTab")]
    public partial class CustomerPersonalDataTab : Migration
    {
        // Upserts a tab's Title + Sequence (only creating the row when the group actually exists in the field config).
        private static string Tab(string section, string group, string title, int seq) => $@"
IF EXISTS (SELECT 1 FROM dbo.CustomerTabSettings WHERE Section = N'{section}' AND GroupName = N'{group}')
    UPDATE dbo.CustomerTabSettings SET Title = N'{title}', Sequence = {seq} WHERE Section = N'{section}' AND GroupName = N'{group}';
ELSE
    INSERT dbo.CustomerTabSettings (Section, GroupName, Title, Sequence, IsVisible)
    SELECT N'{section}', N'{group}', N'{title}', {seq}, 1
     WHERE EXISTS (SELECT 1 FROM dbo.CustomerFieldConfigs WHERE Section = N'{section}' AND GroupName = N'{group}');";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Tab("Account", "Company Identity & Classification", "Business & Organization", 100));
            migrationBuilder.Sql(Tab("Account", "Tax, Trade License & Registration", "Tax, Trade License & Registration", 200));
            migrationBuilder.Sql(Tab("Contact", "Personal Details", "Personal Data", 300));
            migrationBuilder.Sql(Tab("Contact", "Contact & UAE Pass", "Personal Data", 310));
            migrationBuilder.Sql(Tab("Contact", "Identification", "Personal Data", 320));
            migrationBuilder.Sql(Tab("Account", "Contact & Address", "Contact & Address", 400));
            migrationBuilder.Sql(Tab("Account", "Ownership, Banking & Payment", "Ownership, Banking & Payment", 500));
            migrationBuilder.Sql(Tab("Contact", "Employment & Other", "Employment & Other", 600));

            // Remove the duplicate contact-level IBAN field (the company IBAN lives on the Ownership, Banking &
            // Payment tab). Hidden via IsRemoved so it can be restored from the Customer Field Manager.
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1 WHERE Section = N'Contact' AND FieldKey = N'IBANNumber';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0 WHERE Section = N'Contact' AND FieldKey = N'IBANNumber';");
            // Re-merge the personal groups back under "Contact & Address" (the prior migration's state).
            migrationBuilder.Sql("UPDATE dbo.CustomerTabSettings SET Title = N'Contact & Address' WHERE Section = N'Contact' AND GroupName IN (N'Personal Details', N'Contact & UAE Pass', N'Identification');");
        }
    }
}
