using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>
/// Renders a single-file PDF summary of a landlord registration — property information, all contacts,
/// authorised signatories and the attached documents — produced on submission. The PDF is stored against
/// the registration and is the document later routed to the authorised signatories (e.g. via Circularo) for
/// e-signature.
/// </summary>
public class LandlordSummaryPdfService
{
    private static readonly string Navy = Colors.Blue.Darken4;

    /// <summary>Builds the registration-summary PDF. <paramref name="documents"/> is the list of attached
    /// files (metadata only) to enumerate — the generated summary itself should be excluded by the caller.</summary>
    public byte[] Build(Landlord l, IReadOnlyList<LandlordDocument> documents)
    {
        var primary = l.Contacts.FirstOrDefault(c => c.IsPrimary);
        var secondaries = l.Contacts.Where(c => !c.IsPrimary).ToList();
        var signatories = l.Signatories.OrderBy(s => s.Order).ToList();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(h =>
                {
                    h.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Dubai Investments").FontSize(15).Bold().FontColor(Navy);
                            c.Item().Text("Business Centre · Leasing").FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(170).AlignRight().Column(c =>
                        {
                            c.Item().Text("Landlord Registration Summary").FontSize(11).Bold().FontColor(Navy);
                            c.Item().Text(l.Reference ?? "(pending reference)").FontSize(9).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);

                    // ----- Property information -----
                    Section(col, "Property Information");
                    col.Item().Table(t =>
                    {
                        DefineKv(t);
                        Kv(t, "Property Code", l.PropertyCode);
                        Kv(t, "Company", l.CompanyName ?? "—");
                        Kv(t, "Building", l.Building ?? "—");
                        Kv(t, "Agreement Type", l.Type.ToString());
                        Kv(t, "Invitation Email", l.Email);
                        if (!string.IsNullOrWhiteSpace(l.LandLordCode)) Kv(t, "Landlord Code", l.LandLordCode!);
                    });

                    // ----- Primary contact -----
                    Section(col, "Primary Contact");
                    col.Item().Table(t =>
                    {
                        DefineKv(t);
                        Kv(t, "Name", primary?.FullName ?? "—");
                        Kv(t, "Designation", primary?.Designation ?? "—");
                        Kv(t, "Contact Type", primary?.ContactType.ToString() ?? "—");
                        Kv(t, "Email", primary?.Email ?? "—");
                        Kv(t, "Mobile", primary?.Mobile ?? "—");
                        if (l.Type == LandlordType.Individual)
                        {
                            Kv(t, "Emirates ID", string.IsNullOrWhiteSpace(primary?.EmiratesId) ? "—" : primary!.EmiratesId!);
                            Kv(t, "Emirates ID Expiry", primary?.EmiratesIdExpiry is { } ex ? ex.ToString("dd MMM yyyy") : "—");
                        }
                    });

                    // ----- Secondary contacts -----
                    Section(col, $"Secondary Contacts ({secondaries.Count})");
                    if (secondaries.Count == 0)
                        col.Item().Text("None provided.").Italic().FontColor(Colors.Grey.Darken1);
                    else
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(3); c.RelativeColumn(2); });
                            THead(t, "Name", "Type", "Email", "Mobile");
                            foreach (var c in secondaries)
                            {
                                Cell(t, c.FullName);
                                Cell(t, c.ContactType.ToString());
                                Cell(t, c.Email);
                                Cell(t, c.Mobile ?? "—");
                            }
                        });

                    // ----- Authorised signatories -----
                    Section(col, $"Authorised Signatories ({signatories.Count})  ·  {(l.SigningRule == SigningRule.All ? "All must sign" : "Any one may sign")}");
                    if (signatories.Count == 0)
                        col.Item().Text("None provided.").Italic().FontColor(Colors.Grey.Darken1);
                    else
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.ConstantColumn(24); c.RelativeColumn(3); c.RelativeColumn(4); c.RelativeColumn(2); });
                            THead(t, "#", "Name", "Email", "Mobile");
                            var n = 1;
                            foreach (var s in signatories)
                            {
                                Cell(t, (n++).ToString());
                                Cell(t, s.Name + (s.IsPrimaryContact ? "  (Primary)" : ""));
                                Cell(t, s.Email);
                                Cell(t, s.Mobile ?? "—");
                            }
                        });

                    // ----- Documents attached -----
                    Section(col, $"Documents Attached ({documents.Count})");
                    if (documents.Count == 0)
                        col.Item().Text("None attached.").Italic().FontColor(Colors.Grey.Darken1);
                    else
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(3); });
                            THead(t, "Type", "File", "Size", "Uploaded");
                            foreach (var d in documents)
                            {
                                Cell(t, DocLabel(d.DocumentType));
                                Cell(t, d.FileName);
                                Cell(t, FormatSize(d.FileSizeBytes));
                                Cell(t, d.UploadedAt.ToString("dd MMM yyyy HH:mm"));
                            }
                        });

                    col.Item().PaddingTop(8).Text(txt =>
                    {
                        txt.Span("Declaration:  ").Bold();
                        txt.Span("The landlord confirmed that the information above — contacts, documents and signatories — is accurate and complete at the time of submission.");
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("Dubai Investments Business Centre · Landlord Registration  ·  ").FontSize(8).FontColor(Colors.Grey.Medium);
                    txt.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    txt.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    txt.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title) =>
        col.Item().PaddingBottom(2).Text(title).FontSize(11).Bold().FontColor(Navy);

    private static void DefineKv(TableDescriptor t) =>
        t.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });

    private static void Kv(TableDescriptor t, string k, string v)
    {
        t.Cell().PaddingVertical(2).Text(k).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(2).Text(v);
    }

    private static void THead(TableDescriptor t, params string[] headers)
    {
        t.Header(h =>
        {
            foreach (var col in headers)
                h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(col).Bold().FontSize(9);
        });
    }

    private static void Cell(TableDescriptor t, string value) =>
        t.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(value).FontSize(9);

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };

    private static string DocLabel(LandlordDocumentType t) => t switch
    {
        LandlordDocumentType.EmiratesId => "Emirates ID",
        LandlordDocumentType.TradeLicense => "Trade Licence",
        LandlordDocumentType.Moa => "MOA",
        LandlordDocumentType.MoaOrPoa => "MOA / POA",
        LandlordDocumentType.PowerOfAttorney => "POA",
        LandlordDocumentType.AuthorisationLetter => "Authorisation Letter",
        LandlordDocumentType.TitleDeed => "Title Deed",
        LandlordDocumentType.RegistrationSummary => "Registration Summary",
        _ => "Other"
    };
}
