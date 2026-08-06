namespace SupplierPortal.Web.Services;

/// <summary>
/// Cloud OCR configuration, bound from the <c>Ocr</c> config section. Used to read scanned/image
/// trade-license PDFs that carry no embedded text layer. The real endpoint/key live only in the
/// gitignored <c>appsettings.Local.json</c> (or a secret store), never in committed appsettings.
/// </summary>
public class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>OCR provider. "Azure" enables Azure AI Document Intelligence; empty/unknown disables OCR.</summary>
    public string Provider { get; set; } = "";

    /// <summary>Azure Document Intelligence endpoint, e.g. https://myresource.cognitiveservices.azure.com.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Azure Document Intelligence API key (sent as the Ocp-Apim-Subscription-Key header).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Prebuilt model to run. "prebuilt-read" is plain OCR (text only).</summary>
    public string ModelId { get; set; } = "prebuilt-read";

    /// <summary>Service API version.</summary>
    public string ApiVersion { get; set; } = "2024-11-30";

    /// <summary>Max seconds to poll for the async analyze result before giving up.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>True only when a usable Azure configuration is present; otherwise OCR is skipped.</summary>
    public bool IsEnabled =>
        string.Equals(Provider, "Azure", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey);
}
