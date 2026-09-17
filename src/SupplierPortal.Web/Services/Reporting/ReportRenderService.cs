using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SupplierPortal.Web.Services.Reporting;

/// <summary>Renders a report (a title, a header row and string data rows produced by
/// <see cref="ReportSchemaService"/>) to PDF or Excel with the exact chosen columns, in order.</summary>
public class ReportRenderService
{
    public byte[] RenderPdf(string title, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, bool truncated)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(headers.Count > 12 ? 7 : 8));

                page.Header().Column(col =>
                {
                    col.Item().Text(string.IsNullOrWhiteSpace(title) ? "Report" : title).FontSize(15).Bold();
                    col.Item().Text($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · {rows.Count} row(s)"
                        + (truncated ? " (truncated)" : "")).FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in headers) { columns.RelativeColumn(); }
                    });

                    table.Header(header =>
                    {
                        foreach (var h in headers)
                        {
                            header.Cell().Element(HeaderCell).Text(h).Bold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell().Element(BodyCell).Text(cell);
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

        static IContainer HeaderCell(IContainer c) => c.Border(1).Background(Colors.Grey.Lighten3).Padding(3);
        static IContainer BodyCell(IContainer c) => c.Border(1).Padding(3);
    }

    public byte[] RenderExcel(string title, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheetName = new string((title ?? "Report").Where(ch => !"[]:\\/?*".Contains(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(sheetName)) { sheetName = "Report"; }
        if (sheetName.Length > 31) { sheetName = sheetName[..31]; }
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var c = 0; c < headers.Count; c++)
        {
            var cell = sheet.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count && c < row.Length; c++)
            {
                // Assign as text so numbers/IDs keep their exact form.
                sheet.Cell(r, c + 1).Value = row[c];
            }
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        if (headers.Count > 0)
        {
            sheet.Range(1, 1, 1, headers.Count).SetAutoFilter();
            sheet.Columns(1, headers.Count).AdjustToContents(1, Math.Min(r - 1, 60));
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
