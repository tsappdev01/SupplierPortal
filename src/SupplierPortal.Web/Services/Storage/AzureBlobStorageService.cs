using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services.Storage;

/// <summary>Azure Blob Storage implementation of <see cref="IFileStorageService"/>. Registered as a
/// singleton; when no connection string is configured (or the account can't be reached at startup) it
/// stays disabled and callers keep their local-disk / database storage.</summary>
public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient? _container;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            return; // disabled — no connection string configured
        }

        try
        {
            var service = new BlobServiceClient(opts.ConnectionString);
            var containerName = string.IsNullOrWhiteSpace(opts.ContainerName) ? "userfiles" : opts.ContainerName.Trim();
            var container = service.GetBlobContainerClient(containerName);
            container.CreateIfNotExists(PublicAccessType.None);
            _container = container;
            _logger.LogInformation("Blob storage enabled: container '{Container}'.", containerName);
        }
        catch (Exception ex)
        {
            _container = null;
            _logger.LogError(ex, "Blob storage initialisation failed; falling back to local storage.");
        }
    }

    public bool Enabled => _container is not null;

    public async Task<string?> SaveAsync(string keyPrefix, string fileName, byte[] bytes, string contentType)
    {
        if (_container is null)
        {
            return null;
        }

        var key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var blob = _container.GetBlobClient(key);
        using var ms = new MemoryStream(bytes, writable: false);
        await blob.UploadAsync(ms, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            },
        });
        return key;
    }

    public async Task<byte[]?> ReadAsync(string? blobKey)
    {
        if (_container is null || string.IsNullOrWhiteSpace(blobKey))
        {
            return null;
        }

        try
        {
            var blob = _container.GetBlobClient(blobKey);
            if (!await blob.ExistsAsync())
            {
                return null;
            }
            var resp = await blob.DownloadContentAsync();
            return resp.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blob read failed for key {Key}.", blobKey);
            return null;
        }
    }

    public async Task DeleteAsync(string? blobKey)
    {
        if (_container is null || string.IsNullOrWhiteSpace(blobKey))
        {
            return;
        }

        try
        {
            await _container.GetBlobClient(blobKey).DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blob delete failed for key {Key}.", blobKey);
        }
    }
}
