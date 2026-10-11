using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyListProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set-based Property list: collapse dbo.Property to one row per property code (preferring a row that
            // carries an email), join the latest landlord registration per code, and apply zone/category/search
            // and status filtering — all in SQL Server. Replaces pulling the whole view into the app.
            migrationBuilder.Sql(@"
CREATE OR ALTER PROCEDURE [leasing].[PropertyList]
    @Zone nvarchar(20) = NULL,
    @Category nvarchar(200) = NULL,
    @Search nvarchar(200) = NULL,
    @NotStartedOnly bit = 0,
    @StatusCsv nvarchar(400) = NULL,
    @Take int = 100000
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Prem AS (
        SELECT p.Id, p.PropertyCode, p.BuildingName, p.CategoryName, p.CompanyName, p.ContactName, p.EmailAddress,
               ROW_NUMBER() OVER (PARTITION BY p.PropertyCode
                    ORDER BY CASE WHEN p.EmailAddress IS NOT NULL AND LTRIM(RTRIM(p.EmailAddress)) <> '' THEN 0 ELSE 1 END, p.Id) AS rn
        FROM dbo.Property p
        WHERE (@Zone IS NULL OR p.PropertyCode LIKE @Zone + '-%')
          AND (@Category IS NULL OR p.CategoryName = @Category)
          AND (@Search IS NULL
               OR p.PropertyCode LIKE '%' + @Search + '%'
               OR p.CompanyName  LIKE '%' + @Search + '%'
               OR p.ContactName  LIKE '%' + @Search + '%'
               OR p.EmailAddress LIKE '%' + @Search + '%')
    ),
    Codes AS (
        SELECT TOP (@Take) Id, PropertyCode, BuildingName, CategoryName, CompanyName, ContactName, EmailAddress
        FROM Prem WHERE rn = 1 ORDER BY PropertyCode
    ),
    Reg AS (
        SELECT l.Id, l.PropertyCode, l.[Status], l.LinkActive,
               ROW_NUMBER() OVER (PARTITION BY l.PropertyCode ORDER BY l.Id DESC) AS rn
        FROM [leasing].[Landlords] l
    )
    SELECT
        c.Id            AS PremisesId,
        c.PropertyCode  AS PropertyCode,
        c.BuildingName  AS BuildingName,
        c.CategoryName  AS CategoryName,
        c.CompanyName   AS CompanyName,
        c.ContactName   AS ContactName,
        c.EmailAddress  AS EmailAddress,
        r.Id            AS RegistrationId,
        r.[Status]      AS StatusInt,
        CAST(ISNULL(r.LinkActive, 0) AS bit) AS LinkActive
    FROM Codes c
    LEFT JOIN Reg r ON r.PropertyCode = c.PropertyCode AND r.rn = 1
    WHERE
        (@NotStartedOnly = 1 AND r.Id IS NULL)
        OR (@NotStartedOnly = 0 AND (
                @StatusCsv IS NULL
             OR (r.[Status] IS NOT NULL
                 AND r.[Status] IN (SELECT TRY_CAST(value AS int) FROM STRING_SPLIT(@StatusCsv, ',')))))
    ORDER BY c.PropertyCode;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP PROCEDURE IF EXISTS [leasing].[PropertyList];");
        }
    }
}
