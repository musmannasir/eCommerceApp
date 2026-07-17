namespace ECommerceApp.Infrastructure.Storage;

/// <summary>
/// Identifies an image's true type from its file signature (magic bytes), never from the
/// caller-supplied extension or Content-Type header - both are trivial to spoof.
/// </summary>
internal static class ImageSignatureDetector
{
    public const int RequiredHeaderBytes = 12;

    public static (string Extension, string ContentType)? Detect(byte[] header)
    {
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (header.Length >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return (".png", "image/png");
        }

        if (header.Length >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
        {
            return (".webp", "image/webp");
        }

        return null;
    }
}
