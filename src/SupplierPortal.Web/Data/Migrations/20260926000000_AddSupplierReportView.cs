using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds a read-only reporting view (dbo.vSupplierReport) that flattens the common supplier lookups
    /// (Status, Business Unit, City, Country) to their display names, so the Report Builder — which reads a
    /// single table/view without joins — can report and filter on readable text (e.g. Status =
    /// 'Awaiting Supplier Response') instead of numeric lookup ids.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260926000000_AddSupplierReportView")]
    public partial class AddSupplierReportView : Migration
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
    CASE WHEN s.IsActive = 1 THEN 'Yes' ELSE 'No' END AS IsActive
FROM dbo.Suppliers s
LEFT JOIN dbo.LookupValues st      ON st.Id      = s.StatusId
LEFT JOIN dbo.BusinessUnits bu     ON bu.Id      = s.BusinessUnitId
LEFT JOIN dbo.LookupValues city    ON city.Id    = s.CityId
LEFT JOIN dbo.LookupValues country ON country.Id = s.CountryId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.vSupplierReport', 'V') IS NOT NULL DROP VIEW dbo.vSupplierReport;");
        }
    }
}
