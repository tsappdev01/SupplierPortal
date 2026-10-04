namespace SupplierPortal.Web.Services.Storage;

/// <summary>Durable storage for user-uploaded files (documents, profile photos, logos). The Azure Blob
/// implementation is used when configured; otherwise <see cref="Enabled"/> is false and callers keep
/// their existing local-disk / database storage. Reads are addressed by an opaque blob key that callers
/// persist alongside the file's metadata.</summary>
public interface IFileStorageService
{
    /// <summary>True when a blob backend is configured and reachable, so new uploads should be stored in it.</summary>
    bool Enabled { get; }

    /// <summary>Uploads bytes and returns the blob key to persist, or null when blob storage is disabled
    /// (the caller then falls back to its previous storage).</summary>
    Task<string?> SaveAsync(string keyPrefix, string fileName, byte[] bytes, string contentType);

    /// <summary>Uploads bytes at a caller-chosen, human-readable key (e.g. "SupplierNo_Name/Trade License _ x.pdf").
    /// When <paramref name="overwrite"/> is true the key is written as-is (replacing any existing blob);
    /// when false a duplicate key is disambiguated with a " (2)", " (3)" … suffix so nothing is overwritten.
    /// Returns the actual key stored, or null when blob storage is disabled (the caller falls back to the DB).</summary>
    Task<string?> SaveAtKeyAsync(string desiredKey, byte[] bytes, string contentType, bool overwrite);

    /// <summary>Reads the bytes for a previously saved blob key, or null if disabled/not found.</summary>
    Task<byte[]?> ReadAsync(string? blobKey);

    /// <summary>Deletes a blob by key (no-op when disabled or the key is blank).</summary>
    Task DeleteAsync(string? blobKey);
}
