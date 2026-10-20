namespace ContosoDashboard.Services.Storage;

/// <summary>
/// Stores files on the local filesystem under a root directory outside wwwroot.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string storageKey, string contentType)
    {
        var fullPath = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        // FileMode.CreateNew: keys are unique, never overwrite an existing file
        await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(target);
        return storageKey;
    }

    public Task DeleteAsync(string storageKey)
    {
        var fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<Stream> DownloadAsync(string storageKey)
    {
        var fullPath = ResolvePath(storageKey);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Stored file not found.", storageKey);
        }
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<string> GetUrlAsync(string storageKey, TimeSpan expiration)
    {
        // Local files have no direct URL; they are served only through the authorized
        // /documents/{id}/download endpoint.
        throw new NotSupportedException("Local storage does not issue direct URLs.");
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.IsPathRooted(storageKey))
        {
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, storageKey));
        var rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Storage key resolves outside the storage root.", nameof(storageKey));
        }

        return fullPath;
    }
}
