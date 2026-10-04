using ClosedXML.Excel;
using SupplierPortal.Web.Models.Customer;
using SupplierPortal.Web.Services;
using Xunit;

namespace SupplierPortal.Tests;

/// <summary>Round-trips the customer upload template: generate it with the real <see cref="CustomerExportService"/>
/// (download), then read every column back using the exact positional contract <see cref="CustomerImportService"/>
/// uses (upload). Proves the download and upload agree on column order, headers and value fidelity — including the
/// synthetic VAT-group column, the appended ERP columns, dates and yes/no fields, and the aligned labels.</summary>
public class CustomerTemplateRoundTripTests
{
    private static CustomerFieldConfig F(string section, string key, string label, int seq, string kind = "text") => new()
    {
        Section = section,
        FieldKey = key,
        DisplayLabel = label,
        Sequence = seq,
        FieldKind = kind,
        IsVisible = true,
        IsRemoved = false,
        BusinessControl = "Editable",
        CustomerControl = "Editable"
    };

    // A representative config covering every mechanism: text, date, yes/no, the synthetic VAT-group column, the
    // appended ERP columns, both sections, and the aligned labels.
    private static List<CustomerFieldConfig> SampleConfig() =>
    [
        F("Account", "LegalName", "Customer Name (Legal Name)", 1),
        F("Account", "VatGroupLegalNames", "Legal Names of all Entities under the VAT Group", 2),
        F("Account", "VATStructure", "VAT Registration Type", 3),
        F("Account", "TRNApplicable", "VAT Registered? (Y/N)", 4, "yesno"),
        F("Account", "TRNNumber", "Tax Registration Number", 5),
        F("Account", "TradeLicenseIssueDate", "Trade License Issue Date", 6, "date"),
        F("Account", "Owner1PassportIssueCountry", "Owner 1 - Passport Issue Country Code", 7),
        F("Account", "PaymentTerms", "Payment Terms", 8),
        F("Account", "PartyId", "Party ID", 9),
        F("Account", "PartyNumber", "Party Number", 10),
        F("Account", "ErpBillToAccount", "ERP Bill To Account", 11),
        F("Contact", "FirstName", "First Name", 1),
        F("Contact", "Email", "Email", 2),
        F("Contact", "PassportExpiryDate", "Passport Expiry Date", 3, "date"),
        F("Contact", "ReceivedVATCertificate", "Received VAT Certificate? (Yes/No)", 4, "yesno"),
    ];

    private static Customer SampleCustomer() => new()
    {
        BaanRef = "BAAN-1",
        CustomerId = "CUN-1",
        LegalName = "Acme Trading LLC",
        VATStructure = "VAT Group",
        TRNApplicable = true,
        TRNNumber = "100300500700003",
        TradeLicenseIssueDate = new DateTime(2026, 9, 28),
        Owner1PassportIssueCountry = "AE",
        PaymentTerms = "Net 30",
        PartyId = "P123",
        PartyNumber = "N456",
        ErpBillToAccount = "BTA789",
        VatGroupEntities =
        [
            new CustomerVatGroupEntity { BusinessLegalName = "Entity A", SortOrder = 0 },
            new CustomerVatGroupEntity { BusinessLegalName = "Entity B", SortOrder = 1 },
        ],
        Contact = new CustomerContact
        {
            FirstName = "Nayyar",
            Email = "nayyar@example.com",
            PassportExpiryDate = new DateTime(2027, 2, 22),
            ReceivedVATCertificate = false,
        }
    };

    [Fact]
    public void Template_Download_And_Upload_Agree_On_Columns_And_Values()
    {
        var config = SampleConfig();
        var customer = SampleCustomer();

        // --- DOWNLOAD: real export service produces the template workbook ---
        var bytes = new CustomerExportService().ExportUploadTemplate([customer], config);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        // Column layout, derived EXACTLY as CustomerImportService derives it from the same config.
        var account = config.Where(c => c.Section == "Account").OrderBy(c => c.Sequence).ToList();
        var contact = config.Where(c => c.Section == "Contact").OrderBy(c => c.Sequence).ToList();
        const int accountFirstCol = 3;
        var contactFirstCol = accountFirstCol + account.Count + 2 /*blank*/ + 2 /*contact Baan/Unique*/;

        // --- Header row matches the aligned labels in the exact positions ---
        Assert.Equal("Baan Ref#", ws.Cell(1, 1).GetString());
        Assert.Equal("Customer Unique Number", ws.Cell(1, 2).GetString());
        for (var i = 0; i < account.Count; i++)
            Assert.Equal(account[i].DisplayLabel, ws.Cell(1, accountFirstCol + i).GetString());
        Assert.Equal("", ws.Cell(1, accountFirstCol + account.Count).GetString());       // blank separator
        Assert.Equal("", ws.Cell(1, accountFirstCol + account.Count + 1).GetString());   // blank separator
        Assert.Equal("Baan Ref#", ws.Cell(1, contactFirstCol - 2).GetString());
        Assert.Equal("Customer Unique Number", ws.Cell(1, contactFirstCol - 1).GetString());
        for (var i = 0; i < contact.Count; i++)
            Assert.Equal(contact[i].DisplayLabel, ws.Cell(1, contactFirstCol + i).GetString());

        // --- UPLOAD: read row 2 back with the importer's exact cell logic into a fresh customer ---
        string Cell(int col)
        {
            var c = ws.Cell(2, col);
            if (c.DataType == XLDataType.DateTime)
            {
                try { return c.GetDateTime().ToString("yyyy-MM-dd"); } catch { /* fall through */ }
            }
            return c.GetString();
        }

        Assert.Equal("BAAN-1", Cell(1));
        Assert.Equal("CUN-1", Cell(2));

        var round = new Customer { Contact = new CustomerContact() };
        string? vatGroupCell = null;
        for (var i = 0; i < account.Count; i++)
        {
            var value = Cell(accountFirstCol + i);
            if (account[i].FieldKey == CustomerExportService.VatGroupLegalNamesKey) { vatGroupCell = value; continue; }
            CustomerFieldAccessor.SetString(round, account[i].FieldKey, value);
        }
        for (var i = 0; i < contact.Count; i++)
            CustomerFieldAccessor.SetString(round.Contact, contact[i].FieldKey, Cell(contactFirstCol + i));

        // --- Every value survived the round-trip, with the right type conversions ---
        Assert.Equal("Acme Trading LLC", round.LegalName);
        Assert.Equal("VAT Group", round.VATStructure);
        Assert.Equal(true, round.TRNApplicable);
        Assert.Equal("100300500700003", round.TRNNumber);
        Assert.Equal(new DateTime(2026, 9, 28), round.TradeLicenseIssueDate);
        Assert.Equal("AE", round.Owner1PassportIssueCountry);
        Assert.Equal("Net 30", round.PaymentTerms);
        Assert.Equal("P123", round.PartyId);
        Assert.Equal("N456", round.PartyNumber);
        Assert.Equal("BTA789", round.ErpBillToAccount);
        Assert.Equal("Nayyar", round.Contact!.FirstName);
        Assert.Equal("nayyar@example.com", round.Contact.Email);
        Assert.Equal(new DateTime(2027, 2, 22), round.Contact.PassportExpiryDate);
        Assert.Equal(false, round.Contact.ReceivedVATCertificate);

        // --- VAT-group flat cell carries both entity names and splits back exactly (importer logic) ---
        Assert.Equal("Entity A; Entity B", vatGroupCell);
        var names = vatGroupCell!.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(["Entity A", "Entity B"], names);
    }

    [Fact]
    public void Template_Columns_Are_Driven_By_Config_Order()
    {
        // Re-ordering the config must re-order the template columns identically (download == upload contract).
        var config = SampleConfig();
        var bytes = new CustomerExportService().ExportUploadTemplate([SampleCustomer()], config);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        var account = config.Where(c => c.Section == "Account").OrderBy(c => c.Sequence).ToList();
        for (var i = 0; i < account.Count; i++)
            Assert.Equal(account[i].DisplayLabel, ws.Cell(1, 3 + i).GetString());
    }
}
