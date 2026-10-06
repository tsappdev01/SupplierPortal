using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Surfaces the supplier Active/Inactive flag in the Report Builder with business-friendly wording:
    /// the <c>dbo.vSupplierReport</c> view's old <c>IsActive</c> ('Yes'/'No') column becomes
    /// <c>[Supplier Status]</c> with values 'Active'/'Inactive'. The Report Builder reads view columns from live
    /// metadata, so this makes a "Supplier Status" field available to select and filter on with no other change.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261101000000_SupplierReportViewSupplierStatus")]
    public partial class SupplierReportViewSupplierStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vSupplierReport AS
SELECT
    s.Id                    AS SupplierId,
    s.SupplierNumber        AS SupplierNumber,
    s.SupplierName          AS SupplierName,
    st.DisplayName          AS Status,
    s.Priority              AS Priority,
    bu.Name                 AS BusinessUnit,
    s.Email                 AS Email,
    s.ContactEmail          AS ContactEmail,
    s.TradeLicenseNumber    AS TradeLicenseNumber,
    s.TaxRegistrationNumber AS TaxRegistrationNumber,
    city.DisplayName        AS City,
    country.DisplayName     AS Country,
    s.CreatedBy             AS CreatedBy,
    CASE WHEN s.IsActive = 1 THEN 'Active' ELSE 'Inactive' END AS [Supplier Status]
FROM dbo.Suppliers s
LEFT JOIN dbo.LookupValues st      ON st.Id      = s.StatusId
LEFT JOIN dbo.BusinessUnits bu     ON bu.Id      = s.BusinessUnitId
LEFT JOIN dbo.LookupValues city    ON city.Id    = s.CityId
LEFT JOIN dbo.LookupValues country ON country.Id = s.CountryId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vSupplierReport AS
SELECT
    s.Id                    AS SupplierId,
    s.SupplierNumber        AS SupplierNumber,
    s.SupplierName          AS SupplierName,
    st.DisplayName          AS Status,
    s.Priority              AS Priority,
    bu.Name                 AS BusinessUnit,
    s.Email                 AS Email,
    s.ContactEmail          AS ContactEmail,
    s.TradeLicenseNumber    AS TradeLicenseNumber,
    s.TaxRegistrationNumber AS TaxRegistrationNumber,
    city.DisplayName        AS City,
    country.DisplayName     AS Country,
    s.CreatedBy             AS CreatedBy,
    CASE WHEN s.IsActive = 1 THEN 'Yes' ELSE 'No' END AS IsActive
FROM dbo.Suppliers s
LEFT JOIN dbo.LookupValues st      ON st.Id      = s.StatusId
LEFT JOIN dbo.BusinessUnits bu     ON bu.Id      = s.BusinessUnitId
LEFT JOIN dbo.LookupValues city    ON city.Id    = s.CityId
LEFT JOIN dbo.LookupValues country ON country.Id = s.CountryId;");
        }
    }
}
