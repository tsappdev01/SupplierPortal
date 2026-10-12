namespace SupplierPortal.Web.Services;

/// <summary>Extracts a plain-text layer from a document that has none of its own (a scanned/image
/// PDF) via a cloud OCR service. Implementations are best-effort: any failure - not configured, a
/// network error, an unsupported file - returns null so callers fall back to "nothing extracted".</summary>
public interface IDocumentOcrService
{
    /// <summary>True when OCR is configured and a call will be attempted.</summary>
    bool IsEnabled { get; }

    /// <summary>Runs OCR over the file and returns the concatenated text, or null if OCR is disabled
    /// or fails for any reason.</summary>
    Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
}
