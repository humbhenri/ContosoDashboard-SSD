using ContosoDashboard.Services.Storage;

namespace ContosoDashboard.Tests;

public class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "contoso-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly LocalFileStorageService _storage;

    public LocalFileStorageServiceTests()
    {
        _storage = new LocalFileStorageService(_root);
    }

    [Fact]
    public async Task Upload_then_download_returns_identical_bytes()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await _storage.UploadAsync(new MemoryStream(bytes), "4/personal/abc.pdf", "application/pdf");

        await using var stream = await _storage.DownloadAsync("4/personal/abc.pdf");
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal(bytes, copy.ToArray());
        Assert.True(File.Exists(Path.Combine(_root, "4", "personal", "abc.pdf")));
    }

    [Fact]
    public async Task Delete_missing_file_is_noop()
    {
        await _storage.DeleteAsync("4/personal/missing.pdf");
    }

    [Fact]
    public async Task Download_missing_file_throws_FileNotFound()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.DownloadAsync("4/personal/missing.pdf"));
    }

    [Theory]
    [InlineData("../escape.pdf")]
    [InlineData("4/../../escape.pdf")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public async Task Keys_escaping_root_are_rejected(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _storage.UploadAsync(new MemoryStream(new byte[] { 1 }), key, "application/pdf"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
