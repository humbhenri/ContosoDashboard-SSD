using ContosoDashboard.Models;

namespace ContosoDashboard.Tests;

public class DocumentSearchTests : IDisposable
{
    private readonly TestDb _db = new();

    [Theory]
    [InlineData("roadmap")]          // title
    [InlineData("architecture")]     // description
    [InlineData("budget")]           // tag
    [InlineData("ni kang")]          // uploader display name
    [InlineData("ContosoDashboard")] // project name
    public async Task Search_matches_each_field(string term)
    {
        var id = await _db.UploadAsync(TestDb.DeveloperId, title: "Product Roadmap",
            description: "Architecture overview", projectId: TestDb.ProjectOneId, tags: new[] { "Budget" });

        var results = await _db.CreateService().SearchAsync(term, TestDb.DeveloperId);

        Assert.Contains(results, r => r.DocumentId == id);
    }

    [Fact]
    public async Task Search_excludes_inaccessible_documents()
    {
        var mine = await _db.UploadAsync(TestDb.DeveloperId, title: "Budget plan");
        var theirs = await _db.UploadAsync(TestDb.OutsiderId, title: "Budget secret");

        var results = await _db.CreateService().SearchAsync("budget", TestDb.DeveloperId);

        Assert.Contains(results, r => r.DocumentId == mine);
        Assert.DoesNotContain(results, r => r.DocumentId == theirs);
    }

    [Fact]
    public async Task Administrator_search_includes_all_documents()
    {
        await _db.UploadAsync(TestDb.DeveloperId, title: "Budget plan");
        await _db.UploadAsync(TestDb.OutsiderId, title: "Budget secret");

        var results = await _db.CreateService().SearchAsync("budget", TestDb.AdminId);

        Assert.Equal(2, results.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_term_returns_empty(string term)
    {
        await _db.UploadAsync(TestDb.DeveloperId);
        Assert.Empty(await _db.CreateService().SearchAsync(term, TestDb.DeveloperId));
    }

    [Fact]
    public async Task Wildcard_characters_are_matched_literally()
    {
        await _db.UploadAsync(TestDb.DeveloperId, title: "Plain title");
        Assert.Empty(await _db.CreateService().SearchAsync("%", TestDb.DeveloperId));
        Assert.Empty(await _db.CreateService().SearchAsync("_", TestDb.DeveloperId));
    }

    [Fact]
    public async Task No_match_returns_empty()
    {
        await _db.UploadAsync(TestDb.DeveloperId, category: DocumentCategories.Other);
        Assert.Empty(await _db.CreateService().SearchAsync("nonexistent-term", TestDb.DeveloperId));
    }

    public void Dispose() => _db.Dispose();
}
