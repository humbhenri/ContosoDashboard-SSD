<!--
Sync Impact Report
- Version change: (template, unversioned) → 1.0.0
- Modified principles: all placeholders replaced (initial ratification)
  - [PRINCIPLE_1_NAME] → I. Offline-First, Local-Only Execution
  - [PRINCIPLE_2_NAME] → II. Infrastructure Behind Abstractions
  - [PRINCIPLE_3_NAME] → III. Security by Default (Defense in Depth)
  - [PRINCIPLE_4_NAME] → IV. Spec-Driven, Verifiable Changes
  - [PRINCIPLE_5_NAME] → V. Simplicity and Training Clarity
- Added sections: Technology & Platform Constraints; Development Workflow & Quality Gates
- Removed sections: none
- Templates: plan/spec/tasks templates read this file at runtime; no template edits made
- Follow-up TODOs: none
-->
# ContosoDashboard Constitution

## Core Principles

### I. Offline-First, Local-Only Execution

- The application MUST build, run, and pass its checks on a developer machine with no network
  access after `dotnet restore`.
- Features MUST NOT require cloud subscriptions, external APIs, or hosted services to function.
- Persistent data MUST use the local SQLite database; file content MUST use the local filesystem.
- Startup MUST create and seed the database automatically so a fresh clone runs with `dotnet run`.

Rationale: ContosoDashboard is a training application; offline execution maximizes availability
for students and removes cost and credential barriers.

### II. Infrastructure Behind Abstractions

- Every infrastructure dependency (file storage, email, external identity, etc.) MUST be accessed
  through an interface registered via dependency injection (e.g., `IFileStorageService`).
- Business logic in `Services/` MUST depend only on interfaces, never on concrete infrastructure.
- Swapping a local implementation for a cloud one (Azure Blob Storage, Azure SQL, Entra ID) MUST
  require only a new implementation and DI/configuration changes, with no business-logic edits.

Rationale: Preserves the documented migration path to Azure and teaches proper separation of
concerns.

### III. Security by Default (Defense in Depth) (NON-NEGOTIABLE)

- Every page or endpoint exposing user data MUST carry `[Authorize]` (with a role policy where
  applicable); anonymous access is allowed only for login/logout.
- Every service method that reads or mutates a resource MUST verify the requesting user is
  authorized for that specific resource (IDOR protection); UI-level checks alone are insufficient.
- User input MUST be validated server-side; uploaded files MUST be validated for type and size and
  stored under generated (GUID-based) names, never user-supplied paths.
- Secrets MUST NOT be committed; security headers and cookie protections MUST NOT be weakened.

Rationale: The application demonstrates secure patterns; regressions would teach the wrong lesson.

### IV. Spec-Driven, Verifiable Changes

- New features MUST follow the Spec Kit flow: specify → clarify (as needed) → plan → tasks →
  implement, with artifacts stored under `specs/`.
- Each user story in a spec MUST have acceptance criteria that can be independently verified.
- New or changed service-layer logic MUST be covered by automated tests that run offline
  (e.g., xUnit with SQLite in-memory); authorization rules MUST have tests for both allowed and
  denied cases.
- `dotnet build` MUST succeed with zero errors and no new warnings before a change is complete.

Rationale: Specifications and tests make behavior explicit and reviewable, which is the core skill
this project teaches.

### V. Simplicity and Training Clarity

- Prefer the simplest design that satisfies the spec; avoid speculative abstractions beyond those
  required by Principle II (YAGNI).
- Follow the existing layering: `Models/` (entities), `Data/` (EF Core context), `Services/`
  (business logic + authorization), `Pages/` and `Shared/` (Blazor UI).
- Code MUST be readable by learners: clear names, small methods, and comments only where intent is
  not obvious.
- Known limitations and training-only shortcuts (e.g., mock authentication) MUST be documented in
  the README rather than hidden.

Rationale: The codebase is a teaching artifact; clarity outweighs cleverness.

## Technology & Platform Constraints

- Runtime: .NET 10 (`net10.0`), ASP.NET Core with Blazor Server and Razor Pages for auth flows.
- Data: Entity Framework Core with SQLite (`Data Source=ContosoDashboard.db`); schema changes MUST
  keep `EnsureCreated()`-based startup working or introduce migrations explicitly in the plan.
- UI: Bootstrap 5.3 and Bootstrap Icons; no additional front-end frameworks without justification
  in the plan.
- Authentication: cookie-based mock authentication with claims and roles (Employee, TeamLead,
  ProjectManager, Administrator); production identity is out of scope.
- NuGet dependencies MUST be free of known high/critical vulnerabilities at merge time.
- Scope: training use only; the codebase is not intended for production deployment.

## Development Workflow & Quality Gates

- Plans MUST include a Constitution Check confirming compliance with Principles I–V; any deviation
  MUST be recorded with justification in the plan's complexity tracking.
- Before implementation, `/speckit.analyze` SHOULD be run to detect inconsistencies across spec,
  plan, and tasks; CRITICAL findings MUST be resolved first.
- A change is complete only when: build passes, tests pass, the app starts and seeds the database
  locally, and the README is updated for any user-visible or setup change.
- Reviews MUST verify authorization checks at the service layer for every new data access path.

## Governance

- This constitution supersedes other project practices; conflicting guidance MUST be updated or
  removed.
- Amendments are made via `/speckit.constitution`, recorded with a Sync Impact Report, and reviewed
  before merge.
- Versioning follows semantic versioning: MAJOR for removed or redefined principles, MINOR for new
  principles/sections or materially expanded guidance, PATCH for clarifications.
- Compliance is checked during planning (Constitution Check) and code review; unjustified
  violations block merge.

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-20
