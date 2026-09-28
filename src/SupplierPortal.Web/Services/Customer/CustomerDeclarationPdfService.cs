using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Renders the signed customer declaration as a single PDF: the customer's key details, the
/// declaration text they acknowledged, and their typed + hand-drawn signature. Produced on self-service
/// submission and stored with the customer's documents. Mirrors <see cref="SupplierDeclarationPdfService"/>.</summary>
public class CustomerDeclarationPdfService
{
    private static readonly string Navy = Colors.Blue.Darken4;

    public byte[] Build(Customer c, byte[]? signatureImage)
    {
        string V(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v.Trim();
        string D(DateTime? d) => d?.ToString("dd MMM yyyy") ?? "—";

        var contact = c.Contact;
        var contactName = contact is null
            ? "—"
            : V(string.Join(" ", new[] { contact.FirstName, contact.MiddleName, contact.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p))));

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Colors.Grey.Darken4));

                page.Header().Column(h =>
                {
                    h.Item().Text("Dubai Investments").FontSize(15).Bold().FontColor(Navy);
                    h.Item().Text("Customer Declaration — Customer Data Gathering Portal").FontSize(9).FontColor(Colors.Grey.Darken1);
                    h.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span(DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'")).FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.Span("     Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(8);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(10);

                    void Section(string title, params (string Label, string Value)[] rows)
                    {
                        col.Item().Column(cc =>
                        {
                            cc.Item().PaddingBottom(3).Text(title).FontSize(11).Bold().FontColor(Navy);
                            cc.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.ConstantColumn(180); cd.RelativeColumn(); });
                                var i = 0;
                                foreach (var (label, value) in rows)
                                {
                                    var bg = i++ % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;
                                    t.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(6).Text(label).FontColor(Colors.Grey.Darken2);
                                    t.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(6).Text(value).SemiBold();
                                }
                            });
                        });
                    }

                    Section("Customer / Account",
                        ("Customer Number", V(c.CustomerCode ?? c.CustomerId ?? c.BaanRef)),
                        ("Legal Name", V(c.LegalName)),
                        ("Commercial Name", V(c.CommercialName)),
                        ("Type of Customer", V(c.TypeOfCustomer)),
                        ("VAT Structure", V(c.VATStructure)),
                        ("TRN Number", V(c.TRNNumber)),
                        ("Trade License Number", V(c.TradeLicenseNumber)),
                        ("Trade License Issue Date", D(c.TradeLicenseIssueDate)),
                        ("Trade License Expiry Date", D(c.TradeLicenseExpiryDate)),
                        ("Licensing Authority", V(c.LicensingAuthorityName)),
                        ("Incorporation Country", V(c.IncorporationCountry)),
                        ("Establishment Date", D(c.EstablishmentDate)));

                    Section("Primary Contact",
                        ("Name", contactName),
                        ("E-Mail", V(contact?.Email)),
                        ("Mobile", V(contact?.MobileNumber)));

                    // Declaration text acknowledged by the customer.
                    col.Item().PaddingTop(4).Column(cc =>
                    {
                        cc.Item().PaddingBottom(3).Text("Declaration").FontSize(11).Bold().FontColor(Navy);
                        foreach (var para in (c.DisclaimerText ?? "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                        {
                            cc.Item().PaddingBottom(4).Text(para.Trim()).FontSize(9);
                        }
                    });

                    // Signature block.
                    col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(cc =>
                        {
                            cc.Item().Text("Signed by").FontSize(8).FontColor(Colors.Grey.Darken1);
                            cc.Item().Text(V(c.DisclaimerSignature)).FontSize(12).Bold();
                            cc.Item().PaddingTop(4).Text($"Date: {D(c.DisclaimerAcknowledgedAt?.ToLocalTime() ?? c.DisclaimerAcknowledgedAt)}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            cc.Item().Text($"Acknowledged: {(c.DisclaimerAcknowledged ? "Yes" : "No")}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(230).Column(cc =>
                        {
                            cc.Item().Text("Signature").FontSize(8).FontColor(Colors.Grey.Darken1);
                            if (signatureImage is { Length: > 0 })
                            {
                                cc.Item().Height(70).Image(signatureImage).FitArea();
                            }
                            else
                            {
                                cc.Item().Height(70).AlignBottom().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                            }
                        });
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
