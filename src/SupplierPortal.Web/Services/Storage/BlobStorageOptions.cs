namespace SupplierPortal.Web.Services.Storage;

/// <summary>Configuration for Azure Blob Storage of user-uploaded files, bound from the
/// "BlobStorage" section. When <see cref="ConnectionString"/> is blank the feature is disabled and
/// uploads continue to use local disk / the database (existing behaviour).</summary>
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>Master on/off switch. When false, files are always stored in the database even if a
    /// <see cref="ConnectionString"/> is set. When true (default), blob is used provided a connection
    /// string is configured and it initialises.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Azure Storage account connection string. Blank = blob storage disabled.</summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>Container that holds all user files (documents, photos, logos). Created if missing.</summary>
    public string ContainerName { get; set; } = "userfiles";
}
