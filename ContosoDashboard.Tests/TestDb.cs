using System.Text;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Scanning;
using ContosoDashboard.Services.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContosoDashboard.Tests;

/// <summary>
/// SQLite in-memory database plus temp-directory file storage for one test.
/// Seed (from ApplicationDbContext + extras): 1 Admin, 2 Project Manager (manages projects 1 and 2),
/// 3 TeamLead on project 1, 4 Developer on project 1, 5 Outsider (no projects), 6 member of project 2.
/// </summary>
public sealed class TestDb : IDisposable
{
    public const int AdminId = 1;
    public const int ManagerId = 2;
    public const int TeamLeadId = 3;
    public const int DeveloperId = 4;
    public const int OutsiderId = 5;
    public const int OtherProjectMemberId = 6;
    public const int ProjectOneId = 1;
    public const int ProjectTwoId = 2;

    private readonly SqliteConnection _connection;

    public string StorageRoot { get; }
    public ApplicationDbContext Context { get; }
    public LocalFileStorageService Storage { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        StorageRoot = Path.Combine(Path.GetTempPath(), "contoso-tests", Guid.NewGuid().ToString("N"));
        Storage = new LocalFileStorageService(StorageRoot);

        Context = CreateContext();
        Context.Database.EnsureCreated();
        SeedExtras();
    }

    public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptors)
            .Options;
        return new ApplicationDbContext(options);
    }

    public DocumentService CreateService(
        ApplicationDbContext? context = null,
        IFileStorageService? storage = null,
        IFileScanService? scanner = null)
    {
        var ctx = context ?? Context;
        return new DocumentService(
            ctx,
            storage ?? Storage,
            scanner ?? new ContentTypeScanService(),
            new NotificationService(ctx),
            NullLogger<DocumentService>.Instance);
    }

    public async Task<int> UploadAsync(
        int userId,
        string fileName = "report.pdf",
        byte[]? content = null,
        int? projectId = null,
        string title = "Test Document",
        string category = DocumentCategories.Reports,
        string? description = null,
        params string[] tags)
    {
        var result = await CreateService().UploadAsync(new DocumentUploadRequest
        {
            OriginalFileName = fileName,
            Title = title,
            Category = category,
            Description = description,
            ProjectId = projectId,
            Tags = tags.ToList()
        }, new MemoryStream(content ?? Files.Pdf), userId);

        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.DocumentId!.Value;
    }

    public int StoredFileCount() =>
        Directory.Exists(StorageRoot) ? Directory.GetFiles(StorageRoot, "*", SearchOption.AllDirectories).Length : 0;

    private void SeedExtras()
    {
        Context.Users.AddRange(
            new User { UserId = OutsiderId, Email = "outsider@contoso.com", DisplayName = "Olivia Outsider", Role = UserRole.Employee },
            new User { UserId = OtherProjectMemberId, Email = "other@contoso.com", DisplayName = "Oscar Other", Role = UserRole.Employee });

        Context.Projects.Add(new Project
        {
            ProjectId = ProjectTwoId,
            Name = "Second Project",
            ProjectManagerId = ManagerId,
            StartDate = DateTime.UtcNow,
            Status = ProjectStatus.Active
        });

        Context.ProjectMembers.Add(new ProjectMember { ProjectId = ProjectTwoId, UserId = OtherProjectMemberId, Role = "Developer" });
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
        if (Directory.Exists(StorageRoot))
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
    }
}

public static class Files
{
    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% test document\n");
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    public static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
    public static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00 };
    public static readonly byte[] Text = Encoding.UTF8.GetBytes("plain text notes");
    public static readonly byte[] Executable = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 };
}

/// <summary>Throws when a Document is being inserted, simulating a database failure.</summary>
public sealed class FailDocumentInsertInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<Document>().Any(e => e.State == EntityState.Added))
        {
            throw new DbUpdateException("Simulated database failure.");
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

/// <summary>Storage that always fails to upload.</summary>
public sealed class FailingStorage : IFileStorageService
{
    public Task<string> UploadAsync(Stream fileStream, string storageKey, string contentType) =>
        throw new IOException("Simulated storage failure.");
    public Task DeleteAsync(string storageKey) => Task.CompletedTask;
    public Task<Stream> DownloadAsync(string storageKey) => throw new FileNotFoundException();
    public Task<string> GetUrlAsync(string storageKey, TimeSpan expiration) => throw new NotSupportedException();
}
