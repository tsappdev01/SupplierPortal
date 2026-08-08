using System.Text;
using UglyToad.PdfPig;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Reads the embedded text layer of a PDF locally (no cloud OCR). Returns null for image-only
/// scans that carry no text. Used as a fast path for Emirates ID extraction before falling back to OCR.</summary>
public static class LocalPdfText
{
    public static string? TryExtract(byte[] bytes)
    {
        try
        {
            using var doc = PdfDocument.Open(bytes);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            var text = sb.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
