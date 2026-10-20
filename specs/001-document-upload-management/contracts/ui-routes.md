# Contract: UI Routes and Components

All pages use `@attribute [Authorize]` and obtain the user id from the `NameIdentifier` claim.

| Route / Location | Component | Content | Spec |
|------------------|-----------|---------|------|
| `/documents` | `Pages/Documents.razor` | Tabs: **My Documents** (table, sort, filters), **Shared with Me**, **Search**; "Upload" button opens upload modal | US1, US3, US5 |
| `/documents/reports` | `Pages/DocumentReports.razor` | `[Authorize(Policy = "Administrator")]`; date range; 4 report tables | US8 |
| `/projects/{id}` | `Pages/ProjectDetails.razor` (existing) | New **Documents** section: list, download/preview, upload to this project | US2 |
| `/tasks` | `Pages/Tasks.razor` (existing) | Per-task "Documents" action opens panel: list, attach existing, upload (project auto-set) | US6 |
| `/` | `Pages/Index.razor` (existing) | **Recent Documents** widget (5) + **Documents** summary card | US6 |
| Nav | `Shared/NavMenu.razor` | "Documents" link; "Document Reports" link for Administrators | — |

## Shared components (`Shared/Documents/`)

- `DocumentUploadModal.razor` — `InputFile multiple` (`@key` reset), shared fields (category, project,
  tags, description) prefilling each file row; per-file title (defaults to file name), per-file
  progress and success/error message. Parameters: `FixedProjectId?`, `TaskId?`, `OnUploaded`.
  Upload button reachable in ≤ 3 clicks from `/documents` (SC-001).
- `DocumentTable.razor` — columns: title, category, upload date, size, project; actions shown per
  `CanEdit`/`CanDelete`/`CanShare`/`IsPreviewable`. Download/preview are plain links to the
  [HTTP endpoints](http-endpoints.md) (preview `target="_blank"`).
- `DocumentEditModal.razor` — edit metadata, replace file (uploader only).
- `DocumentShareModal.razor` — share with user or with a project team; list current shares with
  Revoke.
- Delete uses a confirmation dialog before calling `DeleteAsync`.

## Messages

- Size: "File exceeds the 25 MB limit."
- Type/scan: "Unsupported or invalid file. Allowed: PDF, Word, Excel, PowerPoint, text, JPEG, PNG."
- Access: "Document not found or you do not have access."
