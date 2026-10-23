---
slug:       dependency-status
issue:      https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/11
branch:     feat/issue-11
worktree:   /workspace/repository
convention: docs/features/<slug>/     # no docs/ existed; default shape A
language:   en
approved:   false
database:   null                      # no schema change, no migration
seeded:     false
pr:         null
artifacts:
  plan:     null
  handover: null
milestones:
  - id: 1
    title: Administrator concept (configured logins, policy, isAdministrator on /auth/me)
    status: todo
    commit: null
  - id: 2
    title: Dependency health checks, periodic publisher and cached results
    status: todo
    commit: null
  - id: 3
    title: Admin-only GET /api/v1/dependencies endpoint
    status: todo
    commit: null
  - id: 4
    title: Dependencies page in the frontend
    status: todo
    commit: null
---

# Implementation plan: Dependency status page

## Context

Issue #11, "Add a dependency status page":

> Add an admin page that lists the application's external dependencies (database, GitHub API) with their current health, based on the existing ASP.NET Core health checks.
> - Each dependency shows healthy, degraded or unhealthy with the last check time.
> - Only administrators can open the page.

Answers from the issue author in the discussion:

1. Administrators are a configured list of GitHub logins.
2. Add a GitHub API check, and a database check if Aspire doesn't already register one. Also show whether GitHub Actions and the Pull Requests API are up, using the "Actions" and "Pull Requests" components from `https://www.githubstatus.com/api/v2/components.json`.
3. Add a periodic publisher that caches the latest result and check time per dependency.

Feature docs: [use-case.md](use-case.md), [sequence-diagram.md](sequence-diagram.md).

## How it works today

- Health: only `self` is registered (`backend/src/Api/ApiExtensions.cs:19-20`, tagged `liveness`), exposed anonymously at `/health` (`backend/src/Api/Program.cs:52`). No publisher, no cached results, no check time.
- Database: `builder.AddNpgsqlDbContext<ApplicationDbContext>("portal")` (`backend/src/Infrastructure/InfrastructureExtensions.cs:18`) from `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` (`Directory.Packages.props`). That package registers a DbContext health check unless disabled; the exact registration name must be confirmed in milestone 2 by reading the registered `HealthCheckServiceOptions` (not verifiable by reading source here). If it is absent, we add our own `AddDbContextCheck`.
- GitHub: `IGitHubApi` is a Refit client with `GitHubUserTokenHandler`, which needs a signed-in session (`backend/src/Infrastructure/Services/GitHub/Extensions/ServiceCollectionExtensions.cs:33-46`). A background check has no session, so it cannot reuse that client. `GitHubConfiguration.Uri` defaults to `https://api.github.com/` (`.../Configuration/GitHubConfiguration.cs:9`).
- Auth: cookie scheme only, `services.AddAuthorization()` with no policies (`backend/src/Api/Authentication/AuthenticationExtensions.cs:55`). The session carries id, login and avatar claims only (`backend/src/Api/Endpoints/Authentication/CreateSignInEndpoint.cs:49-57`). No roles. Access-denied answers 403 (`AuthenticationExtensions.cs:48`).
- Endpoints follow FastEndpoints + an Application handler per operation: `ReadRepositoriesEndpoint` (`backend/src/Api/Endpoints/Repositories/ReadRepositoriesEndpoint.cs`) calling `ReadRepositories` (`backend/src/Application/ReadRepositories/ReadRepositories.cs`), registered in `ApplicationExtensions.cs:9-20`. Endpoints are authenticated by default (no `AllowAnonymous`).
- `GET /auth/me` returns `GitHubId`, `Login`, `AvatarUrl` from claims (`backend/src/Api/Endpoints/Authentication/ReadCurrentUserEndpoint.cs:5-31`); frontend type `CurrentUser` in `frontend/src/api/types.ts:3-7`.
- Frontend: pages are data (`PageDefinition`, `frontend/src/shell/types.ts`) built in `frontend/src/Portal.tsx:~150-200`; the sidebar, hash route and heading follow from that list. API calls live in `frontend/src/api/portal.ts` over `request()` in `client.ts`.
- Tests: `PortalApiFactory` swaps the DB for in-memory and `IGitHubService` for a fake, and authenticates every request as login `tester` via `TestAuthenticationHandler` (`backend/tests/UnitTests/Endpoints/PortalApiFactory.cs:66-130`). Frontend has `vitest` for `dashboard/model.ts` only.
- Repo rules: no `coding-rules.md` and no architecture tests (`ArchTest`/`NetArchTest`) exist; existing naming (Read*/Update*/Close* operations, one folder per operation) is followed by convention.

## Approach

**Administrators.** Add `Administrators` (string list) to `PortalConfiguration` (`Portal:Administrators`). Register an `Administrator` authorization policy backed by a requirement handler that compares the session's login, case-insensitively, against `IOptionsMonitor<PortalConfiguration>`. Evaluating on each request (rather than stamping a role claim at sign-in) means config changes apply without re-signing in. `GET /auth/me` gains `isAdministrator`, so the frontend can hide the page; the api policy is the real gate.

**Dependency checks.** Four `IHealthCheck`s tagged `dependency`: Database (Aspire's if present, otherwise `AddDbContextCheck`), GitHub API (unauthenticated `GET /rate_limit` on a plain named `HttpClient`, which does not count against rate limits and needs no token), and GitHub Actions / GitHub Pull Requests (one shared `GitHubStatusClient` reading `components.json`, mapping `operational` to Healthy, `degraded_performance`/`partial_outage`/`under_maintenance` to Degraded, `major_outage` to Unhealthy; a component that is missing or an unreachable status site is Unhealthy with a description). Short timeouts on the HTTP calls so a hung dependency reports Unhealthy rather than stalling the run.

**Periodic publisher.** `IHealthCheckPublisher` (built into ASP.NET Core: `HealthCheckPublisherOptions` supplies the period and `Predicate = tag dependency`, so no custom hosted service) writes each entry's status, description and the publish time (via the registered `TimeProvider`) into a singleton `DependencyStatusStore`. The `self` liveness check is excluded by the predicate.

**Read path.** `ReadDependencies` handler in Application returns `DependencyDto { Name, Status, Description, CheckedAt }` from the store; `ReadDependenciesEndpoint` (`GET dependencies`, v1, `Policies("Administrator")`) serves it. Before the first publish the list is empty.

**Frontend.** A `DependenciesPage` (reusing the existing card/`group` styling from the Repositories and Settings pages) listing name, status badge, description and last-check time, loaded on open with a Refresh button. The page is added to `pages` in `Portal.tsx` only when `session.user.isAdministrator`; non-admins never see it, and a stale `#dependencies` hash falls back through the existing `fallbackPage` logic.

Nearest precedent: the Read* operation/endpoint pair for the api, and the `PageDefinition` list for the screen.

## Alternatives rejected

- **Role claim stamped at sign-in**: config changes need a re-login; the per-request check is as small.
- **GitHub org owner / DB flag for admin**: rejected by the issue author (configured logins).
- **Custom `BackgroundService` polling checks**: `IHealthCheckPublisher` already does periodic runs and filtering.
- **Run checks on demand per request**: gives no stable "last check time" and lets any admin hammer GitHub; the author asked for a cached publisher.
- **Reuse the Refit GitHub client**: needs a user token; the background run has none.
- **Persist results in the database**: results are cheap to recompute and a restart repopulates them within a period; no migration needed.

## Screens and the endpoints they read

| Screen | Endpoint | Called as role | Endpoint allows (`path:line`) |
|---|---|---|---|
| Dependencies | `GET /api/v1/dependencies` (new) | Administrator | `Policies("Administrator")` (new, milestone 3) |
| Shell / session probe | `GET /api/v1/auth/me` (adds `isAdministrator`) | any signed-in user | authenticated only, `ReadCurrentUserEndpoint.cs:15-19` |

No existing admin-only endpoint is read by the new screen. Non-admins never call `/dependencies`.

## Milestones

### 1. Administrator concept
- **Files**: `backend/src/Api/Configuration/PortalConfiguration.cs`, new `backend/src/Api/Authentication/` requirement + handler, `AuthenticationExtensions.cs` (policy), `ReadCurrentUserEndpoint.cs`, `frontend/src/api/types.ts` (`isAdministrator`), `backend/src/Api/appsettings.json` (`Administrators: []`), `PortalApiFactory.cs`.
- **Acceptance**: a login in `Portal:Administrators` (any casing) satisfies the `Administrator` policy; others do not; `/auth/me` reports `isAdministrator` accordingly.
- **Tests**: unit tests for the handler (listed / unlisted / case difference / empty list). The harness always authenticates as `tester` (`PortalApiFactory.cs:130`), so add an optional `X-Test-Login` header to `TestAuthenticationHandler` and an `Administrators` setting on the factory, otherwise a 403 case cannot be observed. Endpoint test on `/auth/me` for both outcomes.
- **Rules**: no repo rules file; follows existing one-class-per-concept layout.

### 2. Dependency health checks, publisher and cache
- **Files**: new `backend/src/Infrastructure/Health/` (checks, `GitHubStatusClient`, `DependencyStatusStore`, publisher), `InfrastructureExtensions.cs` (registration, publisher options), `ApiExtensions.cs` if registration sits next to the `self` check.
- **Acceptance**: after a publisher run the store holds four entries (Database, GitHub API, GitHub Actions, GitHub Pull Requests) with status, description and check time; the DB check is confirmed to be registered exactly once (Aspire's, or ours if absent). Status mapping as in Approach. `/health` and `self` behave as before.
- **Tests**: checks tested with a fake `HttpMessageHandler` (operational, degraded, major outage, component missing, timeout/unreachable); publisher test with a fake `TimeProvider` asserting the cached check time. The factory must replace the HTTP-backed checks/publisher (or their handlers) so endpoint tests never call the real network; the in-memory DB provider is what the DB check will see in tests.
- **Rules**: none beyond convention.

### 3. Admin-only dependencies endpoint
- **Files**: `backend/src/Application/ReadDependencies/ReadDependencies.cs`, `Application/Common/DependencyDto.cs`, `ApplicationExtensions.cs`, `backend/src/Api/Endpoints/Dependencies/ReadDependenciesEndpoint.cs`.
- **Acceptance**: administrator gets 200 with the cached list; non-administrator 403; anonymous 401; empty list before the first publish.
- **Tests**: `ReadDependenciesEndpointTests` in the style of `ReadRepositoriesEndpointTests` (401, 403 via the milestone 1 header, 200 with a store pre-filled).
- **Rules**: Read* naming and folder-per-operation as in existing modules.

### 4. Dependencies page
- **Files**: `frontend/src/api/portal.ts` + `types.ts` (`Dependency`), new `frontend/src/dependencies/DependenciesPage.tsx`, `frontend/src/Portal.tsx`, icon in `shell/icons.tsx`, small styles in `index.css`.
- **Acceptance**: administrators see the page in the sidebar with status badge, description and last-check time per dependency and a Refresh button; non-administrators do not see it; empty state says the first check is pending; an error toasts like other pages. Layout is cards/rows that remain readable at phone width like the existing pages.
- **Tests**: `vitest` for any pure mapping helper (status to label/class, relative time); `npm run lint` and type-check green. Manual click-through per the use-case (unverified here: needs the Aspire app host and Docker).
- **Rules**: reuse existing `group`/`header`/`body` page markup and `Spinner`.

## Risks

- **Aspire DB check name/behaviour unknown from source**: confirm in milestone 2 by listing registered checks in a test; early signal is a duplicate or missing "Database" entry.
- **githubstatus.com schema/naming drift** ("Actions", "Pull Requests" component names): match by exact name and report Unhealthy with "component not found" so drift is visible, not silent.
- **Network in tests / CI**: the publisher runs in `WebApplicationFactory`; an unfaked HTTP check would make tests flaky. Signal: tests touching the network.
- **Empty `Administrators` in a deployment** means nobody can open the page; documented in the config and README note.
- **Publisher period vs GitHub load**: default 1 minute, unauthenticated `/rate_limit` and one status call per run is trivial.
- Checks requiring Docker/Aspire to run end to end are unverified in this environment.

## Open questions

Assumptions (the author can correct these in review):

- Period of 1 minute, first run after a few seconds; HTTP timeouts 5 s. Configurable only if cheap.
- Degraded for GitHub status `degraded_performance`, `partial_outage` and `under_maintenance`; Unhealthy for `major_outage`.
- The GitHub API check calls `GET {GitHub:Uri}/rate_limit` unauthenticated; any non-success or timeout is Unhealthy.
- "Last check time" is the time the publisher cached the result, shared by all rows of one run.
- Administrators are matched on GitHub login only, case-insensitive.

## Out of scope

- Roles beyond a single administrator list; managing administrators in the UI.
- Alerting, history or trends of dependency health.
- Changing `/health` or the Docker/infra probes.
- Pausing or triggering checks manually from the page.
