# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

## Summary

Add secure document upload, browsing, search, sharing, and administration to ContosoDashboard.
Users upload one or more files (PDF, Office, text, JPEG/PNG ≤ 25 MB) with metadata; documents can be
associated with projects and tasks, shared with users or project teams, previewed, edited, replaced,
deleted, and audited. Technically: new EF Core entities on SQLite, a `DocumentService` that enforces
all authorization at the service layer, swappable `IFileStorageService` (local filesystem outside
`wwwroot`) and `IFileScanService` (offline content-signature validation), two authorized minimal-API
endpoints for download/preview, new Blazor pages/components, and a new xUnit test project.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)  
**Primary Dependencies**: ASP.NET Core Blazor Server + Razor Pages, EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`), Bootstrap 5.3; tests: xUnit, `Microsoft.NET.Test.Sdk`, `Microsoft.Data.Sqlite` (in-memory)  
**Storage**: SQLite (`ContosoDashboard.db`) for metadata; local filesystem `AppData/uploads` (configurable `FileStorage:RootPath`) for file content  
**Testing**: xUnit in new `ContosoDashboard.Tests` project; SQLite in-memory + temp-dir file storage  
**Target Platform**: Local developer machine (Linux/Windows/macOS), offline  
**Project Type**: Single web application (Blazor Server) + test project  
**Performance Goals**: Upload ≤ 30 s for 25 MB; lists ≤ 2 s at 500 docs; search ≤ 2 s; preview ≤ 3 s  
**Constraints**: Offline-only; no cloud SDKs; files outside `wwwroot`; GUID storage names; no weakening of security headers; `EnsureCreated()` retained (DB reset required once)  
**Scale/Scope**: Training app: 4 seeded users, tens–hundreds of documents; ~5 new entities, 3 services, 2 endpoints, 2 new pages, 4 shared components, 3 existing pages extended

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Pre-design | Post-design |
|-----------|------|------------|-------------|
| I. Offline-First, Local-Only | No cloud services; local SQLite + filesystem; auto DB creation | PASS | PASS — local storage + offline scan (R1, R4); DB reset documented (R7) |
| II. Infrastructure Behind Abstractions | Storage and scanning via DI interfaces; services depend on interfaces | PASS | PASS — `IFileStorageService`, `IFileScanService` ([service-interfaces.md](contracts/service-interfaces.md)) |
| III. Security by Default (NON-NEGOTIABLE) | `[Authorize]` on pages/endpoints; service-level IDOR checks; validation; GUID names; headers unchanged | PASS | PASS — 404 on unauthorized, access filter in service (R5, R6), preview in new tab keeps `X-Frame-Options: DENY` |
| IV. Spec-Driven, Verifiable | Spec Kit flow; offline automated tests incl. allow/deny; zero build errors / no new warnings | PASS | PASS — test project + matrix ([quickstart.md](quickstart.md)) |
| V. Simplicity & Training Clarity | Follow Models/Data/Services/Pages layering; no speculative layers | PASS | PASS — minimal API instead of MVC controllers; no repository layer; no FTS |
| Tech constraints | .NET 10, EF Core + SQLite, Bootstrap; no high/critical vulnerable packages | PASS | PASS — only test-tooling packages added |

No violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── http-endpoints.md
│   ├── service-interfaces.md
│   └── ui-routes.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks - not created here)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs              # new
│   ├── DocumentTag.cs           # new
│   ├── DocumentShare.cs         # new
│   ├── TaskDocument.cs          # new
│   ├── DocumentActivity.cs      # new
│   ├── DocumentCategories.cs    # new (category + allowed-type constants)
│   └── Notification.cs          # extend NotificationType
├── Data/
│   └── ApplicationDbContext.cs  # add DbSets, relationships, indexes
├── Services/
│   ├── Storage/
│   │   ├── IFileStorageService.cs
│   │   └── LocalFileStorageService.cs
│   ├── Scanning/
│   │   ├── IFileScanService.cs
│   │   └── ContentTypeScanService.cs
│   ├── DocumentService.cs       # IDocumentService + DTOs + authorization
│   ├── DocumentReportService.cs # IDocumentReportService
│   └── DashboardService.cs      # add TotalDocuments
├── Pages/
│   ├── Documents.razor          # new: My Documents / Shared with Me / Search
│   ├── DocumentReports.razor    # new: Administrator reports
│   ├── ProjectDetails.razor     # extend: Documents section
│   ├── Tasks.razor              # extend: task Documents panel
│   └── Index.razor              # extend: Recent Documents + count
├── Shared/
│   ├── NavMenu.razor            # extend: links
│   └── Documents/
│       ├── DocumentUploadModal.razor
│       ├── DocumentTable.razor
│       ├── DocumentEditModal.razor
│       └── DocumentShareModal.razor
├── Program.cs                   # DI registrations + /documents/{id}/download|preview endpoints
└── appsettings.json             # FileStorage:RootPath

ContosoDashboard.Tests/          # new xUnit project
├── ContosoDashboard.Tests.csproj
├── TestDb.cs                    # SQLite in-memory context + seed helpers
├── DocumentAuthorizationTests.cs
├── DocumentUploadTests.cs
├── DocumentSharingTests.cs
├── ContentTypeScanServiceTests.cs
├── LocalFileStorageServiceTests.cs
└── DocumentReportServiceTests.cs
```

**Structure Decision**: Keep the existing single Blazor Server project and its Models/Data/Services/
Pages/Shared layering (constitution V). Infrastructure abstractions are grouped under
`Services/Storage` and `Services/Scanning`. Add one sibling test project at the repository root.
`.gitignore` should exclude `ContosoDashboard/AppData/`.

## Implementation Notes (from research)

- Upload order: validate → authorize → generate key → save file → DB transaction → compensate on
  failure → log → notify (R2).
- Use one access-filter query for lists/search and explicit checks for single-document operations;
  include the project manager as a project member (R6).
- README must be updated: new pages, storage location, DB reset step, offline scan limitation.

## Complexity Tracking

Not applicable — no constitution violations.
