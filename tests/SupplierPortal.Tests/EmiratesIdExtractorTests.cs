using SupplierPortal.Web.Services.Leasing;
using Xunit;

namespace SupplierPortal.Tests;

public class EmiratesIdExtractorTests
{
    // OCR reading order of the sample card (English column), lines separated by \n.
    private const string SampleCard =
        "United Arab Emirates\nResident Identity Card\nID Number / رقم الهوية\n784-2008-2052928-5\n" +
        "الاسم: سبحه ناير خان\nName: Subhah Nayyar Khan\nDate of Birth : 18/01/2008\n" +
        "Nationality: Pakistan\nIssuing Date / تاريخ الاصدار\n24/07/2026\nExpiry Date / تاريخ الانتهاء\n23/07/2028";

    [Fact]
    public void Parses_number_name_and_expiry_from_sample_card()
    {
        var r = EmiratesIdExtractor.Parse(SampleCard);
        Assert.Equal("784-2008-2052928-5", r.Number);
        Assert.Equal("Subhah Nayyar Khan", r.Name);          // must NOT bleed into "Date of Birth"
        Assert.Equal("23/07/2028", r.Expiry);                 // latest / expiry date, not DOB or issue date
    }

    [Theory]
    [InlineData("ID: 784200820529285")]                       // 15 digits, no separators
    [InlineData("784 2008 2052928 5")]                        // space separated
    [InlineData("784-2008-2052928-5")]                        // dashed
    public void Normalises_id_number_to_dashed_form(string text)
    {
        Assert.Equal("784-2008-2052928-5", EmiratesIdExtractor.Parse(text).Number);
    }

    [Fact]
    public void Name_capture_trims_trailing_field_labels_when_lines_merge()
    {
        // If OCR flattens the name and next label onto one line, trailing label words are trimmed.
        var r = EmiratesIdExtractor.Parse("Name: Subhah Nayyar Khan Date of Birth 18/01/2008");
        Assert.Equal("Subhah Nayyar Khan", r.Name);
    }

    [Fact]
    public void Returns_nulls_for_empty_or_unrelated_text()
    {
        var r = EmiratesIdExtractor.Parse("no id here");
        Assert.Null(r.Number);
        var empty = EmiratesIdExtractor.Parse("");
        Assert.Null(empty.Number);
        Assert.Null(empty.Name);
        Assert.Null(empty.Expiry);
    }

    [Fact]
    public void Prefers_expiry_labelled_date_over_others()
    {
        var r = EmiratesIdExtractor.Parse("Issuing Date 01/01/2020 Expiry Date 05/06/2030");
        Assert.Equal("05/06/2030", r.Expiry);
    }
}
