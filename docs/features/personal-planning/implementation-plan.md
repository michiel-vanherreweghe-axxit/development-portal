---
slug:       personal-planning
issue:      https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/8
branch:     feat/issue-8
worktree:   /workspace/repository
convention: docs/features/<slug>/     # no prior feature docs; defaulted to per-feature folder
language:   en
approved:   false
database:   portal
seeded:     false
pr:         null
artifacts:
  plan:     null
  handover: null
milestones:
  - id: 1
    title: PlanItem domain, persistence and migration
    status: todo
    commit: null
  - id: 2
    title: GitHub issue read and search in the GitHub service
    status: todo
    commit: null
  - id: 3
    title: Plan item operations and endpoints
    status: todo
    commit: null
  - id: 4
    title: Frontend api client and planning model
    status: todo
    commit: null
  - id: 5
    title: Planning page
    status: todo
    commit: null
---

# Implementation plan: Personal planning

## Context

[Issue #8](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/8), "Personal Planning for Daily and Weekly Tasks":

> Introduce a personal planning feature that allows users to organize their work across multiple GitHub repositories by selecting existing GitHub issues and combining them with manual to-dos.

A user plans per day or per week, mixes GitHub issues with manual to-dos, tracks personal completion independently of the GitHub issue, and sees only their own data (AC1–AC10). Out of scope per the issue: creating/closing issues, team planning, calendar sync, recurring tasks, prioritisation. The only comment on the issue is `/feature-plan`.

## How it works today

- **Layers**: `Domain` (entities, `Result` factories) → `Application` (one class per operation in its own folder) → `Infrastructure` (EF, GitHub) → `Api` (FastEndpoints). Each operation is registered in `backend/src/Application/ApplicationExtensions.cs:9-21`; each endpoint is a thin class that calls the handler and `SendIfFailedAsync` (`backend/src/Api/Endpoints/Watches/UpdateWatchesEndpoint.cs:15-27`, `backend/src/Api/Extensions/EndpointExtensions.cs:11-44`).
- **Per-user data precedent**: `Watch` is owned by `User` via `UserId`, cascade delete, unique index, `Create` returns `Result<Watch>` with `ValidationError` codes (`backend/src/Domain/Watches/Watch.cs:11-71`, `backend/src/Infrastructure/Database/Configurations/WatchConfiguration.cs:8-31`, `backend/src/Infrastructure/Database/Configurations/UserConfiguration.cs:20-24`). Handlers resolve the user from `CurrentUser.GitHubId` and scope every query by it (`backend/src/Application/ReadWatches/ReadWatches.cs:11-33`). `Entity` supplies `Id`, `CreatedAt`, `UpdatedAt` (stamped in `ApplicationDbContext.cs:19-40`), and `UpdatedAt` is a concurrency token (`EntityConfiguration.cs:10-14`).
- **Auth**: a cookie session is the only scheme and holds the user's GitHub PAT; there are no roles. Endpoints are authenticated by default (`AuthenticationExtensions.cs:28-47`; the unauthenticated test returns 401, `UpdateWatchesEndpointTests.cs:14-25`). GitHub calls are made with the user's own token (`GitHubUserTokenHandler.cs`), so GitHub permissions are the user's.
- **GitHub integration**: Refit interface `IGitHubApi` (`backend/src/Infrastructure/Services/GitHub/IGitHubApi.cs`) wrapped by `IGitHubService`, which turns failures into `Result` (`GitHubService.cs:236-252`) and pages with a cap (`PagedAsync`, `GitHubService.cs:196-211`). There is **no issue endpoint** today. The generic read `CallAsync<T>` maps every failure, including 404, to `GitHub.RequestFailed` (`GitHubService.cs:245-252`), so "gone" and "temporarily failing" cannot be told apart yet. Fan-out is capped by `Throttled.MapAsync` (`backend/src/Application/Common/Throttled.cs`), as used in `ReadPullRequests.cs:10-45`.
- **Database**: EF with one migration (`Migrations/20260923162544_InitialCreate.cs`), applied at startup (`Program.cs:26`); tests swap Npgsql for the in-memory provider, which **does not enforce unique indexes** (comment in `UpdateWatches.cs:34`).
- **Frontend**: pages are declared as data in `frontend/src/Portal.tsx:158-203` (`PageDefinition`, hash routing, no router; `frontend/src/shell/types.ts`). API calls go through `frontend/src/api/portal.ts`; errors are `ApiError` with `code`, reported through `usePortalData.report` with per-code texts (`usePortalData.ts:35-49`). A repository picker exists (`frontend/src/dashboard/RepositoryFinder.tsx`) and `readRepositories` already supplies the repositories the user can see. Frontend tests are vitest on pure model code only (`frontend/src/dashboard/model.test.ts`); there is no component-test library.
- **Docs**: no `docs/` existed; this plan defaults to per-feature folder, English.
- **Rules check (Step 4b)**: no `coding-rules.md` and no architecture tests (`ArchTest`/`NetArchTest`) exist. The de facto convention is operation folders named with verbs (`Read…`, `Update…`, `Create…`, `Close…`, `Merge…`), `Result` for failures, and `AAA` comments in tests.

## Approach

**Data model.** One table, `PlanItems`, one entity `PlanItem : Entity` (a single table with a nullable issue reference rather than two entity types; the screens treat both kinds identically).

| Column | Notes |
|---|---|
| `UserId` | FK to `Users`, cascade delete |
| `Title` (≤200), `Description?` (≤2000) | to-do text; for issues `Title` is the last-known issue title (snapshot) |
| `Owner?`, `Repository?`, `IssueNumber?` | all three set for an issue entry, all null for a to-do |
| `Span` | `Day` or `Week` |
| `Start` (`DateOnly`) | the day, or for `Week` the Monday of that week (normalised in the domain) |
| `IsCompleted`, `CompletedAt?` | personal state only |

Unique index on `(UserId, Owner, Repository, IssueNumber, Span, Start)` for duplicates (AC8); nulls are distinct so to-dos never collide. Because the in-memory provider ignores it, `CreatePlanItem` and `UpdatePlanItem` (on move) also check in code and return `Result.Conflict("PlanItem.AlreadyPlanned")`, mirroring `UpdateWatches`. Items are never removed when GitHub changes (§5 of the issue): the row only holds a reference and a snapshot.

**Operations** (CRUD verbs, v1 routes, all for the signed-in user only; a row that is not the user's is a 404, never a 403, so existence does not leak):

- `ReadPlanItems`: `GET /api/v1/plan-items?from=&to=` (dates `yyyy-MM-dd`). Returns items with `Start` between the Monday of `from` and `to`, ordered; the frontend filters a day view to that day plus the containing week's weekly items. For each distinct issue it reads the issue from GitHub (throttled, `Throttled.MapAsync`) and returns `IssueState`: `Open`, `Closed`, `Unavailable` (404/410, or 403 not caused by rate limiting), `Unknown` (any other failure; shows the snapshot). A successful read refreshes the stored title snapshot. A GitHub failure never fails the request (AC9).
- `CreatePlanItem`: `POST /api/v1/plan-items`, body `{ kind, title?, description?, repository?, issueNumber?, span, date }`. For an issue it reads the issue once (to validate it exists/is accessible and to take the title); it refuses pull requests.
- `UpdatePlanItem`: `PUT /api/v1/plan-items/{id}`, body `{ title?, description?, span, date, isCompleted }`. Title/description may only change on to-dos; for issue entries they are ignored/rejected as invalid. Move, complete and un-complete go through this one operation (a single `Update` verb, no `Complete`/`Move` verbs).
- `DeletePlanItem`: `DELETE /api/v1/plan-items/{id}` → 204. Deletes only the row.
- `ReadIssues`: `GET /api/v1/issues?repository=owner/name&query=`. Validates the repository with `Watch.IsValidFullName`, calls `GET /search/issues?q=repo:owner/name is:issue <query>` (first page, 30 results, most recently updated), and drops any result not belonging to the requested repository so free-text cannot smuggle in another `repo:` qualifier.

**Why no GitHub write**: the service only gains read methods; nothing in this feature calls a PATCH/PUT/POST on GitHub (AC2, AC6, AC7).

**Frontend.** A new `planning` page (`PageDefinition`, group "Planning") with day/week toggle, previous/next/today, a to-do form, an issue finder (repository from the already-loaded `data.repositories`, text search, debounced), and a list of entries grouped by day (week mode) with checkbox, move (date input), edit, remove, GitHub link, state badge and "unavailable" marker. Week arithmetic (Monday start) lives in a pure `planning/model.ts` with vitest tests. Planning data is loaded by a small `usePlanning` hook, not folded into `usePortalData`, so the 60-second dashboard refresh does not re-run GitHub fan-out for the planning page and vice versa.

## Alternatives rejected

- **Two tables / two entity types (to-do vs issue entry)**: doubles the endpoints and the move/complete code for no behavioural gain.
- **Store issue state in the database and sync in the background**: needs a hosted service and the user's token outside a request; the token only lives in the session cookie (`AuthenticationExtensions.cs:28-47`). Reading live at request time respects the user's current permissions.
- **GitHub Projects / labels as storage**: would modify GitHub, which the issue forbids.
- **Cross-repository search with no `repo:` qualifier**: searches all of GitHub, not "accessible repositories". Picking a repository first reuses the existing repository list and keeps queries bounded; cross-repo search can be added later.
- **Separate `Complete` and `Move` endpoints**: not needed; one `UpdatePlanItem`.
- **Adding a router or component-test library**: the app uses hash pages and pure-model tests; keep it.

## Screens and the endpoints they read

| Screen | Endpoint | Called as role | Endpoint allows (`path:line`) |
|---|---|---|---|
| Planning | `GET /api/v1/plan-items` (new) | any signed-in user | authenticated default, scoped by `CurrentUser.GitHubId` (new) |
| Planning | `POST /api/v1/plan-items` (new) | any signed-in user | same |
| Planning | `PUT /api/v1/plan-items/{id}` (new) | any signed-in user | same, 404 for other users' rows |
| Planning | `DELETE /api/v1/plan-items/{id}` (new) | any signed-in user | same |
| Planning | `GET /api/v1/issues` (new) | any signed-in user | same; GitHub enforces the token's repository access |
| Planning | `GET /api/v1/repositories` (existing, via `usePortalData`) | any signed-in user | no roles; `ReadRepositoriesEndpoint.cs:9-16` |

No role gates exist in this app (cookie scheme only), so there is no ⚠ row.

## Milestones

### 1. PlanItem domain, persistence and migration
- **Files**: `backend/src/Domain/Planning/PlanItem.cs`, `PlanSpan.cs`; `backend/src/Infrastructure/Database/Configurations/PlanItemConfiguration.cs`; `ApplicationDbContext.cs` (`DbSet<PlanItem>`); `UserConfiguration.cs` only if a navigation is wanted (not required); new migration `AddPlanItems` + snapshot; `backend/tests/UnitTests/Domain/PlanItemTests.cs`.
- **Acceptance**: `PlanItem.CreateTodo` / `CreateIssue` validate (title required and ≤200, description ≤2000, repository via `Watch.IsValidFullName`, issue number > 0) and return `Result` with `ValidationError` codes `PlanItem.*`; week items normalise `Start` to Monday; `MoveTo`, `SetCompleted` (stamps/clears `CompletedAt`) and `Edit` (to-dos only) behave as described; migration creates `PlanItems` with the unique index; solution builds.
- **Tests**: unit tests per factory/method incl. Sunday → Monday normalisation and invalid inputs (pure domain, nothing to mock).
- **Rules**: follows `Watch`/`User` (private ctor, static `Create`, `Result`, length constants). Migration generated with `dotnet ef migrations add AddPlanItems` against `ApplicationDbContext`; applying it needs PostgreSQL (unverified here, no Docker).

### 2. GitHub issue read and search in the GitHub service
- **Files**: `IGitHubApi.cs` (`GET /repos/{owner}/{repository}/issues/{number}`, `GET /search/issues`), `Models/GitHubModels.cs` (`GitHubIssue(Number, Title, State, HtmlUrl, PullRequest?)`, `GitHubIssueSearch(Items)`), `GitHubService.cs` (`GetIssueAsync`, `SearchIssuesAsync` on `IGitHubService`), `GitHubErrors.cs` (`Unavailable = "GitHub.Unavailable"`), `Application/Common/GitHubMapper.cs` (issue DTO), `tests/UnitTests/Endpoints/FakeGitHubService.cs` (new members), `tests/UnitTests/Services/GitHubServiceTests.cs` or extend `GitHubMapperTests.cs`.
- **Acceptance**: `GetIssueAsync` returns `Result.NotFound(GitHubErrors.Unavailable)` for 404/410 and for 403 unless `x-ratelimit-remaining: 0` (then `Error(RequestFailed)`), `Error(RequestFailed)` for other failures; `SearchIssuesAsync` returns the first page only (search API allows 30 req/min) and failure becomes `Result.Error`; no write calls added.
- **Tests**: service tests with a stub `IGitHubApi` raising `ApiException` for 404, 410, 403 (with and without rate-limit header), 500; mapper test for issue → DTO including the pull-request marker. `IGitHubApi` is `internal`: check `InternalsVisibleTo` for `UnitTests` (existing `GitHubRetryPolicyTests` shows how the service tests reach internals); if absent, test through the public `IGitHubService` with a stub `HttpMessageHandler`.
- **Rules**: existing `CallAsync` pattern (`GitHubService.cs:236-252`); the new 404 branch must not change what other reads return.

### 3. Plan item operations and endpoints
- **Files**: `Application/{ReadPlanItems,CreatePlanItem,UpdatePlanItem,DeletePlanItem,ReadIssues}/*` (handler, request + `Validator` like `UpdateWatchesRequest.cs`), `Application/Common/PlanItemDto.cs`, `IssueDto.cs`, `ApplicationExtensions.cs` registrations, `Api/Endpoints/PlanItems/*Endpoint.cs`, `Api/Endpoints/Issues/ReadIssuesEndpoint.cs`, tests under `tests/UnitTests/Endpoints/`, a `PortalApiFactory.SeedPlanItemAsync` helper and a way to seed a second user.
- **Acceptance**: AC1, AC3–AC10 are observable through the API: range read returns day and week items; create to-do and create issue entry (issue untouched; GitHub fake records no write); duplicate issue in the same day/week → 409 `PlanItem.AlreadyPlanned`, same issue on another day/week → 201; move changes `Start`/`Span`; complete persists and issue state is unchanged; delete removes only the row; unavailable issue → item returned with `issueState = Unavailable`, 200; GitHub failure → `Unknown` with the snapshot; another user's id → 404 for update and delete and absent from reads; unauthenticated → 401; invalid dates/titles → 400 with error codes.
- **Tests**: `*EndpointTests` per operation using `PortalApiFactory` and `FakeGitHubService`, AAA comments. The harness signs in as whatever GitHub id the `X-Test-User` header carries (`PortalApiFactory.cs:~110-125`) but `SeedUserAsync` seeds only the fixed id 4242, so isolation tests need a helper that seeds a second user and a client sending a different header. The harness has no roles to test (none exist). Duplicate behaviour is verified through the in-code check because the in-memory provider ignores the unique index; the index itself is covered only by the migration (unverified without PostgreSQL).
- **Rules**: verbs `Read`/`Create`/`Update`/`Delete` only, matching existing operation names; failures via `Result` + `SendIfFailedAsync`; per-user scoping as in `ReadWatches.cs:11-33`.

### 4. Frontend api client and planning model
- **Files**: `frontend/src/api/types.ts` (`PlanItem`, `IssueHit`, request types), `frontend/src/api/portal.ts` (`readPlanItems`, `createPlanItem`, `updatePlanItem`, `deletePlanItem`, `readIssues`), `frontend/src/planning/model.ts`, `frontend/src/planning/model.test.ts`.
- **Acceptance**: `model.ts` exposes pure helpers: Monday-of-week, `weekDays`, `shift(date, span, ±1)`, ISO date formatting that does not go through UTC (local date, so late evening does not roll to the next day), `visibleItems(items, span, date)` (day mode: that day's items plus the containing week's items; week mode: everything in the week grouped by day), and `summary` (done/total). `npm run build`, `lint` and `test` pass.
- **Tests**: vitest for week boundaries (Sunday/Monday, year boundary), DST-safe day shifting, grouping and the day-mode filter.
- **Rules**: same file/style as `dashboard/model.ts`; no new dependencies.

### 5. Planning page
- **Files**: `frontend/src/planning/PlanningPage.tsx`, `usePlanning.ts`, `PlanItemRow.tsx`, `TodoForm.tsx`, `IssueFinder.tsx`; `frontend/src/Portal.tsx` (new `PageDefinition`, `NotConnected` when not connected, 401 → `session.expired`); `frontend/src/shell/icons.tsx` (icon); `frontend/src/index.css` (styles, including a stacked layout on narrow screens, matching how existing pages behave).
- **Acceptance**: loading, empty and error states are shown; day/week toggle and date navigation work; creating, editing, completing, moving and removing items updates the view and survives refresh; issue entries show title, `owner/repo#number`, state and a link opening on GitHub; unavailable and unknown issues are labelled and still actionable; the "already planned" 409 shows a message instead of a generic error; completed items remain visible, struck through; Confirm dialog is used before removing/deleting, as on the dashboard.
- **Tests**: no component harness exists, so UI behaviour is verified by hand against the running app (unverified in this planning environment) and logic by milestone 4 tests; `npm run lint` and `npm run build` must be green.
- **Rules**: reuse `Confirm`, `Toasts`/`useToasts`, `Spinner`, and the `report`-style error-code mapping from `usePortalData.ts:35-49`; reuse the `RepositoryFinder` interaction (type to filter repositories) rather than a new pattern.

## Risks

- **Search API limits** (30 requests/min, results lag): debounce the finder and cap to the first page. Early signal: `403`/`429` from `/search/issues` → surface "try again in a minute", not the generic error.
- **GitHub 403 means either "no access" or "rate limited"**: mishandling it would mislabel live issues as unavailable. Mitigated by the rate-limit header check in milestone 2; test covers it.
- **Fan-out cost on the plan read**: bounded by distinct issues in the requested range and `Throttled.MapAsync`; a week with many issues across repos is the stress case.
- **Time zones**: "today" is the browser's local date; the server only stores `DateOnly`. Signal: items landing a day off near midnight; covered by the local-date helper test.
- **In-memory provider vs unique index**: duplicate handling is verified in code, not in the database.
- **Migration** can only be applied/verified with PostgreSQL; unverified here.

## Open questions

Assumptions made (no blocking ambiguity):
1. Weeks start on **Monday**; a weekly entry belongs to exactly one week and is shown on all days of that week in day mode, in a separate "this week" group.
2. Issue search is **per repository** (pick a repository first), using the repositories the user's token already lists.
3. Completed items stay in their period; there is no automatic roll-over of unfinished items to the next day/week (not asked for).
4. Closed GitHub issues can still be planned (the state is just displayed); pull requests are rejected.
5. A to-do's description is plain text; no ordering within a day beyond creation time.
6. The same issue may be planned on several different days/weeks (only the same period is a duplicate).

## Out of scope

Creating, editing or closing GitHub issues; team or shared plans; calendar sync; recurring tasks; priorities and estimates; automatic scheduling or recommendations; automatic roll-over of unfinished items; drag-and-drop; cross-repository free-text search; background sync of issue state.
