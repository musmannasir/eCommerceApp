namespace ECommerceApp.Application.Common.Options;

/// <summary>Binds the "FileStorage" configuration section.</summary>
public class FileStorageOptions
{
    public long MaxImageSizeBytes { get; set; } = 5 * 1024 * 1024;
}
