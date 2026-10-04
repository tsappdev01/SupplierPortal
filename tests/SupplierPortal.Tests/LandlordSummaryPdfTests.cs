using QuestPDF.Infrastructure;
using SupplierPortal.Web.Models.Leasing;
using SupplierPortal.Web.Services.Leasing;
using Xunit;

namespace SupplierPortal.Tests;

public class LandlordSummaryPdfTests
{
    private static Landlord SampleLandlord() => new()
    {
        Reference = "DIPLR2026100020",
        Type = LandlordType.Individual,
        PropertyCode = "DIP-123",
        CompanyName = "Acme Holdings LLC",
        Building = "Building 7",
        Email = "owner@example.com",
        SigningRule = SigningRule.All,
        Contacts =
        {
            new LandlordContact { IsPrimary = true, ContactType = ContactType.Owner, FullName = "Subhah Nayyar Khan",
                Designation = "Owner", Email = "subhah@example.com", Mobile = "+971500000000",
                EmiratesId = "784-2008-2052928-5", EmiratesIdExpiry = new DateTime(2028, 7, 23) },
        },
        Signatories =
        {
            new LandlordSignatory { Order = 1, Name = "Subhah Nayyar Khan", Email = "subhah@example.com", IsPrimaryContact = true },
        }
    };

    // Guards the QuestPDF Foreground/pinned-signature layout against runtime layout exceptions
    // (these do not surface at compile time) and confirms a summary is always produced.
    [Fact]
    public void Build_produces_a_pdf_without_layout_errors()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var pdf = new LandlordSummaryPdfService().Build(SampleLandlord(), new List<LandlordDocument>());
        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 1000, "Expected a non-trivial PDF to be generated.");
        // %PDF header
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    // The printed "Sign here" box geometry (LandlordSummaryPdfService) and the Circularo signature-field
    // placement (CircularoOptions.SignPercent*) must stay in agreement, or the e-signature field drifts off
    // the box. A4 = 595.28 x 841.89 pt; box 220x70 pt, 28 pt from the right, 46 pt above the bottom.
    [Fact]
    public void Circularo_signature_percentages_match_the_printed_box()
    {
        const double pageW = 595.28, pageH = 841.89;
        const double boxW = 220, boxH = 70, right = 28, bottom = 46;

        var expectedX = (pageW - right - boxW) / pageW;
        var expectedY = (pageH - bottom - boxH) / pageH;
        var expectedW = boxW / pageW;
        var expectedH = boxH / pageH;

        var o = new CircularoOptions();
        Assert.Equal(expectedX, o.SignPercentX, 2);
        Assert.Equal(expectedY, o.SignPercentY, 2);
        Assert.Equal(expectedW, o.SignPercentWidth, 2);
        Assert.Equal(expectedH, o.SignPercentHeight, 2);
    }
}
