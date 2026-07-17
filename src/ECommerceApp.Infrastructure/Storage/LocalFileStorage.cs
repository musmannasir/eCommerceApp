using System.Text.RegularExpressions;
using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Common.Options;
using ECommerceApp.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Storage;

/// <summary>
/// Stores images on local disk under wwwroot/uploads/{category}/. Filenames are always
/// random (never derived from user input); the true file type is determined from its
/// signature, not the caller-supplied extension or Content-Type.
/// </summary>
public sealed partial class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<LocalFileStorage> _logger;
    private readonly string _webRootPath;

    public LocalFileStorage(IOptions<FileStorageOptions> options, ILogger<LocalFileStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
        _webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<Result<string>> SaveImageAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string category,
        CancellationToken cancellationToken = default)
    {
        await using var buffered = new MemoryStream();
        var copied = await CopyWithLimitAsync(content, buffered, _options.MaxImageSizeBytes, cancellationToken);
        if (!copied)
        {
            return Result.Failure<string>(Error.Validation(
                "file.too_large",
                $"The image exceeds the maximum allowed size of {_options.MaxImageSizeBytes / (1024 * 1024)} MB."));
        }

        if (buffered.Length == 0)
        {
            return Result.Failure<string>(Error.Validation("file.empty", "The uploaded file is empty."));
        }

        buffered.Position = 0;
        var header = new byte[Math.Min(ImageSignatureDetector.RequiredHeaderBytes, buffered.Length)];
        _ = await buffered.ReadAsync(header, cancellationToken);

        var detected = ImageSignatureDetector.Detect(header);
        if (detected is null)
        {
            return Result.Failure<string>(Error.Validation(
                "file.invalid_type",
                "Only JPEG, PNG, and WebP images are allowed."));
        }

        var safeCategory = SafeCategoryRegex().Replace(category, string.Empty);
        var directory = Path.Combine(_webRootPath, "uploads", safeCategory);
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{detected.Value.Extension}";
        var physicalPath = Path.Combine(directory, fileName);

        buffered.Position = 0;
        await using (var fileStream = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write))
        {
            await buffered.CopyToAsync(fileStream, cancellationToken);
        }

        return Result.Success($"/uploads/{safeCategory}/{fileName}");
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var physicalPath = Path.Combine(_webRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        try
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to delete orphaned upload {Path}", relativePath);
        }

        return Task.CompletedTask;
    }

    private static async Task<bool> CopyWithLimitAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;

        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
            {
                return false;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    [GeneratedRegex("[^a-z0-9-]")]
    private static partial Regex SafeCategoryRegex();
}
