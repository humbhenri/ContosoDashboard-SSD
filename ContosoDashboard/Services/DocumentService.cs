using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Scanning;
using ContosoDashboard.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    Task<UploadResult> UploadAsync(DocumentUploadRequest request, Stream content, int requestingUserId);

    Task<List<DocumentListItem>> GetMyDocumentsAsync(int userId, DocumentQuery query);
    Task<List<DocumentListItem>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<List<DocumentListItem>> SearchAsync(string term, int requestingUserId);
    Task<DocumentDetails?> GetByIdAsync(int documentId, int requestingUserId);

    Task<DocumentFile?> OpenForDownloadAsync(int documentId, int requestingUserId, bool previewOnly);

    Task<OperationResult> UpdateMetadataAsync(int documentId, DocumentMetadataUpdate update, int requestingUserId);
    Task<OperationResult> ReplaceFileAsync(int documentId, string originalFileName, Stream content, int requestingUserId);
    Task<OperationResult> DeleteAsync(int documentId, int requestingUserId);
}

public class DocumentUploadRequest
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class DocumentMetadataUpdate
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public enum DocumentSortField
{
    UploadedDate,
    Title,
    Category,
    FileSize
}

public class DocumentQuery
{
    public string? Category { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DocumentSortField SortBy { get; set; } = DocumentSortField.UploadedDate;
    public bool Descending { get; set; } = true;
}

public class DocumentListItem
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime UploadedDate { get; set; }
    public long FileSizeBytes { get; set; }
    public string FileType { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public int UploadedByUserId { get; set; }
    public string UploaderName { get; set; } = string.Empty;
    public bool IsPreviewable { get; set; }
    public bool CanEdit { get; set; }
    public bool CanReplace { get; set; }
    public bool CanDelete { get; set; }
    public bool CanShare { get; set; }
}

public class DocumentDetails : DocumentListItem
{
    public string? Description { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

public sealed class DocumentFile : IAsyncDisposable
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public class OperationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }

    public static OperationResult Success() => new() { Succeeded = true };
    public static OperationResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}

public class UploadResult : OperationResult
{
    public int? DocumentId { get; init; }

    public static UploadResult Success(int documentId) => new() { Succeeded = true, DocumentId = documentId };
    public static new UploadResult Failure(string message) => new() { Succeeded = false, ErrorMessage = message };
}

public class DocumentService : IDocumentService
{
    public const string NotFoundMessage = "Document not found or you do not have access.";
    public const string SizeLimitMessage = "File exceeds the 25 MB limit.";
    public const string TeamLeadRole = "TeamLead";

    private const int MaxTagLength = 50;

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;
    private readonly IFileScanService _scanner;
    private readonly INotificationService _notifications;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService storage,
        IFileScanService scanner,
        INotificationService notifications,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _storage = storage;
        _scanner = scanner;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Upload

    public async Task<UploadResult> UploadAsync(DocumentUploadRequest request, Stream content, int requestingUserId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == requestingUserId);
        if (user == null)
        {
            return UploadResult.Failure("User not found.");
        }

        var metadataError = ValidateMetadata(request.Title, request.Description, request.Category);
        if (metadataError != null)
        {
            return UploadResult.Failure(metadataError);
        }

        await using var buffered = await EnsureSeekableAsync(content);
        var sizeError = ValidateSize(buffered.Length);
        if (sizeError != null)
        {
            return UploadResult.Failure(sizeError);
        }

        var scan = await _scanner.ScanAsync(buffered, request.OriginalFileName);
        if (!scan.IsClean)
        {
            return UploadResult.Failure(scan.Reason ?? ContentTypeScanService.RejectionMessage);
        }

        Project? project = null;
        if (request.ProjectId.HasValue)
        {
            project = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId.Value);

            if (project == null || !IsProjectMember(project, requestingUserId))
            {
                return UploadResult.Failure("You can only upload documents to projects you belong to.");
            }
        }

        var storageKey = BuildStorageKey(requestingUserId, request.ProjectId, request.OriginalFileName);

        // Sequence: save file first, then metadata; compensate if the DB insert fails
        try
        {
            buffered.Position = 0;
            await _storage.UploadAsync(buffered, storageKey, scan.MimeType!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store uploaded file for user {UserId}", requestingUserId);
            return UploadResult.Failure("The file could not be saved. Please try again.");
        }

        var now = DateTime.UtcNow;
        var document = new Document
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Category = request.Category,
            ProjectId = request.ProjectId,
            UploadedByUserId = requestingUserId,
            OriginalFileName = Path.GetFileName(request.OriginalFileName),
            FilePath = storageKey,
            FileType = scan.MimeType!,
            FileSizeBytes = buffered.Length,
            UploadedDate = now,
            UpdatedDate = now
        };
        foreach (var tag in NormalizeTags(request.Tags))
        {
            document.Tags.Add(new DocumentTag { Tag = tag });
        }

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document metadata; removing stored file {StorageKey}", storageKey);
            _context.Entry(document).State = EntityState.Detached;
            await _storage.DeleteAsync(storageKey);
            return UploadResult.Failure("The document could not be saved. Please try again.");
        }

        await LogActivityAsync(document, requestingUserId, DocumentActions.Upload);

        if (project != null)
        {
            await NotifyProjectMembersAsync(project, document, user);
        }

        return UploadResult.Success(document.DocumentId);
    }

    // ---------------------------------------------------------------- Queries

    public async Task<List<DocumentListItem>> GetMyDocumentsAsync(int userId, DocumentQuery query)
    {
        var context = await GetUserContextAsync(userId);
        if (context == null)
        {
            return new List<DocumentListItem>();
        }

        var documents = _context.Documents.Where(d => d.UploadedByUserId == userId);

        if (!string.IsNullOrEmpty(query.Category))
        {
            documents = documents.Where(d => d.Category == query.Category);
        }
        if (query.ProjectId.HasValue)
        {
            documents = documents.Where(d => d.ProjectId == query.ProjectId.Value);
        }
        if (query.FromDate.HasValue)
        {
            var from = query.FromDate.Value.Date;
            documents = documents.Where(d => d.UploadedDate >= from);
        }
        if (query.ToDate.HasValue)
        {
            var toExclusive = query.ToDate.Value.Date.AddDays(1);
            documents = documents.Where(d => d.UploadedDate < toExclusive);
        }

        documents = (query.SortBy, query.Descending) switch
        {
            (DocumentSortField.Title, false) => documents.OrderBy(d => d.Title),
            (DocumentSortField.Title, true) => documents.OrderByDescending(d => d.Title),
            (DocumentSortField.Category, false) => documents.OrderBy(d => d.Category).ThenBy(d => d.Title),
            (DocumentSortField.Category, true) => documents.OrderByDescending(d => d.Category).ThenBy(d => d.Title),
            (DocumentSortField.FileSize, false) => documents.OrderBy(d => d.FileSizeBytes),
            (DocumentSortField.FileSize, true) => documents.OrderByDescending(d => d.FileSizeBytes),
            (_, false) => documents.OrderBy(d => d.UploadedDate),
            _ => documents.OrderByDescending(d => d.UploadedDate)
        };

        return await ToListItemsAsync(documents, context);
    }

    public async Task<List<DocumentListItem>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return new List<DocumentListItem>();
        }

        if (!context.IsAdministrator && !context.ProjectIds.Contains(projectId))
        {
            return new List<DocumentListItem>();
        }

        var documents = _context.Documents
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.UploadedDate);

        return await ToListItemsAsync(documents, context);
    }

    public async Task<List<DocumentListItem>> SearchAsync(string term, int requestingUserId)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new List<DocumentListItem>();
        }

        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return new List<DocumentListItem>();
        }

        var pattern = $"%{EscapeLikePattern(term.Trim())}%";
        var documents = AccessibleDocuments(context)
            .Where(d =>
                EF.Functions.Like(d.Title, pattern, "\\")
                || (d.Description != null && EF.Functions.Like(d.Description, pattern, "\\"))
                || d.Tags.Any(t => EF.Functions.Like(t.Tag, pattern, "\\"))
                || EF.Functions.Like(d.UploadedBy.DisplayName, pattern, "\\")
                || (d.Project != null && EF.Functions.Like(d.Project.Name, pattern, "\\")))
            .OrderByDescending(d => d.UploadedDate);

        return await ToListItemsAsync(documents, context);
    }

    public async Task<DocumentDetails?> GetByIdAsync(int documentId, int requestingUserId)
    {
        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return null;
        }

        var document = await AccessibleDocuments(context)
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Include(d => d.Project)
            .Include(d => d.Tags)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return null;
        }

        var details = new DocumentDetails
        {
            Description = document.Description,
            OriginalFileName = document.OriginalFileName,
            Tags = document.Tags.Select(t => t.Tag).OrderBy(t => t).ToList()
        };
        FillListItem(details, document, context);
        return details;
    }

    // ---------------------------------------------------------------- File access

    public async Task<DocumentFile?> OpenForDownloadAsync(int documentId, int requestingUserId, bool previewOnly)
    {
        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return null;
        }

        var document = await AccessibleDocuments(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null)
        {
            return null;
        }

        if (previewOnly && !DocumentCategories.PreviewableTypes.Contains(document.FileType))
        {
            return null;
        }

        Stream stream;
        try
        {
            stream = await _storage.DownloadAsync(document.FilePath);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("Stored file missing for document {DocumentId}", documentId);
            return null;
        }

        await LogActivityAsync(document, requestingUserId, DocumentActions.Download);

        return new DocumentFile
        {
            Content = stream,
            ContentType = document.FileType,
            FileName = document.OriginalFileName
        };
    }

    // ---------------------------------------------------------------- Management

    public async Task<OperationResult> UpdateMetadataAsync(int documentId, DocumentMetadataUpdate update, int requestingUserId)
    {
        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return OperationResult.Failure(NotFoundMessage);
        }

        var document = await _context.Documents
            .Include(d => d.Project)
            .Include(d => d.Tags)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null || !CanEdit(document, context))
        {
            return OperationResult.Failure(NotFoundMessage);
        }

        var metadataError = ValidateMetadata(update.Title, update.Description, update.Category);
        if (metadataError != null)
        {
            return OperationResult.Failure(metadataError);
        }

        document.Title = update.Title.Trim();
        document.Description = string.IsNullOrWhiteSpace(update.Description) ? null : update.Description.Trim();
        document.Category = update.Category;
        document.UpdatedDate = DateTime.UtcNow;

        var newTags = NormalizeTags(update.Tags);
        foreach (var existing in document.Tags.Where(t => !newTags.Contains(t.Tag)).ToList())
        {
            document.Tags.Remove(existing);
            _context.DocumentTags.Remove(existing);
        }
        foreach (var tag in newTags.Where(t => document.Tags.All(existing => existing.Tag != t)))
        {
            document.Tags.Add(new DocumentTag { Tag = tag });
        }

        await _context.SaveChangesAsync();
        await LogActivityAsync(document, requestingUserId, DocumentActions.EditMetadata);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ReplaceFileAsync(int documentId, string originalFileName, Stream content, int requestingUserId)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.DocumentId == documentId);
        if (document == null || document.UploadedByUserId != requestingUserId)
        {
            return OperationResult.Failure(NotFoundMessage);
        }

        await using var buffered = await EnsureSeekableAsync(content);
        var sizeError = ValidateSize(buffered.Length);
        if (sizeError != null)
        {
            return OperationResult.Failure(sizeError);
        }

        var scan = await _scanner.ScanAsync(buffered, originalFileName);
        if (!scan.IsClean)
        {
            return OperationResult.Failure(scan.Reason ?? ContentTypeScanService.RejectionMessage);
        }

        var newKey = BuildStorageKey(document.UploadedByUserId, document.ProjectId, originalFileName);
        try
        {
            buffered.Position = 0;
            await _storage.UploadAsync(buffered, newKey, scan.MimeType!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store replacement file for document {DocumentId}", documentId);
            return OperationResult.Failure("The file could not be saved. Please try again.");
        }

        var oldKey = document.FilePath;
        document.FilePath = newKey;
        document.OriginalFileName = Path.GetFileName(originalFileName);
        document.FileType = scan.MimeType!;
        document.FileSizeBytes = buffered.Length;
        document.UpdatedDate = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document {DocumentId}; removing replacement file", documentId);
            await _context.Entry(document).ReloadAsync();
            await _storage.DeleteAsync(newKey);
            return OperationResult.Failure("The document could not be updated. Please try again.");
        }

        await _storage.DeleteAsync(oldKey);
        await LogActivityAsync(document, requestingUserId, DocumentActions.ReplaceFile);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAsync(int documentId, int requestingUserId)
    {
        var context = await GetUserContextAsync(requestingUserId);
        if (context == null)
        {
            return OperationResult.Failure(NotFoundMessage);
        }

        var document = await _context.Documents
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null || !CanDelete(document, context))
        {
            return OperationResult.Failure(NotFoundMessage);
        }

        var storageKey = document.FilePath;
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        await _storage.DeleteAsync(storageKey);
        await LogActivityAsync(document, requestingUserId, DocumentActions.Delete);
        return OperationResult.Success();
    }

    // ---------------------------------------------------------------- Authorization

    private sealed class UserContext
    {
        public int UserId { get; init; }
        public bool IsAdministrator { get; init; }
        public HashSet<int> ProjectIds { get; init; } = new();
        public HashSet<int> ManagedProjectIds { get; init; } = new();
        public HashSet<int> LedUserIds { get; init; } = new();
    }

    private async Task<UserContext?> GetUserContextAsync(int userId)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
        {
            return null;
        }

        var managedProjectIds = await _context.Projects
            .Where(p => p.ProjectManagerId == userId)
            .Select(p => p.ProjectId)
            .ToListAsync();

        var memberProjectIds = await _context.ProjectMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ProjectId)
            .ToListAsync();

        var leadProjectIds = await _context.ProjectMembers
            .Where(m => m.UserId == userId && m.Role == TeamLeadRole)
            .Select(m => m.ProjectId)
            .ToListAsync();

        var ledUserIds = await _context.ProjectMembers
            .Where(m => leadProjectIds.Contains(m.ProjectId) && m.UserId != userId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();

        return new UserContext
        {
            UserId = userId,
            IsAdministrator = user.Role == UserRole.Administrator,
            ProjectIds = managedProjectIds.Concat(memberProjectIds).ToHashSet(),
            ManagedProjectIds = managedProjectIds.ToHashSet(),
            LedUserIds = ledUserIds.ToHashSet()
        };
    }

    /// <summary>
    /// Documents the user may view: own uploads, documents of their projects, documents shared
    /// with them directly or with one of their project teams, and (as a project Team Lead)
    /// documents uploaded by members of projects they lead. Administrators see everything.
    /// </summary>
    private IQueryable<Document> AccessibleDocuments(UserContext context)
    {
        if (context.IsAdministrator)
        {
            return _context.Documents;
        }

        var userId = context.UserId;
        var projectIds = context.ProjectIds.ToList();
        var ledUserIds = context.LedUserIds.ToList();

        return _context.Documents.Where(d =>
            d.UploadedByUserId == userId
            || (d.ProjectId != null && projectIds.Contains(d.ProjectId.Value))
            || d.Shares.Any(s => s.SharedWithUserId == userId
                || (s.SharedWithProjectId != null && projectIds.Contains(s.SharedWithProjectId.Value)))
            || ledUserIds.Contains(d.UploadedByUserId));
    }

    private static bool CanEdit(Document document, UserContext context) =>
        context.IsAdministrator
        || document.UploadedByUserId == context.UserId
        || context.LedUserIds.Contains(document.UploadedByUserId);

    private static bool CanDelete(Document document, UserContext context) =>
        CanEdit(document, context)
        || (document.ProjectId.HasValue && context.ManagedProjectIds.Contains(document.ProjectId.Value));

    private static bool IsProjectMember(Project project, int userId) =>
        project.ProjectManagerId == userId || project.ProjectMembers.Any(m => m.UserId == userId);

    // ---------------------------------------------------------------- Helpers

    private async Task<List<DocumentListItem>> ToListItemsAsync(IQueryable<Document> documents, UserContext context)
    {
        var results = await documents
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .Include(d => d.Project)
            .ToListAsync();

        return results.Select(d =>
        {
            var item = new DocumentListItem();
            FillListItem(item, d, context);
            return item;
        }).ToList();
    }

    private static void FillListItem(DocumentListItem item, Document document, UserContext context)
    {
        item.DocumentId = document.DocumentId;
        item.Title = document.Title;
        item.Category = document.Category;
        item.UploadedDate = document.UploadedDate;
        item.FileSizeBytes = document.FileSizeBytes;
        item.FileType = document.FileType;
        item.ProjectId = document.ProjectId;
        item.ProjectName = document.Project?.Name;
        item.UploadedByUserId = document.UploadedByUserId;
        item.UploaderName = document.UploadedBy?.DisplayName ?? string.Empty;
        item.IsPreviewable = DocumentCategories.PreviewableTypes.Contains(document.FileType);
        item.CanEdit = CanEdit(document, context);
        item.CanReplace = document.UploadedByUserId == context.UserId;
        item.CanDelete = CanDelete(document, context);
        item.CanShare = document.UploadedByUserId == context.UserId;
    }

    private static string? ValidateMetadata(string? title, string? description, string? category)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Title is required.";
        }
        if (title.Trim().Length > 255)
        {
            return "Title must be 255 characters or fewer.";
        }
        if (description != null && description.Trim().Length > 2000)
        {
            return "Description must be 2000 characters or fewer.";
        }
        if (string.IsNullOrEmpty(category) || !DocumentCategories.All.Contains(category))
        {
            return "Please select a valid category.";
        }
        return null;
    }

    private static string? ValidateSize(long size)
    {
        if (size <= 0)
        {
            return "The file is empty.";
        }
        if (size > DocumentCategories.MaxFileSizeBytes)
        {
            return SizeLimitMessage;
        }
        return null;
    }

    private static HashSet<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? Enumerable.Empty<string>())
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Select(t => t.Length > MaxTagLength ? t[..MaxTagLength] : t)
            .ToHashSet();

    private static string BuildStorageKey(int userId, int? projectId, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var folder = projectId?.ToString() ?? "personal";
        return $"{userId}/{folder}/{Guid.NewGuid():N}{extension}";
    }

    private static string EscapeLikePattern(string term) =>
        term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static async Task<Stream> EnsureSeekableAsync(Stream content)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
            return new NonDisposingStream(content);
        }

        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        buffer.Position = 0;
        return buffer;
    }

    private async Task LogActivityAsync(Document document, int userId, string action)
    {
        try
        {
            _context.DocumentActivities.Add(new DocumentActivity
            {
                DocumentId = document.DocumentId,
                DocumentTitle = document.Title,
                FileType = document.FileType,
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log {Action} for document {DocumentId}", action, document.DocumentId);
        }
    }

    private async Task NotifyProjectMembersAsync(Project project, Document document, User uploader)
    {
        var recipients = project.ProjectMembers.Select(m => m.UserId)
            .Append(project.ProjectManagerId)
            .Where(id => id != uploader.UserId)
            .Distinct();

        foreach (var recipientId in recipients)
        {
            await _notifications.CreateNotificationAsync(new Notification
            {
                UserId = recipientId,
                Title = "New project document",
                Message = $"{uploader.DisplayName} added \"{document.Title}\" to {project.Name}.",
                Type = NotificationType.DocumentAdded,
                Priority = NotificationPriority.Informational,
                CreatedDate = DateTime.UtcNow
            });
        }
    }

    /// <summary>Wraps a caller-owned stream so disposing the wrapper leaves it open.</summary>
    private sealed class NonDisposingStream : Stream
    {
        private readonly Stream _inner;
        public NonDisposingStream(Stream inner) => _inner = inner;
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
