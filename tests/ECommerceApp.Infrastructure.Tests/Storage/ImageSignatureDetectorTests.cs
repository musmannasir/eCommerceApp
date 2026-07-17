using ECommerceApp.Infrastructure.Storage;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Storage;

public class ImageSignatureDetectorTests
{
    [Fact]
    public void Detects_a_jpeg_signature()
    {
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        var result = ImageSignatureDetector.Detect(header);

        result.Should().Be((".jpg", "image/jpeg"));
    }

    [Fact]
    public void Detects_a_png_signature()
    {
        byte[] header = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var result = ImageSignatureDetector.Detect(header);

        result.Should().Be((".png", "image/png"));
    }

    [Fact]
    public void Detects_a_webp_signature()
    {
        byte[] header = [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50];

        var result = ImageSignatureDetector.Detect(header);

        result.Should().Be((".webp", "image/webp"));
    }

    [Fact]
    public void Rejects_content_that_is_not_a_recognized_image_even_with_a_spoofed_extension()
    {
        // Plain text pretending to be an image via its (irrelevant) extension/content-type.
        var header = "<?php system($_GET['c']); ?>"u8.ToArray();

        var result = ImageSignatureDetector.Detect(header);

        result.Should().BeNull();
    }

    [Fact]
    public void Rejects_an_empty_header()
    {
        ImageSignatureDetector.Detect([]).Should().BeNull();
    }
}
