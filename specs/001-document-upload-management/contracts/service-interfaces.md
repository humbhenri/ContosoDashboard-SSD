# Contract: Service Interfaces

Signatures are the design contract for implementation and tests. All methods taking
`requestingUserId` MUST enforce the authorization rules in [research.md](../research.md) (R6)
and return `null`/`false`/failure results (never throw for "not allowed") so pages can show
"not found or access denied".

## IFileStorageService (infrastructure, swappable)

```csharp
public interface IFileStorageService
{
    // storageKey is system-generated ({userId}/{projectId|personal}/{guid}.{ext}); returns the key.
    Task<string> UploadAsync(Stream fileStream, string storageKey, string contentType);
    Task DeleteAsync(string storageKey);              // no-op if missing
    Task<Stream> DownloadAsync(string storageKey);    // throws FileNotFoundException if missing
    Task<string> GetUrlAsync(string storageKey, TimeSpan expiration); // local: app download route
}
```

Implementations: `LocalFileStorageService` (training). Future: `AzureBlobStorageService`.

## IFileScanService (infrastructure, swappable)

```csharp
public interface IFileScanService
{
    // Returns Clean, or Rejected with a user-facing reason. Stream position is restored.
    Task<FileScanResult> ScanAsync(Stream content, string originalFileName);
}
public record FileScanResult(bool IsClean, string? Reason, string? MimeType);
```

Implementations: `ContentTypeScanService` (training; extension allow-list + signature check).

## IDocumentService (business logic + authorization)

```csharp
public interface IDocumentService
{
    // Upload (one file per call; UI loops for multi-file). Validates size/type/scan, authorizes
    // project, saves file, inserts rows, logs, notifies project members. Optional taskId attaches it.
    Task<UploadResult> UploadAsync(DocumentUploadRequest request, Stream content, int requestingUserId);

    // Queries (all filtered by access rules)
    Task<List<DocumentListItem>> GetMyDocumentsAsync(int userId, DocumentQuery query);
    Task<List<DocumentListItem>> GetSharedWithMeAsync(int userId);
    Task<List<DocumentListItem>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<List<DocumentListItem>> GetTaskDocumentsAsync(int taskId, int requestingUserId);
    Task<List<DocumentListItem>> SearchAsync(string term, int requestingUserId);
    Task<List<DocumentListItem>> GetRecentAsync(int userId, int count = 5);
    Task<int> GetUploadedCountAsync(int userId);
    Task<DocumentDetails?> GetByIdAsync(int documentId, int requestingUserId);

    // File access for endpoints (logs Download). Null when missing or not authorized.
    Task<DocumentFile?> OpenForDownloadAsync(int documentId, int requestingUserId, bool previewOnly);

    // Management
    Task<OperationResult> UpdateMetadataAsync(int documentId, DocumentMetadataUpdate update, int requestingUserId);
    Task<OperationResult> ReplaceFileAsync(int documentId, string originalFileName, Stream content, int requestingUserId);
    Task<OperationResult> DeleteAsync(int documentId, int requestingUserId);
    Task<OperationResult> AttachToTaskAsync(int documentId, int taskId, int requestingUserId);

    // Sharing
    Task<OperationResult> ShareWithUserAsync(int documentId, int targetUserId, int requestingUserId);
    Task<OperationResult> ShareWithProjectTeamAsync(int documentId, int projectId, int requestingUserId);
    Task<OperationResult> RevokeShareAsync(int documentShareId, int requestingUserId);
    Task<List<DocumentShareInfo>> GetSharesAsync(int documentId, int requestingUserId);
}
```

### DTOs (shape only)

- `DocumentUploadRequest`: `OriginalFileName`, `FileSizeBytes`, `Title`, `Description?`, `Category`,
  `ProjectId?`, `Tags` (list), `TaskId?`.
- `DocumentQuery`: `Category?`, `ProjectId?`, `FromDate?`, `ToDate?`, `SortBy`
  (Title | UploadedDate | Category | FileSize), `Descending`.
- `DocumentListItem`: `DocumentId`, `Title`, `Category`, `UploadedDate`, `FileSizeBytes`,
  `ProjectName?`, `UploaderName`, `FileType`, `IsPreviewable`, `CanEdit`, `CanDelete`, `CanShare`.
- `DocumentDetails`: list item fields + `Description`, `Tags`, `OriginalFileName`.
- `DocumentFile`: `Stream Content`, `ContentType`, `FileName`.
- `UploadResult` / `OperationResult`: `Succeeded`, `ErrorMessage?`, (`DocumentId?` for upload).

## IDocumentReportService (Administrator only)

```csharp
public interface IDocumentReportService
{
    // Returns null when requesting user is not an Administrator.
    Task<DocumentReport?> GetReportAsync(DateTime fromUtc, DateTime toUtc, int requestingUserId);
}
// DocumentReport: TopFileTypes (type, uploadCount), TopUploaders (user, uploadCount),
//                 TopDownloadedDocuments (title, downloadCount), DownloadsPerUser (user, count)
```

## Changes to existing services

- `IDashboardService.GetDashboardSummaryAsync` → `DashboardSummary.TotalDocuments` populated.
- `INotificationService` unchanged; used with new `NotificationType` values.
