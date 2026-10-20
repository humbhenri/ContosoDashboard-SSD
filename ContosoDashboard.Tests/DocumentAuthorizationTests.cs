using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Tests;

public class DocumentAuthorizationTests : IDisposable
{
    private readonly TestDb _db = new();

    // ---- US1: My Documents and download

    [Fact]
    public async Task My_documents_returns_only_own_uploads()
    {
        await _db.UploadAsync(TestDb.DeveloperId, title: "Mine");
        await _db.UploadAsync(TestDb.OutsiderId, title: "Theirs");

        var mine = await _db.CreateService().GetMyDocumentsAsync(TestDb.DeveloperId, new DocumentQuery());

        Assert.Equal(new[] { "Mine" }, mine.Select(d => d.Title));
    }

    [Fact]
    public async Task My_documents_sorts_by_requested_field()
    {
        await _db.UploadAsync(TestDb.DeveloperId, title: "Beta", content: Files.Pdf);
        await _db.UploadAsync(TestDb.DeveloperId, "big.pdf", content: Files.Pdf.Concat(new byte[100]).ToArray(), title: "Alpha");
        await _db.UploadAsync(TestDb.DeveloperId, title: "Gamma", category: DocumentCategories.Other);
        var service = _db.CreateService();

        var byTitle = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { SortBy = DocumentSortField.Title, Descending = false });
        var bySize = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { SortBy = DocumentSortField.FileSize, Descending = true });
        var byCategory = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { SortBy = DocumentSortField.Category, Descending = false });
        var byDateDesc = await service.GetMyDocumentsAsync(TestDb.DeveloperId, new DocumentQuery());

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, byTitle.Select(d => d.Title));
        Assert.Equal("Alpha", bySize.First().Title);
        Assert.Equal("Gamma", byCategory.First().Title);
        Assert.Equal("Gamma", byDateDesc.First().Title);
    }

    [Fact]
    public async Task My_documents_filters_by_category_project_and_date()
    {
        await _db.UploadAsync(TestDb.DeveloperId, title: "Report", category: DocumentCategories.Reports);
        await _db.UploadAsync(TestDb.DeveloperId, title: "Project Doc", category: DocumentCategories.ProjectDocuments,
            projectId: TestDb.ProjectOneId);
        var service = _db.CreateService();

        var byCategory = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { Category = DocumentCategories.Reports });
        var byProject = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { ProjectId = TestDb.ProjectOneId });
        var today = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { FromDate = DateTime.UtcNow.Date, ToDate = DateTime.UtcNow.Date });
        var future = await service.GetMyDocumentsAsync(TestDb.DeveloperId,
            new DocumentQuery { FromDate = DateTime.UtcNow.Date.AddDays(1) });

        Assert.Equal(new[] { "Report" }, byCategory.Select(d => d.Title));
        Assert.Equal(new[] { "Project Doc" }, byProject.Select(d => d.Title));
        Assert.Equal(2, today.Count);
        Assert.Empty(future);
    }

    [Theory]
    [InlineData(TestDb.DeveloperId, true)]   // uploader
    [InlineData(TestDb.AdminId, true)]       // administrator
    [InlineData(TestDb.TeamLeadId, true)]    // TeamLead on a project the uploader belongs to
    [InlineData(TestDb.OutsiderId, false)]   // unrelated
    [InlineData(TestDb.ManagerId, false)]    // manager, but document is personal
    public async Task Personal_document_download_access(int userId, bool allowed)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);

        await using var file = await _db.CreateService().OpenForDownloadAsync(id, userId, previewOnly: false);

        Assert.Equal(allowed, file != null);
    }

    [Fact]
    public async Task Download_returns_original_content_and_logs_activity()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, "minutes.pdf");

        await using (var file = await _db.CreateService().OpenForDownloadAsync(id, TestDb.DeveloperId, previewOnly: false))
        {
            Assert.NotNull(file);
            Assert.Equal("minutes.pdf", file!.FileName);
            Assert.Equal("application/pdf", file.ContentType);
            using var copy = new MemoryStream();
            await file.Content.CopyToAsync(copy);
            Assert.Equal(Files.Pdf, copy.ToArray());
        }

        Assert.Contains(_db.Context.DocumentActivities, a => a.Action == DocumentActions.Download && a.DocumentId == id);
    }

    [Fact]
    public async Task Denied_download_is_not_logged()
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId);

        await _db.CreateService().OpenForDownloadAsync(id, TestDb.OutsiderId, previewOnly: false);

        Assert.DoesNotContain(_db.Context.DocumentActivities, a => a.Action == DocumentActions.Download);
    }

    [Fact]
    public async Task Missing_document_returns_null()
    {
        Assert.Null(await _db.CreateService().OpenForDownloadAsync(999, TestDb.AdminId, previewOnly: false));
        Assert.Null(await _db.CreateService().GetByIdAsync(999, TestDb.AdminId));
    }

    // ---- US2: project documents

    [Theory]
    [InlineData(TestDb.DeveloperId, true)]           // member (uploader)
    [InlineData(TestDb.TeamLeadId, true)]            // member
    [InlineData(TestDb.ManagerId, true)]             // project manager
    [InlineData(TestDb.AdminId, true)]               // administrator
    [InlineData(TestDb.OutsiderId, false)]           // no projects
    [InlineData(TestDb.OtherProjectMemberId, false)] // member of a different project
    public async Task Project_document_access(int userId, bool allowed)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, projectId: TestDb.ProjectOneId);
        var service = _db.CreateService();

        var list = await service.GetProjectDocumentsAsync(TestDb.ProjectOneId, userId);
        await using var file = await service.OpenForDownloadAsync(id, userId, previewOnly: false);

        Assert.Equal(allowed, list.Any(d => d.DocumentId == id));
        Assert.Equal(allowed, file != null);
    }

    [Fact]
    public async Task Removed_member_loses_access_to_project_documents()
    {
        var id = await _db.UploadAsync(TestDb.ManagerId, projectId: TestDb.ProjectOneId);
        var service = _db.CreateService();
        Assert.NotNull(await service.GetByIdAsync(id, TestDb.TeamLeadId));

        var membership = await _db.Context.ProjectMembers.SingleAsync(m => m.UserId == TestDb.TeamLeadId && m.ProjectId == TestDb.ProjectOneId);
        _db.Context.ProjectMembers.Remove(membership);
        await _db.Context.SaveChangesAsync();

        Assert.Null(await service.GetByIdAsync(id, TestDb.TeamLeadId));
        Assert.Empty(await service.GetProjectDocumentsAsync(TestDb.ProjectOneId, TestDb.TeamLeadId));
    }

    // ---- Shares grant access (sharing operations themselves arrive with US5)

    [Fact]
    public async Task Direct_and_team_shares_grant_view_access()
    {
        var direct = await _db.UploadAsync(TestDb.DeveloperId, title: "Direct");
        var team = await _db.UploadAsync(TestDb.DeveloperId, title: "Team");
        _db.Context.DocumentShares.AddRange(
            new DocumentShare { DocumentId = direct, SharedWithUserId = TestDb.OutsiderId, SharedByUserId = TestDb.DeveloperId },
            new DocumentShare { DocumentId = team, SharedWithProjectId = TestDb.ProjectTwoId, SharedByUserId = TestDb.DeveloperId });
        await _db.Context.SaveChangesAsync();
        var service = _db.CreateService();

        Assert.NotNull(await service.GetByIdAsync(direct, TestDb.OutsiderId));
        Assert.Null(await service.GetByIdAsync(team, TestDb.OutsiderId));
        Assert.NotNull(await service.GetByIdAsync(team, TestDb.OtherProjectMemberId));
        Assert.NotNull(await service.GetByIdAsync(team, TestDb.ManagerId));
    }

    public void Dispose() => _db.Dispose();
}
