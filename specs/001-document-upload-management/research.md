# Research: Document Upload and Management

**Feature**: 001-document-upload-management | **Date**: 2026-10-20

All Technical Context items were resolvable from the existing codebase, the constitution (v1.0.0),
the stakeholder document, and the clarified spec. No open NEEDS CLARIFICATION items remain.

## R1. File storage abstraction and location

- **Decision**: `IFileStorageService` with `UploadAsync`, `DeleteAsync`, `DownloadAsync`,
  `GetUrlAsync`; training implementation `LocalFileStorageService` writes under a configurable root
  (`FileStorage:RootPath`, default `AppData/uploads` relative to the content root, outside `wwwroot`).
  Stored key pattern: `{uploaderId}/{projectId|personal}/{guid}.{ext}` (forward slashes, relative).
- **Rationale**: Required by constitution Principle II and stakeholder doc; relative keys work
  unchanged as Azure blob names; GUID names satisfy FR-010 (no user-supplied names in paths).
- **Alternatives considered**: Storing bytes in SQLite (bloats DB, no migration path); `wwwroot`
  storage (bypasses authorization, violates FR-011).
- **Notes**: `LocalFileStorageService` MUST resolve the full path and verify it stays under the root
  (defense in depth against path traversal). `GetUrlAsync` returns the app's authorized download
  route, not a filesystem path.

## R2. Upload sequence and atomicity (FR-009)

- **Decision**: Validate → authorize → generate key → save file → insert DB rows in a transaction →
  on DB failure delete the saved file (compensating action) → log activity → send notifications.
- **Rationale**: Matches stakeholder sequence; prevents orphaned DB rows and orphaned files;
  key generated before insert prevents empty/duplicate `FilePath` values.
- **Alternatives considered**: DB-first then file (creates orphaned rows on file failure).

## R3. Blazor Server upload handling and progress (FR-001, FR-007)

- **Decision**: `InputFile` with `multiple`, `@key` reset after upload; capture name/size/content
  type first; copy `OpenReadStream(maxAllowedSize: 25 MB)` into a `MemoryStream` in chunks while
  reporting per-file progress; clear `IBrowserFile` references after copy.
- **Rationale**: Stakeholder-required pattern avoiding stream disposal/reuse errors; chunked copy
  gives a real progress value without JS interop.
- **Alternatives considered**: Separate HTTP upload endpoint with JS `fetch` (more moving parts,
  antiforgery handling); not needed for an in-process Blazor Server app.

## R4. File-safety scan (FR-008, clarified option A)

- **Decision**: `IFileScanService` (replaceable) with `ContentTypeScanService` for training. It checks
  the extension against an allow-list and verifies file signature ("magic bytes"):
  PDF `%PDF`; PNG `89 50 4E 47 0D 0A 1A 0A`; JPEG `FF D8 FF`; OOXML (.docx/.xlsx/.pptx) ZIP `50 4B 03 04`;
  legacy Office (.doc/.xls/.ppt) OLE `D0 CF 11 E0 A1 B1 1A E1`; `.txt` must contain no NUL bytes in
  the first 8 KB. The stored MIME type is derived from the validated extension, not the browser value.
- **Rationale**: Works offline with no installs; rejects renamed executables; production can register
  a real antivirus implementation (e.g., ClamAV/Defender) via DI with no other changes.
- **Alternatives considered**: ClamAV locally (install burden per student); no scan (violates spec).
- **Allowed extensions**: `.pdf .doc .docx .xls .xlsx .ppt .pptx .txt .jpg .jpeg .png` (macro-enabled
  `.docm/.xlsm/.pptm` excluded).

## R5. Download and preview endpoints (FR-011, FR-016, FR-017)

- **Decision**: Minimal API endpoints in `Program.cs`, both `.RequireAuthorization()`:
  `GET /documents/{id}/download` (attachment, original file name) and
  `GET /documents/{id}/preview` (inline; only PDF/JPEG/PNG, else 404). Each calls `IDocumentService`
  which enforces per-document authorization and logs the download. Preview opens in a new browser tab.
- **Rationale**: Files are outside `wwwroot`, so an authorized endpoint is required; Blazor components
  cannot stream file responses directly. Opening previews in a new tab avoids embedding in an
  `<iframe>`, so the existing `X-Frame-Options: DENY` and CSP need no weakening (constitution III).
- **Alternatives considered**: MVC controller (adds controller infrastructure for two routes);
  in-page `<iframe>` preview (requires relaxing framing headers).
- **Notes**: Unauthorized or missing documents both return 404 (no existence disclosure).
  `X-Content-Type-Options: nosniff` already applies; content type comes from validated metadata.

## R6. Authorization model (FR-021–FR-025, clarified team = project)

- **Decision**: All rules live in `DocumentService` (service layer), expressed as one reusable
  `IQueryable<Document>` access filter for lists/search and explicit checks for single-document ops.
  - **View**: Administrator; uploader; project member or manager of the document's project; direct
    share recipient; member/manager of a project whose team the document was shared with; a user who
    holds `ProjectMember.Role == "TeamLead"` on any project where the uploader is a member.
  - **Edit metadata**: uploader; Administrator; Team Lead (as above).
  - **Replace file**: uploader.
  - **Delete**: uploader; Project Manager of the document's project; Team Lead (as above); Administrator.
  - **Share / revoke**: owner (uploader); Administrator may revoke.
  - **Upload to project**: project member or project manager of that project.
- **Rationale**: Matches clarified spec; reuses existing `ProjectMember` and `Project.ProjectManagerId`;
  service-level enforcement satisfies IDOR protection (constitution III).
- **Alternatives considered**: ASP.NET resource-based `IAuthorizationHandler` (more indirection than
  needed for one service; can be adopted later).
- **Notes**: "Project members" includes the project's manager (`ProjectManagerId`), who is not
  necessarily in `ProjectMembers`.

## R7. Persistence on SQLite with `EnsureCreated()`

- **Decision**: Add new entities to `ApplicationDbContext`; keep `EnsureCreated()`. Because
  `EnsureCreated()` does not alter an existing database, the quickstart/README instruct deleting
  `ContosoDashboard.db` once after pulling this feature.
- **Rationale**: Constitution allows keeping `EnsureCreated()`; training DB is disposable; avoids
  introducing migrations tooling mid-course.
- **Alternatives considered**: EF Core migrations (correct for production; deferred, can be adopted
  later without model changes).
- **Notes**: SQLite `LIKE` is case-insensitive for ASCII — adequate for search. Category is stored as
  text, validated against a fixed list. `FileType` length 255.

## R8. Search and list performance (SC-003, SC-004)

- **Decision**: Server-side filtering/sorting via EF queries over the access filter; `LIKE '%term%'`
  over title, description, tags, uploader display name, and project name; indexes on
  `Document(UploadedByUserId)`, `Document(ProjectId)`, `Document(UploadedDate)`,
  `DocumentTag(Tag)`, `DocumentShare(SharedWithUserId)`, `DocumentShare(SharedWithProjectId)`.
- **Rationale**: 500 documents per list is small for SQLite; indexes keep filters well under 2 s.
- **Alternatives considered**: SQLite FTS5 (extra setup, not needed at this scale).

## R9. Notifications (FR-027, FR-028)

- **Decision**: Reuse `INotificationService.CreateNotificationAsync`; append `DocumentShared` and
  `DocumentAdded` to `NotificationType` (appended to keep existing integer values stable).
  Team shares notify each current project member/manager except the sharer.
- **Rationale**: Uses the existing in-app mechanism; no email (spec assumption).

## R10. Activity log and reports (FR-031, FR-032)

- **Decision**: `DocumentActivity` rows (action, user, document id, document title snapshot, timestamp)
  with no FK to `Document`, so history survives deletions. Reports are aggregate queries filtered by
  date range on an Administrator-only page.
- **Rationale**: Audit records must outlive the document; snapshot title keeps reports readable.

## R11. Testing approach (constitution IV)

- **Decision**: New `ContosoDashboard.Tests` xUnit project referencing the web project; tests use
  SQLite in-memory (`DataSource=:memory:` with an open connection) and a temp-directory
  `LocalFileStorageService`. Focus: authorization allow/deny matrix, upload atomicity, scan rules,
  share/revoke, delete cleanup, reports aggregation.
- **Rationale**: Offline, fast, uses the real provider (SQLite) rather than the EF in-memory provider.
- **Alternatives considered**: EF InMemory provider (does not enforce relational constraints).
