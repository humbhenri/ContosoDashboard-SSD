using ContosoDashboard.Models;

namespace ContosoDashboard.Services.Scanning;

/// <summary>
/// Offline training scanner: checks the extension allow-list and verifies the file's
/// signature matches its extension. It does NOT detect viruses.
/// </summary>
public class ContentTypeScanService : IFileScanService
{
    public const string RejectionMessage =
        "Unsupported or invalid file. Allowed: PDF, Word, Excel, PowerPoint, text, JPEG, PNG.";

    private const int HeaderBytes = 8192;

    private static readonly byte[] Pdf = "%PDF"u8.ToArray();
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    public async Task<FileScanResult> ScanAsync(Stream content, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName ?? string.Empty).ToLowerInvariant();
        if (!DocumentCategories.AllowedExtensions.TryGetValue(extension, out var mimeType))
        {
            return FileScanResult.Rejected(RejectionMessage);
        }

        var start = content.Position;
        var buffer = new byte[HeaderBytes];
        var read = 0;
        try
        {
            int n;
            while (read < buffer.Length && (n = await content.ReadAsync(buffer.AsMemory(read))) > 0)
            {
                read += n;
            }
        }
        finally
        {
            content.Position = start;
        }

        var header = buffer.AsSpan(0, read);
        var valid = extension switch
        {
            ".pdf" => header.StartsWith(Pdf),
            ".png" => header.StartsWith(Png),
            ".jpg" or ".jpeg" => header.StartsWith(Jpeg),
            ".docx" or ".xlsx" or ".pptx" => header.StartsWith(Zip),
            ".doc" or ".xls" or ".ppt" => header.StartsWith(Ole),
            ".txt" => header.IndexOf((byte)0) < 0,
            _ => false
        };

        return valid ? FileScanResult.Clean(mimeType) : FileScanResult.Rejected(RejectionMessage);
    }
}
