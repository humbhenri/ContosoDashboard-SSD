# Contract: HTTP File Endpoints

Files live outside `wwwroot`; these are the only ways to retrieve file bytes. Both endpoints require
an authenticated cookie session (`RequireAuthorization()`), and per-document authorization is
enforced by `IDocumentService` (see [service-interfaces.md](service-interfaces.md)).

## GET `/documents/{documentId:int}/download`

| Case | Response |
|------|----------|
| Authorized, file exists | `200 OK`, body = file bytes, `Content-Type` = stored `FileType`, `Content-Disposition: attachment; filename="<OriginalFileName>"` (RFC 5987 encoded) |
| Not authenticated | `302` redirect to `/login` |
| Document missing **or** caller not authorized | `404 Not Found` (no existence disclosure) |

Side effect: a `Download` activity record is written (FR-031).

## GET `/documents/{documentId:int}/preview`

| Case | Response |
|------|----------|
| Authorized and `FileType` ∈ {application/pdf, image/jpeg, image/png} | `200 OK`, `Content-Disposition: inline; filename="<OriginalFileName>"` |
| Authorized but type not previewable | `404 Not Found` (UI shows Download instead) |
| Not authenticated | `302` redirect to `/login` |
| Missing or not authorized | `404 Not Found` |

Side effect: a `Download` activity record is written (preview counts as access).

## Common rules

- Responses keep the global security headers (`X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, CSP). The UI opens previews in a new tab (`target="_blank"`,
  `rel="noopener"`), so no framing is required.
- No query parameters accept paths or file names; only the integer id is accepted.
