using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentUploadTests : IDisposable
{
    private readonly TestDb _db = new();

    private DocumentUploadRequest Request(string fileName = "report.pdf", int? projectId = null,
        string title = "Quarterly Report", string category = DocumentCategories.Reports) => new()
    {
        OriginalFileName = fileName,
        Title = title,
        Category = category,
        ProjectId = projectId,
        Tags = new List<string> { " Budget ", "budget", "Q3" }
    };

    [Fact]
    public async Task Valid_upload_stores_file_and_metadata()
    {
        var result = await _db.CreateService().UploadAsync(Request(), new MemoryStream(Files.Pdf), TestDb.DeveloperId);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var doc = await _db.Context.Documents.Include(d => d.Tags).SingleAsync();
        Assert.Equal("Quarterly Report", doc.Title);
        Assert.Equal(DocumentCategories.Reports, doc.Category);
        Assert.Equal("report.pdf", doc.OriginalFileName);
        Assert.Equal("application/pdf", doc.FileType);
        Assert.Equal(Files.Pdf.Length, doc.FileSizeBytes);
        Assert.Equal(TestDb.DeveloperId, doc.UploadedByUserId);
        Assert.Matches(@"^4/personal/[0-9a-f]{32}\.pdf$", doc.FilePath);
        Assert.DoesNotContain("report", doc.FilePath);
        Assert.Equal(new[] { "budget", "q3" }, doc.Tags.Select(t => t.Tag).OrderBy(t => t));
        Assert.True(File.Exists(Path.Combine(_db.StorageRoot, doc.FilePath)));
    }

    [Fact]
    public async Task Project_upload_uses_project_folder()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);
        var doc = await _db.Context.Documents.FindAsync(id);
        Assert.StartsWith("4/1/", doc!.FilePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_title_is_rejected(string title)
    {
        var result = await _db.CreateService().UploadAsync(Request(title: title), new MemoryStream(Files.Pdf), TestDb.DeveloperId);
        Assert.False(result.Succeeded);
        Assert.Equal(0, _db.StoredFileCount());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Secret Stuff")]
    public async Task Missing_or_unknown_category_is_rejected(string category)
    {
        var result = await _db.CreateService().UploadAsync(Request(category: category), new MemoryStream(Files.Pdf), TestDb.DeveloperId);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task File_over_25_MB_is_rejected()
    {
        var big = new byte[DocumentCategories.MaxFileSizeBytes + 1];
        Files.Pdf.CopyTo(big, 0);

        var result = await _db.CreateService().UploadAsync(Request(), new MemoryStream(big), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
        Assert.Equal(DocumentService.SizeLimitMessage, result.ErrorMessage);
        Assert.Equal(0, _db.StoredFileCount());
    }

    [Fact]
    public async Task File_of_exactly_25_MB_is_accepted()
    {
        var exact = new byte[DocumentCategories.MaxFileSizeBytes];
        Files.Pdf.CopyTo(exact, 0);

        var result = await _db.CreateService().UploadAsync(Request(), new MemoryStream(exact), TestDb.DeveloperId);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public async Task Scan_rejection_stores_nothing()
    {
        var result = await _db.CreateService().UploadAsync(Request("invoice.pdf"), new MemoryStream(Files.Executable), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
        Assert.Equal(0, _db.StoredFileCount());
        Assert.Empty(_db.Context.Documents);
    }

    [Fact]
    public async Task Database_failure_removes_stored_file()
    {
        using var failingContext = _db.CreateContext(new FailDocumentInsertInterceptor());
        var service = _db.CreateService(failingContext);

        var result = await service.UploadAsync(Request(), new MemoryStream(Files.Pdf), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
        Assert.Equal(0, _db.StoredFileCount());
        Assert.Empty(_db.Context.Documents);
    }

    [Fact]
    public async Task Storage_failure_creates_no_record()
    {
        var service = _db.CreateService(storage: new FailingStorage());

        var result = await service.UploadAsync(Request(), new MemoryStream(Files.Pdf), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
        Assert.Empty(_db.Context.Documents);
    }

    [Fact]
    public async Task Upload_to_project_user_does_not_belong_to_is_rejected()
    {
        var result = await _db.CreateService().UploadAsync(
            Request(projectId: TestDb.ProjectOneId), new MemoryStream(Files.Pdf), TestDb.OutsiderId);

        Assert.False(result.Succeeded);
        Assert.Equal(0, _db.StoredFileCount());
    }

    [Fact]
    public async Task Project_manager_can_upload_to_managed_project()
    {
        var result = await _db.CreateService().UploadAsync(
            Request(projectId: TestDb.ProjectOneId), new MemoryStream(Files.Pdf), TestDb.ManagerId);

        Assert.True(result.Succeeded, result.ErrorMessage);
    }

    [Fact]
    public async Task Upload_logs_activity()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);

        var activity = await _db.Context.DocumentActivities.SingleAsync();
        Assert.Equal(DocumentActions.Upload, activity.Action);
        Assert.Equal(id, activity.DocumentId);
        Assert.Equal(TestDb.DeveloperId, activity.UserId);
    }

    [Fact]
    public async Task Same_file_name_twice_is_stored_independently()
    {
        var first = await _db.UploadAsync(TestDb.DeveloperId, "same.pdf");
        var second = await _db.UploadAsync(TestDb.DeveloperId, "same.pdf");

        var paths = await _db.Context.Documents.Where(d => d.DocumentId == first || d.DocumentId == second)
            .Select(d => d.FilePath).ToListAsync();
        Assert.Equal(2, paths.Distinct().Count());
        Assert.Equal(2, _db.StoredFileCount());
    }

    // US2: project members are notified when a document is added

    [Fact]
    public async Task Project_upload_notifies_other_members_and_manager()
    {
        await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);

        var recipients = await _db.Context.Notifications
            .Where(n => n.Type == NotificationType.DocumentAdded)
            .Select(n => n.UserId)
            .OrderBy(id => id)
            .ToListAsync();

        Assert.Equal(new[] { TestDb.ManagerId, TestDb.TeamLeadId }, recipients);
    }

    [Fact]
    public async Task Personal_upload_sends_no_notifications()
    {
        await _db.UploadAsync(TestDb.DeveloperId);
        Assert.Empty(_db.Context.Notifications.Where(n => n.Type == NotificationType.DocumentAdded));
    }

    public void Dispose() => _db.Dispose();
}
