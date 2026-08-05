using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class ExportService
{
    // Full field set for CSV (opened in Excel, width is not a concern).
    private static readonly string[] CsvColumns =
    [
        "Supplier Number", "Supplier Name", "Email", "Tax Organization Type", "Supplier Type",
        "Taxpayer Country", "Country Name", "Tax Registration Number", "Trade License Number",
        "D-U-N-S Number", "Corporate Web Site", "Business Relationship", "One-Time Supplier",
        "Address Name", "Address Line 1", "Address Line 2", "City", "State", "Province",
        "Postal Code", "Country", "Remittance Email",
        "Prefix", "First Name", "Administrative Contact", "Phone Country Code", "Phone Area Code", "Phone",
        "Business Unit", "Supplier Site", "Procurement BU", "Client BU", "Bill-to BU",
        "Communication Method", "RFQ or Bidding", "Ordering", "Sourcing Only", "Purchasing", "Pay", "Primary Pay",
        "Payment Method", "Payment Terms", "Payment Method Code", "Remit Advice Email", "VAT Code", "Currency",
        "Bank Name", "Branch Name", "Account Country Code", "Account Name", "Account Number",
        "Account Currency Code", "Swift Code", "IBAN", "Account Start Date", "Allow International Payments",
        "Active"
    ];

    // Curated subset for PDF (landscape A4 page) so columns stay legible.
    private static readonly string[] PdfColumns =
    [
        "Supplier Number", "Supplier Name", "Email", "Tax Organization Type", "Supplier Type",
        "Taxpayer Country", "Country Name", "Tax Registration Number", "Address Line 1",
        "Address Line 2", "City", "Country", "Active"
    ];

    private static IEnumerable<string> ToCsvRow(Supplier s) =>
    [
        s.SupplierNumber,
        s.SupplierName,
        s.Email ?? "",
        s.TaxOrganizationType?.DisplayName ?? "",
        s.SupplierType?.DisplayName ?? "",
        s.TaxpayerCountry?.DisplayName ?? "",
        s.CountryName?.DisplayName ?? "",
        s.TaxRegistrationNumber,
        s.TradeLicenseNumber ?? "",
        s.DunsNumber ?? "",
        s.CorporateWebSite ?? "",
        s.BusinessRelationship?.DisplayName ?? "",
        s.IsOneTimeSupplier ? "Yes" : "No",
        s.AddressName ?? "",
        s.AddressLine1 ?? "",
        s.AddressLine2 ?? "",
        s.City?.DisplayName ?? "",
        s.State ?? "",
        s.Province?.DisplayName ?? "",
        s.PostalCode ?? "",
        s.Country?.DisplayName ?? "",
        s.RemittanceEmail ?? "",
        s.Prefix?.DisplayName ?? "",
        s.FirstName ?? "",
        s.AdministrativeContact ?? "",
        s.PhoneCountryCode?.PhoneCode ?? "",
        s.PhoneAreaCode ?? "",
        s.Phone ?? "",
        s.BusinessUnit?.Name ?? "",
        s.SupplierSiteBu?.Name ?? "",
        s.ProcurementBu?.Name ?? "",
        s.ClientBu?.Name ?? "",
        s.BillToBu?.Name ?? "",
        s.CommunicationMethod?.DisplayName ?? "",
        s.IsRfqOrBidding ? "Yes" : "No",
        s.IsOrdering ? "Yes" : "No",
        s.IsSourcingOnly ? "Yes" : "No",
        s.IsPurchasing ? "Yes" : "No",
        s.IsPay ? "Yes" : "No",
        s.IsPrimaryPay ? "Yes" : "No",
        s.PaymentMethod?.DisplayName ?? "",
        s.PaymentTerms?.DisplayName ?? "",
        s.PaymentMethodCode ?? "",
        s.RemitAdviceEmail ?? "",
        s.VatCode?.Code ?? "",
        s.Currency?.Code ?? "",
        s.Bank?.Name ?? "",
        s.BankBranch?.Name ?? "",
        s.AccountCountryCode?.DisplayName ?? "",
        s.AccountName ?? "",
        s.AccountNumber ?? "",
        s.AccountCurrencyCode?.Code ?? "",
        s.SwiftCode ?? "",
        s.Iban ?? "",
        s.AccountStartDate?.ToString("yyyy-MM-dd") ?? "",
        s.AllowInternationalPayments ? "Yes" : "No",
        s.IsActive ? "Yes" : "No"
    ];

    private static IEnumerable<string> ToPdfRow(Supplier s) =>
    [
        s.SupplierNumber,
        s.SupplierName,
        s.Email ?? "",
        s.TaxOrganizationType?.DisplayName ?? "",
        s.SupplierType?.DisplayName ?? "",
        s.TaxpayerCountry?.DisplayName ?? "",
        s.CountryName?.DisplayName ?? "",
        s.TaxRegistrationNumber,
        s.AddressLine1 ?? "",
        s.AddressLine2 ?? "",
        s.City?.DisplayName ?? "",
        s.Country?.DisplayName ?? "",
        s.IsActive ? "Yes" : "No"
    ];

    public byte[] ExportToCsv(IEnumerable<Supplier> suppliers)
    {
        using var memoryStream = new MemoryStream();
        using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var column in CsvColumns)
            {
                csv.WriteField(column);
            }
            csv.NextRecord();

            foreach (var supplier in suppliers)
            {
                foreach (var field in ToCsvRow(supplier))
                {
                    csv.WriteField(field);
                }
                csv.NextRecord();
            }
        }

        return memoryStream.ToArray();
    }

    public byte[] ExportToPdf(IEnumerable<Supplier> suppliers)
    {
        var rows = suppliers.ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Text("Supplier Data Export").FontSize(16).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in PdfColumns)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in PdfColumns)
                        {
                            header.Cell().Element(CellStyleHeader).Text(column).Bold();
                        }
                    });

                    foreach (var supplier in rows)
                    {
                        foreach (var field in ToPdfRow(supplier))
                        {
                            table.Cell().Element(CellStyle).Text(field);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();

        static IContainer CellStyleHeader(IContainer container) =>
            container.Border(1).Background(Colors.Grey.Lighten3).Padding(3);

        static IContainer CellStyle(IContainer container) =>
            container.Border(1).Padding(3);
    }

    /// <summary>Combines supplier attachment images into a single PDF, one image per page (scaled to
    /// fit the page), with the file name as a caption.</summary>
    public byte[] ImagesToPdf(IReadOnlyList<(byte[] Bytes, string FileName)> images)
    {
        var document = Document.Create(container =>
        {
            foreach (var image in images)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.Header().Text(image.FileName).FontSize(10).Bold();
                    page.Content().PaddingVertical(8).AlignCenter().AlignMiddle().Image(image.Bytes).FitArea();
                });
            }
        });

        return document.GeneratePdf();
    }

    /// <summary>Embeds supplier attachment images into an Excel workbook, one image per worksheet.</summary>
    public byte[] ImagesToExcel(IReadOnlyList<(byte[] Bytes, string FileName)> images)
    {
        using var workbook = new XLWorkbook();
        var index = 0;
        foreach (var image in images)
        {
            index++;
            // Worksheet names are limited to 31 chars and can't contain : \ / ? * [ ]
            var safeName = new string(image.FileName.Where(c => !"[]:\\/?*".Contains(c)).ToArray());
            if (safeName.Length > 28) { safeName = safeName[..28]; }
            var sheet = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(safeName) ? $"Image {index}" : $"{index}. {safeName}"[..Math.Min(31, ($"{index}. {safeName}").Length)]);

            sheet.Cell(1, 1).Value = image.FileName;
            using var stream = new MemoryStream(image.Bytes);
            sheet.AddPicture(stream).MoveTo(sheet.Cell(3, 1));
        }

        if (index == 0)
        {
            workbook.Worksheets.Add("No Images");
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
