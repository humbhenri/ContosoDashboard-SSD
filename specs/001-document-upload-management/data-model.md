# Data Model: Document Upload and Management

**Feature**: 001-document-upload-management | **Date**: 2026-10-20
**Persistence**: EF Core on SQLite (`ApplicationDbContext`); integer keys for consistency with
existing `User`/`Project` tables.

## New Entities

### Document

| Field | Type | Rules |
|-------|------|-------|
| DocumentId | int, PK | Identity |
| Title | string(255) | Required, trimmed, non-empty; defaults to file name without extension (FR-001a) |
| Description | string(2000)? | Optional |
| Category | string(50) | Required; one of `DocumentCategories.All` (FR-005); stored as text |
| ProjectId | int?, FK → Project | Optional; uploader must be member/manager of the project (FR-022) |
| UploadedByUserId | int, FK → User | Required; set by system |
| OriginalFileName | string(255) | Required; display/download name only, never used in paths |
| FilePath | string(500) | Required, unique; system-generated key `{userId}/{projectId|personal}/{guid}.{ext}` (FR-010) |
| FileType | string(255) | Required; MIME type derived from validated extension (FR-006) |
| FileSizeBytes | long | Required; 1 ≤ size ≤ 26,214,400 (25 MB) (FR-003) |
| UploadedDate | DateTime (UTC) | Set by system |
| UpdatedDate | DateTime (UTC) | Updated on metadata edit or file replacement |

Indexes: `UploadedByUserId`, `ProjectId`, `UploadedDate`, unique `FilePath`.
Relationships: many-to-one `User` (Restrict delete), many-to-one `Project` (Restrict delete);
one-to-many `DocumentTag`, `DocumentShare`, `TaskDocument` (Cascade delete).

### DocumentTag

| Field | Type | Rules |
|-------|------|-------|
| DocumentTagId | int, PK | |
| DocumentId | int, FK → Document | Cascade delete |
| Tag | string(50) | Required; trimmed, lower-cased; unique per document |

Indexes: `Tag`, unique (`DocumentId`, `Tag`).

### DocumentShare

| Field | Type | Rules |
|-------|------|-------|
| DocumentShareId | int, PK | |
| DocumentId | int, FK → Document | Cascade delete |
| SharedWithUserId | int?, FK → User | Exactly one of `SharedWithUserId` / `SharedWithProjectId` is set |
| SharedWithProjectId | int?, FK → Project | Team share = all current members + manager of that project |
| SharedByUserId | int, FK → User | Owner who shared |
| SharedDate | DateTime (UTC) | Set by system |

Rules: no share with self; no duplicate (same document + same target); team share only to projects
the sharer belongs to (FR-026). Revocation deletes the row (FR-027a).
Indexes: `SharedWithUserId`, `SharedWithProjectId`, unique (`DocumentId`, `SharedWithUserId`),
unique (`DocumentId`, `SharedWithProjectId`).

### TaskDocument

| Field | Type | Rules |
|-------|------|-------|
| TaskId | int, FK → TaskItem | Composite PK part; Cascade delete |
| DocumentId | int, FK → Document | Composite PK part; Cascade delete |
| AttachedByUserId | int, FK → User | |
| AttachedDate | DateTime (UTC) | |

Rules: the document's `ProjectId` MUST equal the task's `ProjectId`; uploading from a task sets the
document's project automatically (FR-029). Attaching requires access to both task and document.

### DocumentActivity

| Field | Type | Rules |
|-------|------|-------|
| DocumentActivityId | int, PK | |
| DocumentId | int | No FK (history survives deletion) |
| DocumentTitle | string(255) | Snapshot at time of action |
| FileType | string(255) | Snapshot (used by "most uploaded types" report) |
| UserId | int, FK → User | Actor |
| Action | string(30) | `Upload`, `Download`, `EditMetadata`, `ReplaceFile`, `Delete`, `Share`, `RevokeShare` |
| Timestamp | DateTime (UTC) | Set by system |

Indexes: (`Action`, `Timestamp`), `UserId`, `DocumentId`.

## Constants

- `DocumentCategories.All` = Project Documents, Team Resources, Personal Files, Reports,
  Presentations, Other.
- Allowed extensions → MIME: `.pdf` application/pdf; `.doc` application/msword; `.docx`
  application/vnd.openxmlformats-officedocument.wordprocessingml.document; `.xls`
  application/vnd.ms-excel; `.xlsx` application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;
  `.ppt` application/vnd.ms-powerpoint; `.pptx`
  application/vnd.openxmlformats-officedocument.presentationml.presentation; `.txt` text/plain;
  `.jpg`/`.jpeg` image/jpeg; `.png` image/png.
- Previewable types: application/pdf, image/jpeg, image/png.
- Max size: 25 MB (26,214,400 bytes).

## Changes to Existing Entities

- `NotificationType`: append `DocumentShared`, `DocumentAdded` (existing values unchanged).
- `ApplicationDbContext`: add `DbSet`s for the five new entities, relationships, and indexes above.
- `DashboardSummary`: add `TotalDocuments` (count of documents uploaded by the user).
- No changes to `User`, `Project`, `ProjectMember`, `TaskItem` columns. Team Lead rights use the
  existing `ProjectMember.Role == "TeamLead"`.

## Lifecycle / State

- **Document**: Created (after scan + file save + DB insert) → Updated (metadata edit / file
  replace; old file deleted after new one saved and DB updated) → Deleted (DB rows removed in a
  transaction, then file deleted; shares and task attachments cascade). No soft delete.
- **DocumentShare**: Created → Revoked (row deleted). Team-share access is evaluated at request time
  against current project membership.
