using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Builds the Manage Customer exports (Excel, Excel-all-fields, PDF and the upload template),
/// mirroring the supplier <see cref="ExportService"/>. Columns are driven by <see cref="CustomerFieldConfig"/>
/// so they always match the wizard, and values are read through <see cref="CustomerFieldAccessor"/>.</summary>
public class CustomerExportService
{
    /// <summary>Excel of the customers, one row each, with the <b>visible</b> account then contact fields
    /// (plus Customer Number and Status). "All fields" includes hidden fields too.</summary>
    public byte[] ExportToExcel(IReadOnlyList<Customer> customers, IReadOnlyList<CustomerFieldConfig> config, bool allFields)
    {
        var account = FieldsFor(config, "Account", allFields);
        var contact = FieldsFor(config, "Contact", allFields);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Customers");

        var headers = new List<string> { "Customer Number", "Status" };
        headers.AddRange(account.Select(f => f.DisplayLabel));
        headers.AddRange(contact.Select(f => f.DisplayLabel));

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2233");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var cust in customers)
        {
            var col = 1;
            ws.Cell(row, col++).Value = cust.CustomerId ?? $"#{cust.Id}";
            ws.Cell(row, col++).Value = cust.Status;
            foreach (var f in account)
                ws.Cell(row, col++).Value = f.FieldKey == VatGroupLegalNamesKey ? VatGroupLegalNames(cust) : Display(cust, f.FieldKey);
            var contactEntity = (object?)cust.Contact ?? new CustomerContact();
            foreach (var f in contact)
                ws.Cell(row, col++).Value = Display(contactEntity, f.FieldKey);
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>The "Customer Data Template" workbook in the exact positional layout the importer reads:
    /// col 1 Baan Ref#, col 2 Customer Unique Number, then the account fields, two blank columns, then a
    /// second Baan Ref#/Customer Unique Number pair, then the contact fields. When <paramref name="customers"/>
    /// is non-empty the rows are pre-filled, otherwise a single blank example row is written.</summary>
    public byte[] ExportUploadTemplate(IReadOnlyList<Customer> customers, IReadOnlyList<CustomerFieldConfig> config)
    {
        // Visible fields only, so the template's columns match the wizard and the importer (which reads
        // the same visible set positionally) exactly.
        var account = FieldsFor(config, "Account", includeHidden: false);
        var contact = FieldsFor(config, "Contact", includeHidden: false);

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Customer Data Template");

        // Header row mirrors CustomerImportService's positional column map.
        var headers = new List<string> { "Baan Ref#", "Customer Unique Number" };
        headers.AddRange(account.Select(f => f.DisplayLabel));
        headers.Add(string.Empty);
        headers.Add(string.Empty);
        headers.Add("Baan Ref#");
        headers.Add("Customer Unique Number");
        headers.AddRange(contact.Select(f => f.DisplayLabel));

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a2233");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var accountFirstCol = 3;                                   // after Baan + Unique
        var contactFirstCol = accountFirstCol + account.Count + 2 + 2; // + 2 blank + 2 (Baan/Unique)

        var row = 2;
        foreach (var cust in customers)
        {
            ws.Cell(row, 1).Value = cust.BaanRef ?? string.Empty;
            ws.Cell(row, 2).Value = cust.CustomerId ?? string.Empty;
            // Raw values (not the "—"/Yes/No display form) so a populated template re-imports cleanly.
            for (var i = 0; i < account.Count; i++)
                ws.Cell(row, accountFirstCol + i).Value = account[i].FieldKey == VatGroupLegalNamesKey
                    ? VatGroupLegalNames(cust)
                    : CustomerFieldAccessor.GetString(cust, account[i].FieldKey) ?? string.Empty;

            ws.Cell(row, contactFirstCol - 2).Value = cust.BaanRef ?? string.Empty;
            ws.Cell(row, contactFirstCol - 1).Value = cust.CustomerId ?? string.Empty;
            var contactEntity = (object?)cust.Contact ?? new CustomerContact();
            for (var i = 0; i < contact.Count; i++)
                ws.Cell(row, contactFirstCol + i).Value = CustomerFieldAccessor.GetString(contactEntity, contact[i].FieldKey) ?? string.Empty;
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>A compact PDF listing of the customers (Number, Legal Name, Primary Email, Status).</summary>
    public byte[] ExportToPdf(IReadOnlyList<Customer> customers)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9));

                page.Header().PaddingBottom(8).Column(col =>
                {
                    col.Item().Text("Customer Register").FontSize(15).Bold().FontColor("#1a2233");
                    col.Item().Text($"{customers.Count} customer(s) · generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor("#7b8697");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);   // Number
                        c.RelativeColumn(3);     // Legal name
                        c.RelativeColumn(3);     // Email
                        c.ConstantColumn(110);   // Status
                    });

                    void Head(string text) => table.Cell().Background("#1a2233").Padding(5)
                        .Text(text).FontColor(Colors.White).Bold();
                    Head("Customer #");
                    Head("Legal Name");
                    Head("Primary Email");
                    Head("Status");

                    var zebra = false;
                    foreach (var cust in customers)
                    {
                        var bg = zebra ? "#f4f6f9" : "#ffffff";
                        zebra = !zebra;
                        table.Cell().Background(bg).Padding(5).Text(cust.CustomerId ?? $"#{cust.Id}");
                        table.Cell().Background(bg).Padding(5).Text(cust.LegalName ?? "");
                        table.Cell().Background(bg).Padding(5).Text(cust.PrimaryEmail ?? cust.Contact?.Email ?? "");
                        table.Cell().Background(bg).Padding(5).Text(cust.Status);
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return doc.GeneratePdf();
    }

    /// <summary>Human-readable value for a data cell, with the accessor's "—" empty-placeholder blanked
    /// so empty cells stay empty in the spreadsheet.</summary>
    private static string Display(object entity, string key)
    {
        var v = CustomerFieldAccessor.GetDisplay(entity, key);
        return v == "—" ? string.Empty : v;
    }

    private static List<CustomerFieldConfig> FieldsFor(
        IReadOnlyList<CustomerFieldConfig> config, string section, bool includeHidden) =>
        config.Where(f => f.Section == section && (includeHidden || f.IsVisible))
              .OrderBy(f => f.Sequence)
              .ToList();

    /// <summary>Synthetic Account template column (sheet col 11): it has no scalar property — the value is the
    /// VAT-group entities' legal names joined into one cell (the interactive form uses the VAT Group grid).</summary>
    public const string VatGroupLegalNamesKey = "VatGroupLegalNames";

    /// <summary>The VAT-group entities' legal names, in order, joined for the single flat template cell.</summary>
    public static string VatGroupLegalNames(Customer cust) =>
        string.Join("; ", cust.VatGroupEntities
            .OrderBy(e => e.SortOrder)
            .Select(e => e.BusinessLegalName)
            .Where(n => !string.IsNullOrWhiteSpace(n)));
}
