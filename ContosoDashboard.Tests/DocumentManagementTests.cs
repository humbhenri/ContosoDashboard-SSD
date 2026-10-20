using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentManagementTests : IDisposable
{
    private readonly TestDb _db = new();

    private static DocumentMetadataUpdate Update(string title = "Renamed") => new()
    {
        Title = title,
        Description = "Updated description",
        Category = DocumentCategories.Presentations,
        Tags = new List<string> { "new-tag" }
    };

    [Theory]
    [InlineData(TestDb.DeveloperId, true)]           // uploader
    [InlineData(TestDb.TeamLeadId, true)]            // TeamLead on uploader's project
    [InlineData(TestDb.AdminId, true)]               // administrator
    [InlineData(TestDb.ManagerId, false)]            // project manager may delete, not edit
    [InlineData(TestDb.OutsiderId, false)]
    [InlineData(TestDb.OtherProjectMemberId, false)]
    public async Task Edit_metadata_permissions(int userId, bool allowed)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId, tags: new[] { "old-tag" });

        var result = await _db.CreateService().UpdateMetadataAsync(id, Update(), userId);

        Assert.Equal(allowed, result.Succeeded);
        _db.Context.ChangeTracker.Clear();
        var doc = await _db.Context.Documents.Include(d => d.Tags).SingleAsync(d => d.DocumentId == id);
        if (allowed)
        {
            Assert.Equal("Renamed", doc.Title);
            Assert.Equal(DocumentCategories.Presentations, doc.Category);
            Assert.Equal(new[] { "new-tag" }, doc.Tags.Select(t => t.Tag));
            Assert.Contains(_db.Context.DocumentActivities, a => a.Action == DocumentActions.EditMetadata);
        }
        else
        {
            Assert.Equal("Test Document", doc.Title);
        }
    }

    [Fact]
    public async Task Edit_metadata_validates_input()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);

        var result = await _db.CreateService().UpdateMetadataAsync(id, Update(title: " "), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Uploader_can_replace_file()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, "v1.pdf");
        var oldPath = (await _db.Context.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == id)).FilePath;

        var result = await _db.CreateService().ReplaceFileAsync(id, "v2.png", new MemoryStream(Files.Png), TestDb.DeveloperId);

        Assert.True(result.Succeeded, result.ErrorMessage);
        var doc = await _db.Context.Documents.AsNoTracking().SingleAsync(d => d.DocumentId == id);
        Assert.Equal("v2.png", doc.OriginalFileName);
        Assert.Equal("image/png", doc.FileType);
        Assert.Equal(Files.Png.Length, doc.FileSizeBytes);
        Assert.NotEqual(oldPath, doc.FilePath);
        Assert.False(File.Exists(Path.Combine(_db.StorageRoot, oldPath)));
        Assert.True(File.Exists(Path.Combine(_db.StorageRoot, doc.FilePath)));
        Assert.Contains(_db.Context.DocumentActivities, a => a.Action == DocumentActions.ReplaceFile);
    }

    [Theory]
    [InlineData(TestDb.AdminId)]
    [InlineData(TestDb.TeamLeadId)]
    [InlineData(TestDb.ManagerId)]
    [InlineData(TestDb.OutsiderId)]
    public async Task Only_uploader_can_replace_file(int userId)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);

        var result = await _db.CreateService().ReplaceFileAsync(id, "v2.pdf", new MemoryStream(Files.Pdf), userId);

        Assert.False(result.Succeeded);
        Assert.Equal(1, _db.StoredFileCount());
    }

    [Fact]
    public async Task Replace_with_invalid_file_keeps_original()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);

        var result = await _db.CreateService().ReplaceFileAsync(id, "evil.pdf", new MemoryStream(Files.Executable), TestDb.DeveloperId);

        Assert.False(result.Succeeded);
        Assert.Equal(1, _db.StoredFileCount());
    }

    [Theory]
    [InlineData(TestDb.DeveloperId, true)]           // uploader
    [InlineData(TestDb.ManagerId, true)]             // project manager of document's project
    [InlineData(TestDb.TeamLeadId, true)]            // TeamLead on uploader's project
    [InlineData(TestDb.AdminId, true)]               // administrator
    [InlineData(TestDb.OutsiderId, false)]
    [InlineData(TestDb.OtherProjectMemberId, false)]
    public async Task Delete_permissions(int userId, bool allowed)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);

        var result = await _db.CreateService().DeleteAsync(id, userId);

        Assert.Equal(allowed, result.Succeeded);
        Assert.Equal(!allowed, await _db.Context.Documents.AnyAsync(d => d.DocumentId == id));
        Assert.Equal(allowed ? 0 : 1, _db.StoredFileCount());
    }

    [Fact]
    public async Task Project_manager_cannot_delete_personal_document()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);
        var result = await _db.CreateService().DeleteAsync(id, TestDb.ManagerId);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_removes_tags_shares_and_task_links_but_keeps_activity()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId, tags: new[] { "keep" });
        _db.Context.DocumentShares.Add(new DocumentShare { DocumentId = id, SharedWithUserId = TestDb.OutsiderId, SharedByUserId = TestDb.DeveloperId });
        _db.Context.TaskDocuments.Add(new TaskDocument { DocumentId = id, TaskId = 1, AttachedByUserId = TestDb.DeveloperId });
        await _db.Context.SaveChangesAsync();
        _db.Context.ChangeTracker.Clear();

        var result = await _db.CreateService().DeleteAsync(id, TestDb.DeveloperId);

        Assert.True(result.Succeeded, result.ErrorMessage);
        _db.Context.ChangeTracker.Clear();
        Assert.Empty(_db.Context.DocumentTags);
        Assert.Empty(_db.Context.DocumentShares);
        Assert.Empty(_db.Context.TaskDocuments);
        var actions = await _db.Context.DocumentActivities.Where(a => a.DocumentId == id).Select(a => a.Action).ToListAsync();
        Assert.Contains(DocumentActions.Upload, actions);
        Assert.Contains(DocumentActions.Delete, actions);
    }

    [Fact]
    public async Task List_items_expose_permission_flags()
    {
        await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);
        var service = _db.CreateService();

        var asManager = (await service.GetProjectDocumentsAsync(TestDb.ProjectOneId, TestDb.ManagerId)).Single();
        var asUploader = (await service.GetProjectDocumentsAsync(TestDb.ProjectOneId, TestDb.DeveloperId)).Single();

        Assert.False(asManager.CanEdit);
        Assert.True(asManager.CanDelete);
        Assert.False(asManager.CanReplace);
        Assert.True(asUploader.CanEdit && asUploader.CanDelete && asUploader.CanReplace && asUploader.CanShare);
    }

    public void Dispose() => _db.Dispose();
}
