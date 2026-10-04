using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds a read-only reporting view (dbo.vCustomerReport) that flattens a customer with its primary
    /// contact, business unit and assigned user to readable columns, so the Report Builder — which reads a single
    /// table/view without joins — can report and filter on the Customer Portal the same way it does suppliers.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261002000000_AddCustomerReportView")]
    public partial class AddCustomerReportView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR ALTER VIEW dbo.vCustomerReport AS
SELECT
    c.Id                        AS CustomerId,
    c.CustomerCode              AS CustomerCode,
    c.CustomerId                AS CustomerRef,
    c.LegalName                 AS LegalName,
    c.CommercialName            AS CommercialName,
    c.Status                    AS Status,
    c.Priority                  AS Priority,
    bu.Name                     AS BusinessUnit,
    COALESCE(u.FullName, u.Username) AS AssignedTo,
    c.PrimaryEmail              AS PrimaryEmail,
    c.PrimaryPhone              AS PrimaryPhone,
    c.TRNNumber                 AS TRNNumber,
    c.TradeLicenseNumber        AS TradeLicenseNumber,
    c.LicensingAuthorityName    AS LicensingAuthority,
    c.IncorporationCountry      AS IncorporationCountry,
    ct.FirstName                AS ContactFirstName,
    ct.LastName                 AS ContactLastName,
    ct.Email                    AS ContactEmail,
    ct.MobileNumber             AS ContactMobile,
    c.CreatedBy                 AS CreatedBy,
    c.CreatedAt                 AS CreatedAt,
    c.UpdatedAt                 AS UpdatedAt,
    CASE WHEN c.IsDraft = 1 THEN 'Yes' ELSE 'No' END AS IsDraft
FROM dbo.Customers c
LEFT JOIN dbo.BusinessUnits bu   ON bu.Id = c.BusinessUnitId
LEFT JOIN dbo.Users u            ON u.Id  = c.AssignedToUserId
LEFT JOIN dbo.CustomerContacts ct ON ct.CustomerId = c.Id;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF OBJECT_ID('dbo.vCustomerReport', 'V') IS NOT NULL DROP VIEW dbo.vCustomerReport;");
        }
    }
}
