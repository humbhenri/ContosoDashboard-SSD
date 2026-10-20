namespace ContosoDashboard.Services.Storage;

/// <summary>
/// Storage abstraction for document file content. Swap the implementation via DI
/// (e.g., LocalFileStorageService for training, Azure Blob Storage for production).
/// </summary>
public interface IFileStorageService
{
    /// <summary>Stores the stream under the system-generated <paramref name="storageKey"/> and returns the key.</summary>
    Task<string> UploadAsync(Stream fileStream, string storageKey, string contentType);

    /// <summary>Deletes the stored file; does nothing if it does not exist.</summary>
    Task DeleteAsync(string storageKey);

    /// <summary>Opens the stored file for reading. Throws <see cref="FileNotFoundException"/> if missing.</summary>
    Task<Stream> DownloadAsync(string storageKey);

    /// <summary>Returns a time-limited URL for the stored file, where the provider supports it.</summary>
    Task<string> GetUrlAsync(string storageKey, TimeSpan expiration);
}
