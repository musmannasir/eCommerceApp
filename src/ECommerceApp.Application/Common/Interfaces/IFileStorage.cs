using ECommerceApp.Domain.Common;

namespace ECommerceApp.Application.Common.Interfaces;

/// <summary>
/// Stores uploaded images behind an abstraction so the storage backend (local disk today,
/// potentially cloud storage later) can change without touching callers. Implementations
/// must validate content (not just extension/content-type, which can be spoofed), generate
/// random filenames, and never trust caller-supplied paths.
/// </summary>
public interface IFileStorage
{
    Task<Result<string>> SaveImageAsync(
        Stream content,
        string originalFileName,
        string contentType,
        string category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}
