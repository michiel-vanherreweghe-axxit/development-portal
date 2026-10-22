---
slug:       application-insights-monitoring
issue:      https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/3
branch:     feat/issue-3
worktree:   /workspace/repository
convention: docs/features/<slug>/     # no docs/ existed; default shape A
language:   en
approved:   false
database:   null
seeded:     false
pr:         null
artifacts:
  plan:     null
  handover: null
milestones:
  - id: 1
    title: Instance domain, persistence and manage endpoints
    status: todo
    commit: null
  - id: 2
    title: Azure query client and exceptions endpoint
    status: todo
    commit: null
  - id: 3
    title: Dashboard cards and instance management in the frontend
    status: todo
    commit: null
---

# Implementation plan: Application Insights monitoring

## Context

Issue #3: "Monitor Azure Application Insights for thrown exceptions, alongside the repository monitoring already in the portal." A user adds several instances by resource ID, sees a card per instance with the exception count over 24h and the most recent exceptions, and an instance that cannot be queried shows an error rather than a healthy card. Out of scope per the issue: alerting, stack traces, non-exception telemetry. The issue comment only repeats `/feature-plan`.

## How it works today

- Watches are per user rows: `Watch` entity with `UserId`, `SortOrder`, `DisplayName`, a strict regex that keeps a name inside its URL segment (`backend/src/Domain/Watches/Watch.cs:50-83`, `:85-100`).
- The whole list is replaced on every save: `UpdateWatches` deletes and re-adds, de-duplicates in memory because the in-memory provider ignores the unique index (`backend/src/Application/UpdateWatches/UpdateWatches.cs:31-60`); `UpdateWatchesRequest.Validator` repeats the domain rules with error codes (`UpdateWatchesRequest.cs:13-36`).
- Reading: `ReadWatches` (`backend/src/Application/ReadWatches/ReadWatches.cs`) and `ReadRuns`, which fans out with `Throttled.MapAsync` and puts a per-row `Error` on a failing row instead of failing the request (`backend/src/Application/ReadRuns/ReadRuns.cs:36-58`, `Common/WatchRunsDto.cs`).
- Endpoints are thin FastEndpoints classes, versioned `v1`, authenticated by default (only sign-in and `/health` are anonymous: `Program.cs:52`, `CreateSignInEndpoint.cs:18`); failures render through `SendIfFailedAsync` (`backend/src/Api/Extensions/EndpointExtensions.cs`). Handlers are registered by hand in `ApplicationExtensions.cs:9-20`.
- Portal-level config: `PortalConfiguration` (`backend/src/Api/Configuration/PortalConfiguration.cs`); external client config bound with `ValidateOnStart` and a Refit client with resilience handler (`Infrastructure/Services/GitHub/Extensions/ServiceCollectionExtensions.cs:14-52`, `GitHubConfiguration.cs`). Error keys are constants (`GitHubErrors.cs`), shown through i18n-style message maps in the frontend.
- Schema: one `InitialCreate` migration, `WatchConfiguration` with a unique index (`Infrastructure/Database/Configurations/WatchConfiguration.cs`); DbContext in `ApplicationDbContext.cs`. Migrations run on boot (`InfrastructureExtensions.cs:30-40`).
- Tests: endpoint tests over `PortalApiFactory` (in-memory DB, fake `IGitHubService`, header-based test auth; no roles exist) plus domain tests such as `WatchTests.cs`.
- Frontend: `usePortalData.ts` loads runs and PRs, refreshes every 60s, and saves the full watch list through `commit`; `Portal.tsx` builds cards (`buildCards`, `attentionOf` in `dashboard/model.ts`), `RepositoriesPage.tsx` renders `RepositoryCard`s in `.cards`; the "needs attention" filter is driven by `attention.any`. API wrappers in `api/portal.ts`, types in `api/types.ts`. The only settings page is the GitHub connection (`settings/SettingsPage.tsx`).
- Repo rules: no `coding-rules.md` and no architecture tests exist (checked `backend/tests` for ArchTest/NetArchTest). No `docs/` existed; this plan creates `docs/features/` (shape A, English).

## Approach

Mirror watches end to end, with one new entity and one new read path, following `Watch` / `ReadRuns` closely.

- **Domain**: `Domain.Insights.InsightsInstance` (`UserId`, `ResourceId`, `DisplayName`, `SortOrder`), `Create` with `TryParseResourceId`. The regex accepts only `/subscriptions/{guid}/resourceGroups/{name}/providers/microsoft.insights/components/{name}` (case-insensitive provider segments, name characters limited to `[A-Za-z0-9._()-]`, no `..` segments), so it cannot walk out of its URL segment. The resource ID is stored normalised.
- **API shape**: `GET /api/v1/insights/instances` and `POST /api/v1/insights/instances` (replace the list, like watches), `GET /api/v1/insights/exceptions` (one row per instance). Handlers `ReadInsightsInstances`, `UpdateInsightsInstances`, `ReadInsightsExceptions`. A separate list from watches: instances are not tied to the run limit.
- **Connection**: `AppInsightsConfiguration` (`TenantId`, `ClientId`, `ClientSecret`, timeouts), section `AppInsights`, bound with `ValidateOnStart` only when the section is present (same as `AddGitHubClient`). Tokens via `Azure.Identity` `ClientSecretCredential` (scope `https://api.applicationinsights.io/.default`, cached by the SDK, registered as a singleton). Query via a Refit client on `https://api.applicationinsights.io/` calling `POST /v1{resourceId}/query` with a KQL body over `exceptions`, behind the standard resilience handler. A thin `IAppInsightsService` returns `Result<...>` like `IGitHubService`, so tests fake it the way they fake GitHub.
- **KQL** (fixed 24h window as a constant): count of rows in the window plus top exception groups: `exceptions | where timestamp > ago(24h) | summarize count = sum(itemCount), last = max(timestamp) by type, outerMessage, operation_Name | top 10 by last desc`; total is the sum of the groups' counts from a second summarize (two small queries in one request body is not supported, so two calls, or one query with `union`; implementation picks the simpler and records it).
- **Errors**: 401/403/404 from Azure map to `AppInsights.AccessDenied` (missing Monitoring Reader or wrong ID); anything else to `AppInsights.RequestFailed`; missing configuration to `AppInsights.NotConfigured`. The row carries the key in `Error`, exactly like `WatchRunsDto.Error`.
- **Frontend**: `InsightsCard` rendered in the same `.cards` grid, built by `buildInsightsCards` in `dashboard/model.ts`. Attention: error or count > 0 gives a `fail` badge ("N exceptions in 24h" / "cannot read"), otherwise "all clear"; these feed the "Needs attention only" filter. Management lives on the cards and a small add form (resource ID + display name), with rename, reorder (up/down) and remove through the same confirm dialog as watches; every change sends the full list. A new `useInsightsData` hook keeps `usePortalData` from growing, with the same 60s refresh and 401 handling.

## Alternatives rejected

- **A secret per instance**: the issue asks for one app registration.
- **Extending `Watch`** with a kind column: the two have nothing in common beyond ordering; a shared table would need nullable columns and touch every watch query.
- **Hand-rolled client-credentials token call** instead of `Azure.Identity`: saves one package but reimplements caching and expiry.
- **Per-instance selectable window**: the issue leaves it open; a constant keeps the model and UI small. Easy to add later as a column.
- **Shared instances across users**: contradicts the acceptance criterion "per user and persisted, same as repository watches".

## Screens and the endpoints they read

| Screen | Endpoint | Called as role | Endpoint allows |
|---|---|---|---|
| Dashboard (insight cards) | `GET /api/v1/insights/exceptions` | signed-in user | default auth, no roles (new) |
| Dashboard (manage instances) | `GET`/`POST /api/v1/insights/instances` | signed-in user | default auth, no roles (new) |
| Dashboard (existing) | `GET /api/v1/runs`, `/pull-requests`, `/watches`, `/repositories` | signed-in user | default auth, unchanged |

No role-gated endpoints exist in the portal, so there is no role mismatch to resolve.

## Milestones

### 1. Instance domain, persistence and manage endpoints
- **Files**: `backend/src/Domain/Insights/InsightsInstance.cs`; `Infrastructure/Database/Configurations/InsightsInstanceConfiguration.cs`, `ApplicationDbContext.cs`, new migration + snapshot; `Application/ReadInsightsInstances`, `UpdateInsightsInstances` (+ request validator), `Common/InsightsInstanceDto.cs`, `ApplicationExtensions.cs`; `Api/Endpoints/Insights/ReadInsightsInstancesEndpoint.cs`, `UpdateInsightsInstancesEndpoint.cs`.
- **Acceptance**: a user can save, rename, reorder and remove instances; invalid resource IDs get 400 with `InsightsInstance.ResourceIdInvalid`; instances are per user and survive reload; duplicates dropped.
- **Tests**: `InsightsInstanceTests` (valid ID, traversal attempts, wrong provider, overlong name); endpoint tests for read/update using `PortalApiFactory` (needs a `SeedUserAsync` extension for instances), including isolation between two users.
- **Rules**: no rule files; follows `Watch`/`UpdateWatches` naming (`Read…`/`Update…`).

### 2. Azure query client and exceptions endpoint
- **Files**: `Directory.Packages.props` + `Infrastructure.csproj` (`Azure.Identity`); `Infrastructure/Services/AppInsights/` (`AppInsightsConfiguration`, `IAppInsightsApi`, `IAppInsightsService`, `AppInsightsService`, `AppInsightsErrors`, models, DI extension called from `InfrastructureExtensions.cs`); `Application/ReadInsightsExceptions`, `Common/InsightsExceptionsDto.cs`; `Api/Endpoints/Insights/ReadInsightsExceptionsEndpoint.cs`; `appsettings.json` (empty `AppInsights` section documented, secrets via user-secrets/env); `AppHost.cs` passes the three settings through as environment if present.
- **Acceptance**: one row per instance with count and recent exceptions (type, message, operation, timestamp, count) newest first; a 403/404 from Azure yields a row with `AppInsights.AccessDenied` and an empty list, other rows unaffected; unconfigured portal yields `AppInsights.NotConfigured` rows and boots normally.
- **Tests**: handler/endpoint tests with a `FakeAppInsightsService` registered in `PortalApiFactory` (healthy, zero, denied, failed, mixed); mapper test for the Azure table response to DTO; response parsing test with a recorded sample body. Real Azure calls are unverified (no tenant here).
- **Rules**: `Throttled.MapAsync` for fan-out like `ReadRuns`; the resource ID is only ever sent after passing `TryParseResourceId` (re-checked at read time, since rows could predate a stricter regex).

### 3. Dashboard cards and instance management in the frontend
- **Files**: `frontend/src/api/types.ts`, `api/portal.ts`; `dashboard/useInsightsData.ts`, `dashboard/InsightsCard.tsx`, `dashboard/InsightsForm.tsx`, `dashboard/model.ts` (+ `model.test.ts`), `dashboard/RepositoriesPage.tsx`, `Portal.tsx`, `index.css`.
- **Acceptance**: cards appear next to repository cards; count > 0 or an error shows a failing badge and passes the "needs attention" filter; zero shows "all clear"; add, rename, reorder and remove work with error messages for `ResourceIdInvalid`, `AccessDenied`, `NotConfigured`, `RequestFailed`; the layout stays card-based on narrow screens like the existing cards.
- **Tests**: `model.test.ts` cases for attention (zero, many, error); lint and typecheck pass. No component test harness exists, so the visual flow is verified by hand.
- **Rules**: reuse `.card`, `.badge`, `Confirm`, `Spinner`, `ago()`; no frontend rules file.

## Risks

- **Azure behaviour untestable here**: URL shape `v1{resourceId}/query` and the token scope are from documentation; first signal is a 401/404 on a real instance. Mitigation: keep them in one place and record as unverified in the handover.
- **Docker is unavailable**: Aspire/Postgres checks and migration application cannot be verified here; unit tests run on the in-memory provider.
- **Secret handling**: the client secret must come from user-secrets/environment, never committed to `appsettings.json`.
- **Query cost on dashboard refresh**: N instances every 60s; bounded by `Throttled` parallelism and the resilience timeouts.

## Open questions

Assumptions made (not blocking):
- Time window is fixed at 24h for every instance (the issue's default); a selectable window is a follow-up.
- Instances are per user, matching the acceptance criteria.
- "Most recent exceptions" are exception groups (type + message + operation) with their count and latest timestamp, limited to 10.
- Management UI is on the dashboard itself since the portal has no settings area for per-user lists.

## Out of scope

Alerting and notifications, stack traces and exception detail views, requests/traces/dependencies telemetry, a user-selectable time window, sharing instances between users.
