using ContosoDashboard.Services.Scanning;

namespace ContosoDashboard.Tests;

public class ContentTypeScanServiceTests
{
    private readonly ContentTypeScanService _scanner = new();

    public static IEnumerable<object[]> ValidFiles() => new[]
    {
        new object[] { "a.pdf", Files.Pdf, "application/pdf" },
        new object[] { "a.png", Files.Png, "image/png" },
        new object[] { "a.jpg", Files.Jpeg, "image/jpeg" },
        new object[] { "a.jpeg", Files.Jpeg, "image/jpeg" },
        new object[] { "a.docx", Files.Zip, "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        new object[] { "a.xlsx", Files.Zip, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        new object[] { "a.pptx", Files.Zip, "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        new object[] { "a.doc", Files.Ole, "application/msword" },
        new object[] { "a.xls", Files.Ole, "application/vnd.ms-excel" },
        new object[] { "a.ppt", Files.Ole, "application/vnd.ms-powerpoint" },
        new object[] { "a.txt", Files.Text, "text/plain" },
        new object[] { "UPPER.PDF", Files.Pdf, "application/pdf" },
    };

    [Theory]
    [MemberData(nameof(ValidFiles))]
    public async Task Allowed_type_with_matching_signature_is_clean(string fileName, byte[] content, string expectedMime)
    {
        var result = await _scanner.ScanAsync(new MemoryStream(content), fileName);

        Assert.True(result.IsClean);
        Assert.Equal(expectedMime, result.MimeType);
    }

    [Fact]
    public async Task Renamed_executable_is_rejected()
    {
        var result = await _scanner.ScanAsync(new MemoryStream(Files.Executable), "invoice.pdf");
        Assert.False(result.IsClean);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("macro.docm")]
    [InlineData("script.js")]
    [InlineData("noextension")]
    public async Task Disallowed_extension_is_rejected(string fileName)
    {
        var result = await _scanner.ScanAsync(new MemoryStream(Files.Pdf), fileName);
        Assert.False(result.IsClean);
    }

    [Fact]
    public async Task Text_with_nul_bytes_is_rejected()
    {
        var result = await _scanner.ScanAsync(new MemoryStream(new byte[] { 0x41, 0x00, 0x42 }), "notes.txt");
        Assert.False(result.IsClean);
    }

    [Fact]
    public async Task Stream_position_is_restored()
    {
        var stream = new MemoryStream(Files.Pdf);
        await _scanner.ScanAsync(stream, "a.pdf");
        Assert.Equal(0, stream.Position);
    }
}
