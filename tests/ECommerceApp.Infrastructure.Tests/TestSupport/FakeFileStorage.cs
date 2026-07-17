using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Domain.Common;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>In-memory stand-in for <see cref="IFileStorage"/>, so ProductService tests don't touch real disk.</summary>
public sealed class FakeFileStorage : IFileStorage
{
    public List<string> SavedPaths { get; } = [];
    public List<string> DeletedPaths { get; } = [];

    public Task<Result<string>> SaveImageAsync(
        Stream content, string originalFileName, string contentType, string category, CancellationToken cancellationToken = default)
    {
        var path = $"/uploads/{category}/{Guid.NewGuid():N}.jpg";
        SavedPaths.Add(path);
        return Task.FromResult(Result.Success(path));
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        DeletedPaths.Add(relativePath);
        return Task.CompletedTask;
    }
}
