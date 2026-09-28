using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Renders the customer phone fields with the combined country-code + number control (like the
    /// supplier), by setting their FieldKind to 'phone': Primary/Secondary/Landline phone on the account and
    /// Mobile/Secondary/Landline/Employer phone on the contact.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261006000000_CustomerPhoneFields")]
    public partial class CustomerPhoneFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET FieldKind = 'phone'
 WHERE (Section = 'Account' AND FieldKey IN ('PrimaryPhone', 'SecondaryPhone', 'LandlineMobile'))
    OR (Section = 'Contact' AND FieldKey IN ('MobileNumber', 'SecondaryPhoneNumber', 'LandlineNumber', 'EmployerPhone'));");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs SET FieldKind = 'text'
 WHERE (Section = 'Account' AND FieldKey IN ('PrimaryPhone', 'SecondaryPhone', 'LandlineMobile'))
    OR (Section = 'Contact' AND FieldKey IN ('MobileNumber', 'SecondaryPhoneNumber', 'LandlineNumber', 'EmployerPhone'));");
    }
}
