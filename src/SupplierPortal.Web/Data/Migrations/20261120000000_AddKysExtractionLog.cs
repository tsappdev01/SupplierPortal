using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261120000000_AddKysExtractionLog")]
    public partial class AddKysExtractionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Per-field extraction log for "Extract & store KYS data from documents": one row per field read
            // from a document, with the value, the source document/file, and whether it was stored or skipped.
            // Guarded + idempotent so it is safe to run on any existing database.
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[SupplierKysExtractionLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SupplierKysExtractionLogs] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SupplierId] int NOT NULL,
        [DocumentId] int NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [DocumentLabel] nvarchar(100) NOT NULL,
        [FileName] nvarchar(400) NULL,
        [FieldName] nvarchar(150) NOT NULL,
        [FieldValue] nvarchar(2000) NULL,
        [Outcome] nvarchar(40) NOT NULL,
        [ExtractedAt] datetime2 NOT NULL,
        [ExtractedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_SupplierKysExtractionLogs] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_SupplierKysExtractionLogs_SupplierId]
        ON [dbo].[SupplierKysExtractionLogs] ([SupplierId]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"IF OBJECT_ID(N'[dbo].[SupplierKysExtractionLogs]', N'U') IS NOT NULL DROP TABLE [dbo].[SupplierKysExtractionLogs];");
        }
    }
}
