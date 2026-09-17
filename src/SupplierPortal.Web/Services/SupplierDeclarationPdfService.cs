using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

/// <summary>Renders the signed supplier declaration as a single PDF: the supplier's full details, the
/// declaration text they acknowledged, and their typed + hand-drawn signature. Produced on submission and
/// stored with the supplier's documents (so it appears in the documents list and "download all" zip).</summary>
public class SupplierDeclarationPdfService
{
    private static readonly string Navy = Colors.Blue.Darken4;

    public byte[] Build(Supplier s, byte[]? signatureImage)
    {
        string V(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v.Trim();
        string D(DateTime? d) => d?.ToString("dd MMM yyyy") ?? "—";
        string YN(bool? b) => b is null ? "—" : (b.Value ? "Yes" : "No");

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
                    h.Item().Text("Supplier Declaration — Supplier Data Gathering Portal").FontSize(9).FontColor(Colors.Grey.Darken1);
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
                        col.Item().Column(c =>
                        {
                            c.Item().PaddingBottom(3).Text(title).FontSize(11).Bold().FontColor(Navy);
                            c.Item().Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.ConstantColumn(165); cd.RelativeColumn(); });
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

                    Section("Business / Organization",
                        ("Supplier Number", V(s.SupplierNumber)),
                        ("Supplier Name (Legal Name)", V(s.SupplierName)),
                        ("Tax Organization Type", V(s.TaxOrganizationType?.DisplayName)),
                        ("Supplier Type", V(s.SupplierType?.DisplayName)),
                        ("Taxpayer Country", V(s.TaxpayerCountry?.DisplayName)),
                        ("Trade License Number", V(s.TradeLicenseNumber)),
                        ("Trade License Issue Date", D(s.TradeLicenseIssueDate)),
                        ("Trade License Expiry Date", D(s.TradeLicenseExpiryDate)),
                        ("Trade License Issuing Body", V(s.TradeLicenseIssuingBody?.DisplayName ?? s.IssuingBodyIfOthers)),
                        ("VAT Registered", YN(s.VatRegistered)),
                        ("Tax Registration Number (TRN)", V(s.TaxRegistrationNumber)),
                        ("VAT Registration Type", V(s.VatRegistrationType?.DisplayName)),
                        ("Supplier TIN", V(s.SupplierTin)),
                        ("Business Partner Type", V(s.BusinessPartnerType?.DisplayName)),
                        ("e-Invoicing Wave Applicability", V(s.EInvoicingWave?.DisplayName)),
                        ("D-U-N-S Number", V(s.DunsNumber)),
                        ("Corporate Web Site", V(s.CorporateWebSite)));

                    Section("Address (As per Trade License)",
                        ("Address Name", V(s.AddressName)),
                        ("Address Line 1", V(s.AddressLine1)),
                        ("Address Line 2", V(s.AddressLine2)),
                        ("City / Emirate", V(s.City?.DisplayName ?? s.CityText)),
                        ("Country", V(s.Country?.DisplayName)),
                        ("PO Box / Postal Code", V(s.PostalCode)),
                        ("State", V(s.State)));

                    Section("Contact Information",
                        ("Name", V($"{s.Prefix?.DisplayName} {s.FirstName} {s.MiddleName} {s.LastName}".Trim())),
                        ("Administrative Contact", V(s.AdministrativeContact)),
                        ("E-Mail", V(s.ContactEmail)),
                        ("Phone", V($"{s.PhoneAreaCode} {s.Phone}".Trim())),
                        ("Mobile", V($"{s.MobileAreaCode} {s.Mobile}".Trim())));

                    // Note: the "Site / Business Unit" and "Payment / Banking" sections are intentionally
                    // omitted from the signed declaration document (internal/banking details are not part of
                    // the supplier's declaration).

                    // Declaration text acknowledged by the supplier.
                    col.Item().PaddingTop(4).Column(c =>
                    {
                        c.Item().PaddingBottom(3).Text("Declaration").FontSize(11).Bold().FontColor(Navy);
                        foreach (var para in (s.DisclaimerText ?? "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                        {
                            c.Item().PaddingBottom(4).Text(para.Trim()).FontSize(9);
                        }
                    });

                    // Signature block.
                    col.Item().PaddingTop(6).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Signed by").FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Text(V(s.DisclaimerSignature)).FontSize(12).Bold();
                            c.Item().PaddingTop(4).Text($"Date: {D(s.DisclaimerAcknowledgedAt?.ToLocalTime() ?? s.DisclaimerAcknowledgedAt)}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"Acknowledged: {(s.DisclaimerAcknowledged ? "Yes" : "No")}").FontSize(8.5f).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(230).Column(c =>
                        {
                            c.Item().Text("Signature").FontSize(8).FontColor(Colors.Grey.Darken1);
                            if (signatureImage is { Length: > 0 })
                            {
                                c.Item().Height(70).Image(signatureImage).FitArea();
                            }
                            else
                            {
                                c.Item().Height(70).AlignBottom().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                            }
                        });
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
