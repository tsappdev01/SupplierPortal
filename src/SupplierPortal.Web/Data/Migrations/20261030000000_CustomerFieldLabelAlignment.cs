using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Aligns seven Account field DisplayLabels with the business's updated "Customer Data Field" names
    /// (the "(LOV)" annotations in that list are ignored — they denote list-of-values columns, not label text):
    ///   Legal Name                               -> Customer Name (Legal Name)
    ///   Account Name Arabic                      -> Customer Name Arabic
    ///   VAT Structure                            -> VAT Registration Type
    ///   TRN Applicable (Yes/No)                  -> VAT Registered? (Y/N)
    ///   TRN Number                               -> Tax Registration Number
    ///   Owner 1 - Passport Issue Country         -> Owner 1 - Passport Issue Country Code
    ///   Owner 1 - Passport Issue Place/Country   -> Owner 1 - Passport Issue Place/Country Name
    /// Owner 2's passport labels are intentionally left without the "Code"/"Name" suffix, matching the list.
    /// The label drives both the form field and the export/template header. Pure data; reversible.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261030000000_CustomerFieldLabelAlignment")]
    public partial class CustomerFieldLabelAlignment : Migration
    {
        private static readonly (string FieldKey, string NewLabel, string OldLabel)[] Labels =
        {
            ("LegalName", "Customer Name (Legal Name)", "Legal Name"),
            ("AccountNameArabic", "Customer Name Arabic", "Account Name Arabic"),
            ("VATStructure", "VAT Registration Type", "VAT Structure"),
            ("TRNApplicable", "VAT Registered? (Y/N)", "TRN Applicable (Yes/No)"),
            ("TRNNumber", "Tax Registration Number", "TRN Number"),
            ("Owner1PassportIssueCountry", "Owner 1 - Passport Issue Country Code", "Owner 1 - Passport Issue Country"),
            ("Owner1PassportIssuePlaceCountry", "Owner 1 - Passport Issue Place/Country Name", "Owner 1 - Passport Issue Place/Country"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, newLabel, _) in Labels)
            {
                migrationBuilder.Sql(
                    $"UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'{newLabel.Replace("'", "''")}' " +
                    $"WHERE Section = N'Account' AND FieldKey = N'{key}';");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (key, _, oldLabel) in Labels)
            {
                migrationBuilder.Sql(
                    $"UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'{oldLabel.Replace("'", "''")}' " +
                    $"WHERE Section = N'Account' AND FieldKey = N'{key}';");
            }
        }
    }
}
