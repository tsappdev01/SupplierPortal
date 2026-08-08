using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BaanRef = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CustomerId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusinessUnitId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CommercialName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AccountNameArabic = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AccountType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TypeOfCustomer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TypeOfOrganization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VATStructure = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NumberOfEntities = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CustomerBuyerTIN = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EINvoicingWaveApplicability = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BusinessActivity = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PrimaryPhone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PrimaryEmail = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondaryEmailAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondaryPhone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    URL = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BuyerLegalRegistrationIdentifierType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TRNApplicable = table.Column<bool>(type: "bit", nullable: true),
                    TRNNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TradeLicenseNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LicensingAuthorityName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AuthorityCode = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TradeLicenseIssuingBody = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TradeLicenseIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TradeLicenseExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IncorporationCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PlaceOfIncorporation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EstablishmentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LandlineMobile = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PEP = table.Column<bool>(type: "bit", nullable: true),
                    CustomerBankAccountName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IBANNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PowerOfAttorney = table.Column<bool>(type: "bit", nullable: true),
                    PowerOfAttorney2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountryName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    POBox = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountrySubdivision = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmirateCity = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine12 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine22 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    POBox2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Emirate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1Nationality = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1Passport = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1PassportIssueCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1PassportIssuePlaceCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner1SharePercentage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2Nationality = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2Passport = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2PassportIssueCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2PassportIssuePlaceCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner2SharePercentage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_BusinessUnits_BusinessUnitId",
                        column: x => x.BusinessUnitId,
                        principalTable: "BusinessUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Account = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MaritalStatus = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BuyingRole = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondaryEmail = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondaryPhoneNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IBANNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ResidentialStatus = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Nationality = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    POBox = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmirateCity = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Country2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine12 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AddressLine22 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    POBox2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Emirates = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Passport = table.Column<bool>(type: "bit", nullable: true),
                    PassportNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PassportIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassportExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassportIssueCountryCode = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PassportIssuePlaceCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DualPassport = table.Column<bool>(type: "bit", nullable: true),
                    DualPassportNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DualPassportIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DualPassportExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VISAExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VISANumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TypeOfID = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountryGovtIDNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CountryGovtIDIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountryGovtIDExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmirateID = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmirateIDExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmiratesIDIssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssueCountry = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BirthPlace = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MothersName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AliasName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Nationality2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TAXNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PEP = table.Column<bool>(type: "bit", nullable: true),
                    LandlineNumber = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmployerName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmployerPhone = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EmployerAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceOfIncome = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ReceivedTradeLicense = table.Column<bool>(type: "bit", nullable: true),
                    ReceivedVATCertificate = table.Column<bool>(type: "bit", nullable: true),
                    TypeOfCustomer = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassRegisteredEmail1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassRegisteredEmail2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassRegisteredEmail3 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassRegisteredEmail4 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassMobileNo1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassMobileNo2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassMobileNo3 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UAEPassMobileNo4 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerContacts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDocuments_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerContacts_CustomerId",
                table: "CustomerContacts",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDocuments_CustomerId",
                table: "CustomerDocuments",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BusinessUnitId",
                table: "Customers",
                column: "BusinessUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerContacts");

            migrationBuilder.DropTable(
                name: "CustomerDocuments");

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
