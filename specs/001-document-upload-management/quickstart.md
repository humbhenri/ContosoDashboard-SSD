# Quickstart: Validate Document Upload and Management

Run from the repository root on a machine with the .NET 10 SDK. No network access is needed after
`dotnet restore`.

## 1. Prerequisites and reset

```bash
dotnet --version                     # 10.x
rm -f ContosoDashboard/ContosoDashboard.db   # EnsureCreated() won't add new tables to an old DB
rm -rf ContosoDashboard/AppData/uploads      # optional: clear previously stored files
```

## 2. Build and automated tests

```bash
dotnet build ContosoDashboard/ContosoDashboard.csproj        # expect 0 errors, no new warnings
dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
```

Expected: all tests pass, covering at least:
- Authorization allow/deny matrix for view, edit, replace, delete, share, revoke (all four roles,
  project TeamLead role, non-members) — [service-interfaces.md](contracts/service-interfaces.md).
- Upload rejects >25 MB, disallowed extensions, and renamed executables (signature mismatch).
- Upload atomicity: simulated DB failure leaves no file; simulated storage failure leaves no row.
- Delete removes file, shares, task attachments; activity history remains.
- Report aggregates respect date range.

## 3. Run the app

```bash
dotnet run --project ContosoDashboard/ContosoDashboard.csproj
```

Open the URL printed in the console and log in via `/login`.

## 4. Manual scenarios (map to spec user stories)

| # | Steps | Expected |
|---|-------|----------|
| US1 | As **Ni Kang**: `/documents` → Upload → select 3 files (PDF, PNG, DOCX) → set category once → edit one title → Submit | Progress per file; 3 success messages; rows show title, category, date, size, project; sort/filter work; download returns identical bytes |
| US1-neg | Upload a 26 MB file; upload `test.exe` renamed to `test.pdf` | Size error; invalid-file error; nothing stored under `AppData/uploads` |
| US2 | As **Ni Kang**: upload to "ContosoDashboard Development"; log in as **Floris Kregel** | Notification received; document visible on project page and downloadable |
| US2-neg | As a user who is not a member of that project (and not Administrator), request `/documents/{id}/download` | `404` |
| US3 | Search by a tag and by uploader name | Only accessible matching documents returned |
| US4 | As uploader: edit metadata, replace file, delete with confirmation | Changes visible; old file gone from disk after replace; deleted document gone everywhere |
| US5 | As **Ni Kang** share personal doc with **Camille Nicole**; revoke | Camille notified and sees it in Shared with Me; after revoke, `404` on download |
| US6 | From `/tasks`, open a task's Documents → upload | Document attached to task and assigned to the task's project; appears in dashboard Recent Documents and count |
| US7 | Preview a PDF and a PNG | Opens in new tab inline; DOCX shows Download only |
| US8 | As **System Administrator** open `/documents/reports`, pick today | Upload/download counts reflect the steps above; non-admins redirected/denied |

## 5. Security spot checks

- Files exist only under `ContosoDashboard/AppData/uploads/{userId}/{projectId|personal}/{guid}.{ext}`;
  no original file names on disk.
- `/AppData/uploads/...` is not served as a static file (404).
- Response headers on download still include `X-Frame-Options: DENY` and `X-Content-Type-Options: nosniff`.
