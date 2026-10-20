namespace ContosoDashboard.Services.Scanning;

/// <summary>
/// File-safety scan step run before an upload is stored. Replace the implementation via DI
/// with a real antivirus scanner in production.
/// </summary>
public interface IFileScanService
{
    /// <summary>Scans the content; the stream position is restored afterwards.</summary>
    Task<FileScanResult> ScanAsync(Stream content, string originalFileName);
}

public record FileScanResult(bool IsClean, string? Reason, string? MimeType)
{
    public static FileScanResult Clean(string mimeType) => new(true, null, mimeType);
    public static FileScanResult Rejected(string reason) => new(false, reason, null);
}
