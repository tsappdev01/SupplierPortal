using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UglyToad.PdfPig;
using ZXing;
using ZXing.Common;

namespace SupplierPortal.Web.Services;

/// <summary>Reads a QR code from an uploaded trade-license image or PDF and returns the URL it
/// encodes (the Dubai DED verification link). Best-effort: any failure - no QR, an unreadable image,
/// a parse error - returns null so the caller falls back to extracting from the uploaded file.</summary>
public class LicenseQrService
{
    private static readonly BarcodeReaderGeneric Reader = new()
    {
        Options = new DecodingOptions
        {
            PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE },
            TryHarder = true
        },
        AutoRotate = true
    };

    /// <summary>Returns the first http(s) URL encoded by a QR code in the file, or null.</summary>
    public string? TryReadQrUrl(string filePath)
    {
        try
        {
            if (string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var imageBytes in ExtractPdfImages(filePath))
                {
                    var url = DecodeUrl(imageBytes);
                    if (url is not null)
                    {
                        return url;
                    }
                }
                return null;
            }

            return DecodeUrl(File.ReadAllBytes(filePath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Extracts the embedded raw image bytes from each page of a PDF (DED scanned licenses
    /// store the whole page - QR included - as one JPEG), best-effort.</summary>
    private static IEnumerable<byte[]> ExtractPdfImages(string filePath)
    {
        var images = new List<byte[]>();
        try
        {
            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                foreach (var image in page.GetImages())
                {
                    try { images.Add(image.RawBytes.ToArray()); }
                    catch { /* skip an image we can't read */ }
                }
            }
        }
        catch { /* not a readable PDF; nothing to offer */ }
        return images;
    }

    private static string? DecodeUrl(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            var pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels);
            var source = new RGBLuminanceSource(
                pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
            var result = Reader.Decode(source);
            var text = result?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            return text.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text : null;
        }
        catch
        {
            return null;
        }
    }
}
