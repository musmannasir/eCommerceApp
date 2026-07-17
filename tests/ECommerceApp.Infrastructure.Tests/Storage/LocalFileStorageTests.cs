using ECommerceApp.Application.Common.Options;
using ECommerceApp.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerceApp.Infrastructure.Tests.Storage;

/// <summary>
/// Exercises the real disk-writing implementation (not a fake), including size-limit and
/// signature validation. Writes under a uniquely-named category folder under the test
/// assembly's own wwwroot/uploads, cleaned up afterward.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _category = $"test-{Guid.NewGuid():N}";
    private readonly string _categoryDirectory;

    public LocalFileStorageTests()
    {
        _categoryDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", _category);
    }

    public void Dispose()
    {
        if (Directory.Exists(_categoryDirectory))
        {
            Directory.Delete(_categoryDirectory, recursive: true);
        }
    }

    private static LocalFileStorage CreateStorage(long maxSizeBytes = 5 * 1024 * 1024) =>
        new(Options.Create(new FileStorageOptions { MaxImageSizeBytes = maxSizeBytes }), NullLogger<LocalFileStorage>.Instance);

    private static string PhysicalPath(string webRelativePath) => Path.Combine(
        Directory.GetCurrentDirectory(), "wwwroot", webRelativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    private static readonly byte[] ValidPngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Saving_a_valid_png_succeeds_and_returns_a_web_relative_path()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream(ValidPngBytes);

        var result = await storage.SaveImageAsync(content, "photo.png", "image/png", _category);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().StartWith($"/uploads/{_category}/").And.EndWith(".png");

        File.Exists(PhysicalPath(result.Value)).Should().BeTrue();
    }

    [Fact]
    public async Task The_stored_extension_is_derived_from_the_real_signature_not_the_claimed_one()
    {
        var storage = CreateStorage();
        // Claims to be a .exe with a fake content-type, but the bytes are a real PNG.
        await using var content = new MemoryStream(ValidPngBytes);

        var result = await storage.SaveImageAsync(content, "totally-safe.exe", "application/octet-stream", _category);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().EndWith(".png");
    }

    [Fact]
    public async Task Content_that_is_not_a_real_image_is_rejected_regardless_of_claimed_type()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream("<?php system($_GET['c']); ?>"u8.ToArray());

        var result = await storage.SaveImageAsync(content, "photo.png", "image/png", _category);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("file.invalid_type");
    }

    [Fact]
    public async Task Oversized_content_is_rejected()
    {
        var storage = CreateStorage(maxSizeBytes: 10);
        var oversized = ValidPngBytes.Concat(new byte[100]).ToArray();
        await using var content = new MemoryStream(oversized);

        var result = await storage.SaveImageAsync(content, "photo.png", "image/png", _category);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("file.too_large");
    }

    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream();

        var result = await storage.SaveImageAsync(content, "empty.png", "image/png", _category);

        result.IsFailure.Should().BeTrue();
        result.FirstError.Code.Should().Be("file.empty");
    }

    [Fact]
    public async Task Deleting_a_saved_file_removes_it_from_disk()
    {
        var storage = CreateStorage();
        await using var content = new MemoryStream(ValidPngBytes);
        var saved = (await storage.SaveImageAsync(content, "photo.png", "image/png", _category)).Value;

        await storage.DeleteAsync(saved);

        File.Exists(PhysicalPath(saved)).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_a_path_outside_the_uploads_root_is_a_no_op()
    {
        var storage = CreateStorage();

        // Should not throw, and must not attempt to touch anything outside uploads/.
        var act = async () => await storage.DeleteAsync("/etc/passwd");

        await act.Should().NotThrowAsync();
    }
}
