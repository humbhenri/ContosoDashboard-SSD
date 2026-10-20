# Feature Specification: Document Upload and Management

**Feature Branch**: `001-document-upload-management`  
**Created**: 2026-10-20  
**Status**: Draft  
**Input**: User description: "--file StakeholderDocs/document-upload-and-management-feature.md" (Document Upload and Management Feature - Requirements)

## Clarifications

### Session 2026-10-20

- Q: How should malware scanning work in the offline training environment? → A: Mandatory,
  replaceable scan step; offline it validates actual file content against the declared allowed type
  (no real antivirus); a production scanner can be substituted later.
- Q: What does "team" mean for team sharing and Team Lead permissions? → A: A team is the set of
  members of a project; sharing "with a team" targets a project's members, and a Team Lead manages
  documents uploaded by members of projects where they hold the TeamLead project role.
- Q: Should Administrators be read-only or able to edit/delete any document? → A: Full management:
  Administrators can view, edit metadata, and delete any document; all actions are logged.
- Q: In multi-file uploads, is metadata shared or per file? → A: Shared details (category, project,
  tags, description) prefill every file; each file's title defaults to its file name; any file's
  details can be edited individually before submitting.
- Q: Can owners revoke shares? → A: Yes. Owners and Administrators can revoke any share; the
  recipient loses access immediately; the revocation is logged; no notification is sent.
- Q: What should the "document access patterns" report show? → A: Most-downloaded documents and
  download counts per user, filterable by date range.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload and view my documents (Priority: P1)

An employee uploads one or more work documents from their computer, provides a title, a category,
and optionally a description, project, and tags. The documents then appear in a "My Documents" list
where the employee can sort and filter them and download any of them.

**Why this priority**: This is the core value of the feature: a single, secure place to store work
documents. Without it, no other story is possible.

**Independent Test**: Log in as any user, upload a supported file with required metadata, confirm it
appears in "My Documents" with correct details, and download it with identical content.

**Acceptance Scenarios**:

1. **Given** a logged-in employee on the upload screen, **When** they select a 2 MB PDF, enter a
   title, choose the category "Reports", and submit, **Then** a progress indicator is shown, a
   success message appears, and the document is listed in "My Documents" with title, category,
   upload date, file size, and project (if any).
2. **Given** a logged-in employee, **When** they select three supported files at once and choose
   the category "Reports" once, **Then** each file is prefilled with "Reports" and a title from its
   file name; after changing one file's title and submitting, all three are stored with their
   respective details.
3. **Given** a file larger than 25 MB, **When** the employee attempts to upload it, **Then** the
   upload is rejected with a message stating the size limit and nothing is stored.
4. **Given** a file of an unsupported type (e.g., an executable), **When** the employee attempts to
   upload it, **Then** the upload is rejected with a message listing the supported types.
5. **Given** the upload form, **When** the employee clears a file's title or omits the category,
   **Then** submission is blocked and the missing fields are indicated.
6. **Given** documents in "My Documents", **When** the employee sorts by title, upload date,
   category, or file size, or filters by category, project, or date range, **Then** the list updates
   accordingly.
7. **Given** a document the employee uploaded, **When** they choose download, **Then** they receive
   the original file with its original name.

---

### User Story 2 - Project documents for team members (Priority: P2)

Project members see all documents associated with a project from the project's page, can download
them, and can add documents to projects they belong to. Project members are notified when a new
document is added to one of their projects.

**Why this priority**: Associating documents with projects addresses the stated lack of visibility
into project documents and extends value to whole teams.

**Independent Test**: As a project member, upload a document associated with a project; as another
member of the same project, open the project page and download it; as a non-member, confirm the
document is neither listed nor downloadable.

**Acceptance Scenarios**:

1. **Given** a user who is a member of Project A, **When** they open Project A's details,
   **Then** they see all documents associated with Project A and can download them.
2. **Given** a user who is not a member of Project A, **When** they try to access a Project A
   document directly (e.g., via a link), **Then** access is denied.
3. **Given** an employee assigned to Project A, **When** they upload a document and select Project A,
   **Then** the document is associated with Project A; they cannot select projects they do not
   belong to.
4. **Given** a new document added to Project A, **When** the upload completes, **Then** every other
   member of Project A receives an in-app notification.

---

### User Story 3 - Search documents (Priority: P2)

A user searches for documents by title, description, tags, uploader name, or project, and sees only
documents they are allowed to access.

**Why this priority**: Finding documents quickly is a primary success metric (locate a document in
under 30 seconds).

**Independent Test**: Upload documents with distinct titles and tags as different users; search as
one user and verify matches include only accessible documents.

**Acceptance Scenarios**:

1. **Given** accessible documents with the tag "budget", **When** the user searches "budget",
   **Then** matching documents are returned.
2. **Given** a matching document the user is not permitted to access, **When** they search,
   **Then** that document does not appear in results.
3. **Given** a search term matching nothing, **When** the user searches, **Then** an empty-state
   message is shown.

---

### User Story 4 - Manage my documents (Priority: P3)

The uploader edits a document's metadata, replaces its file with an updated version, or permanently
deletes it after confirmation. Project Managers can delete any document in their projects.

**Why this priority**: Keeps the document store accurate, but users get value before these exist.

**Independent Test**: Upload a document, edit its title and tags, replace its file, then delete it
and confirm it is gone from lists, search, and download.

**Acceptance Scenarios**:

1. **Given** a document the user uploaded, **When** they edit title, description, category, or tags,
   **Then** the changes are saved and shown everywhere the document appears.
2. **Given** a document the user uploaded, **When** they replace its file with a supported file,
   **Then** subsequent downloads return the new file and the size/type/upload information updates.
3. **Given** a document the user did not upload (and is not that project's manager), **When** they
   attempt to edit, replace, or delete it, **Then** the action is denied.
4. **Given** a document the user may delete, **When** they choose delete, **Then** a confirmation is
   required, and after confirming, the document and its file are permanently removed.
5. **Given** a Project Manager of Project A, **When** they delete a document in Project A uploaded by
   someone else, **Then** the deletion succeeds.
6. **Given** an Administrator, **When** they edit metadata of or delete any user's document,
   **Then** the action succeeds and is recorded in the activity log.

---

### User Story 5 - Share documents (Priority: P3)

A document owner shares a document with specific users or with a team. Recipients get an in-app
notification and see the document in a "Shared with Me" section.

**Why this priority**: Replaces uncontrolled sharing via email/drives, but depends on upload existing.

**Independent Test**: As user A, share a personal document with user B; log in as user B and confirm
a notification and the document in "Shared with Me"; confirm user C cannot access it.

**Acceptance Scenarios**:

1. **Given** a document owned by user A, **When** A shares it with user B, **Then** B receives an
   in-app notification and the document appears in B's "Shared with Me" and is downloadable by B.
2. **Given** a document owned by user A, **When** A shares it with the team of a project A belongs
   to, **Then** every member of that project receives a notification and can access the document.
3. **Given** a user who is neither owner nor recipient nor otherwise authorized, **When** they try to
   access the shared document, **Then** access is denied.
4. **Given** a document user A shared with user B, **When** A revokes the share, **Then** B can no
   longer see or download the document, and the revocation appears in the activity log.

---

### User Story 6 - Task and dashboard integration (Priority: P4)

From a task, users see and attach related documents or upload one directly; documents attached to a
task are automatically associated with the task's project. The dashboard shows a "Recent Documents"
widget (last 5 uploaded by the user) and a document count in the summary cards.

**Why this priority**: Convenience that increases adoption, built on top of earlier stories.

**Independent Test**: Upload a document from a task in Project A, confirm it is attached to the task
and associated with Project A, and confirm it appears in the dashboard's Recent Documents widget.

**Acceptance Scenarios**:

1. **Given** a task in Project A the user can access, **When** they upload a document from the task,
   **Then** the document is attached to the task and associated with Project A.
2. **Given** a user who uploaded 7 documents, **When** they open the dashboard, **Then** the Recent
   Documents widget shows their 5 most recent uploads, newest first, and the summary shows their
   document count.

---

### User Story 7 - Preview documents (Priority: P4)

Users preview PDFs and images in the browser without downloading them.

**Why this priority**: Improves speed of finding the right document; download already covers access.

**Independent Test**: Upload a PDF and a PNG; open preview for each and confirm they render in the
browser; confirm other types offer download only.

**Acceptance Scenarios**:

1. **Given** an accessible PDF or JPEG/PNG document, **When** the user chooses preview, **Then** it is
   displayed in the browser.
2. **Given** an accessible Office or text document, **When** the user views it, **Then** download is
   offered instead of preview.

---

### User Story 8 - Activity log and administrator reports (Priority: P5)

All document uploads, downloads, deletions, and share actions are logged. Administrators view
reports of most uploaded document types, most active uploaders, and document access patterns.

**Why this priority**: Needed for audit and compliance, but not for day-to-day employee value.

**Independent Test**: Perform an upload, a download, a share, and a delete; as Administrator, open
reports and confirm each action is reflected; as a non-administrator, confirm reports are not
accessible.

**Acceptance Scenarios**:

1. **Given** any document action (upload, download, delete, share), **When** it completes,
   **Then** an activity record with user, document, action, and timestamp is stored.
2. **Given** an Administrator, **When** they open document reports and choose a date range,
   **Then** they see most uploaded document types, most active uploaders, most-downloaded documents,
   and download counts per user for that range.
3. **Given** a non-administrator, **When** they attempt to open document reports, **Then** access is
   denied.

---

### Edge Cases

- Upload fails midway (e.g., storage error): no document record is created and no partial file
  remains; the user sees an error and can retry.
- Two files with the same original name are uploaded by the same user: both are stored independently
  without overwriting each other.
- File names containing path characters (e.g., `../`) or unusual characters: never affect where the
  file is stored.
- A file whose extension is allowed but whose content type does not match: rejected.
- A file of exactly 25 MB is accepted; 25 MB + 1 byte is rejected.
- Multi-file upload where some files are invalid: valid files are stored; invalid files are reported
  individually.
- A user is removed from a project: they immediately lose access to that project's documents and to
  documents shared with that project's team (unless they uploaded them or they were shared directly),
  and a Team Lead removed from a project loses Team Lead rights over its members' documents.
- A project document's uploader is not the project manager: both the uploader and the project
  manager may delete it.
- A document is deleted while shared: it disappears from all recipients' "Shared with Me" lists.
- Sharing with oneself or re-sharing with an existing recipient: no duplicate share or notification.
- Very long MIME types (e.g., Office formats) are stored without truncation.
- A file fails the malware scan: it is rejected with a clear message and never becomes accessible.

## Requirements *(mandatory)*

### Functional Requirements

**Upload & validation**

- **FR-001**: Users MUST be able to select and upload one or more files in a single action. For
  multiple files, shared details (category, project, tags, description) entered once MUST prefill
  every file, and each file's details MUST remain individually editable before submission.
- **FR-001a**: Each file's title MUST default to its original file name (without extension) and may
  be changed by the user; a title can never be empty at submission.
- **FR-002**: System MUST accept only these file types: PDF, Word, Excel, PowerPoint, plain text,
  JPEG, and PNG; all others MUST be rejected with a message listing supported types.
- **FR-003**: System MUST reject files larger than 25 MB with a message stating the limit.
- **FR-004**: System MUST require a title and a category for each upload; description, project, and
  tags are optional. Tags are free-form, user-defined keywords.
- **FR-005**: Categories MUST be chosen from: Project Documents, Team Resources, Personal Files,
  Reports, Presentations, Other.
- **FR-006**: System MUST automatically record upload date/time, uploader, file size, and file type
  (supporting type identifiers of at least 255 characters).
- **FR-007**: System MUST show upload progress and a success or error message per file.
- **FR-008**: Every upload MUST pass through a mandatory, replaceable file-safety scan step before
  the file becomes available; files that fail the scan MUST be rejected with a clear message. In
  the offline training environment, the scan MUST verify that the file's actual content matches its
  declared, allowed type (e.g., a renamed executable is rejected); real antivirus detection is
  provided by substituting a production scanner without changing other behavior.
- **FR-009**: An upload MUST be atomic: if storing the file fails, no document record is created;
  if recording the document fails, the stored file is removed.
- **FR-010**: Stored file locations MUST be system-generated and unique, and MUST NOT be derived from
  user-supplied file names.
- **FR-011**: Stored files MUST NOT be directly reachable by URL; every retrieval MUST pass an
  authorization check.

**Browsing & search**

- **FR-012**: Users MUST be able to view "My Documents" showing title, category, upload date, file
  size, and associated project.
- **FR-013**: "My Documents" MUST support sorting by title, upload date, category, and file size, and
  filtering by category, project, and date range.
- **FR-014**: The project details view MUST list all documents associated with that project to its
  members.
- **FR-015**: Users MUST be able to search by title, description, tags, uploader name, and project;
  results MUST include only documents the user may access.

**Access & management**

- **FR-016**: Users MUST be able to download any document they may access, receiving the original
  file name.
- **FR-017**: Users MUST be able to preview PDF, JPEG, and PNG documents in the browser.
- **FR-018**: The uploader MUST be able to edit title, description, category, and tags.
- **FR-019**: The uploader MUST be able to replace a document's file; the previous file is discarded
  (no version history).
- **FR-020**: The uploader, the Project Manager of the document's project, and Administrators MUST be
  able to permanently delete a document after explicit confirmation; deletion removes the file,
  record, shares, and task attachments.

**Access rules**

- **FR-021**: A document MUST be accessible only to: its uploader; members of its associated project;
  users and project teams it is shared with; a Team Lead of any project the uploader is a member of
  (see FR-023); the Project Manager of its project; and Administrators.
- **FR-022**: Employees MUST only associate uploads with projects they are members of; Project
  Managers MUST be able to upload to projects they manage.
- **FR-023**: A user holding the TeamLead role on a project MUST be able to view and manage (edit
  metadata, delete) documents uploaded by members of that project. Team Lead rights are scoped per
  project and come from the project role, not from department.
- **FR-024**: Administrators MUST be able to view, download, edit metadata of, and delete any
  document, for audit and compliance; all such actions are recorded per FR-031.
- **FR-025**: Every access rule MUST be enforced on the server for each operation, regardless of
  what the user interface displays.

**Sharing & notifications**

- **FR-026**: Document owners MUST be able to share a document with specific users or with a team.
  A "team" is the set of members of a project; owners may only share with teams of projects they
  belong to. Team membership is evaluated at access time, so later project joiners gain access and
  removed members lose it.
- **FR-027**: Recipients MUST receive an in-app notification and see the document in "Shared with Me".
- **FR-027a**: Document owners and Administrators MUST be able to revoke any existing share; the
  affected user(s) lose access immediately, the document leaves their "Shared with Me", and no
  notification is sent.
- **FR-028**: Members of a project MUST receive an in-app notification when a new document is added
  to that project (excluding the uploader).

**Integration**

- **FR-029**: Users MUST be able to view, attach, and upload documents from a task they can access;
  documents attached to a task MUST be associated with the task's project.
- **FR-030**: The dashboard MUST show a "Recent Documents" widget with the user's last 5 uploads and
  a document count in the summary cards.

**Audit**

- **FR-031**: System MUST log every upload, download, metadata edit, file replacement, deletion,
  share, and share revocation with user, document, action type, and timestamp.
- **FR-032**: Administrators MUST be able to view reports of most uploaded document types, most active
  uploaders, and document access patterns; other roles MUST NOT. "Access patterns" means the
  most-downloaded documents and download counts per user. All reports MUST be filterable by date
  range and derived from the activity records in FR-031.

**Operational**

- **FR-033**: All document features MUST work fully offline, storing files on the local machine.

### Key Entities

- **Document**: A stored file plus metadata: title, optional description, category, optional
  associated project, tags, original file name, file type, file size, upload date/time, uploader,
  and storage location. Belongs to one uploader; optionally to one project.
- **Document Tag**: A free-form keyword attached to a document to aid search; a document has zero or
  more tags.
- **Document Share**: A grant giving a specific user or a project team (all members of a project)
  access to a document, recording who shared it and when.
- **Task Document Attachment**: A link between a task and a document; the document's project matches
  the task's project.
- **Document Activity Record**: An audit entry of an action (upload, download, delete, share) by a
  user on a document at a point in time.
- Existing entities used: **User** (role), **Project** and **Project Member** (including project role),
  **Task**, **Notification**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can upload a document in no more than 3 clicks from the documents page
  (excluding typing metadata).
- **SC-002**: Uploads of files up to 25 MB complete within 30 seconds on a typical network.
- **SC-003**: Document lists load within 2 seconds with up to 500 documents.
- **SC-004**: Search results appear within 2 seconds.
- **SC-005**: Previews of PDF and image documents appear within 3 seconds.
- **SC-006**: In access testing across all four roles, 100% of unauthorized access attempts (list,
  search, download, preview, edit, delete) are denied, and zero security incidents occur.
- **SC-007**: Within 3 months of launch, 70% of active dashboard users have uploaded at least one
  document.
- **SC-008**: Average time for a user to locate a known document is under 30 seconds.
- **SC-009**: 90% of uploaded documents have a category other than "Other".

## Assumptions

- Roles and project membership come from the existing application; a "team" is the set of members
  of a project, and Team Lead rights come from holding the TeamLead role on that project.
- Employees may upload personal documents (no project) and documents for projects they are members
  of.
- Replacing a file overwrites the previous one; version history is out of scope.
- Deletion is permanent; there is no trash or recovery.
- Most documents are under 10 MB; local disk space is sufficient; no storage quotas apply.
- Notifications use the existing in-app notification mechanism; no email is sent.
- Stakeholder technical constraints (swappable storage abstraction for future cloud migration,
  integer document identifiers, text-valued categories, storage path pattern, and user-interface
  implementation guidance) are carried into the planning phase rather than this specification.
- The training environment does not detect real viruses; the scan step only validates file content
  against the declared type. Production deployments are expected to plug in a real malware scanner.

## Out of Scope

- Real-time collaborative editing; version history and rollback.
- Approval workflows or document routing.
- Integration with external systems (e.g., SharePoint, OneDrive).
- Mobile apps; document templates or generation.
- Storage quotas; soft delete/trash with recovery.
