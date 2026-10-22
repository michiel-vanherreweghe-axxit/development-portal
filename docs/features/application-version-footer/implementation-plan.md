---
slug:       application-version-footer
issue:      https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/10
branch:     feat/issue-10
worktree:   /workspace/repository
convention: docs/features/<slug>/     # detected; no docs/ existed, so Shape A default
language:   en                        # repo doc language (no precedent; Shape A default)
approved:   false                     # gate 1 — set true by /feature go
database:   null                      # no data change
seeded:     false
pr:         null
artifacts:
  plan:     null
  handover: null
milestones:
  - id: 1
    title: Api version endpoint
    status: todo
    commit: null
  - id: 2
    title: Footer in the portal shell
    status: todo
    commit: null
  - id: 3
    title: Stamp the docker build with its version
    status: todo
    commit: null
---

# Implementation plan: application version in the footer

## Context

Issue [#10](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/10):

> Show the deployed application version (assembly informational version or commit SHA) in the page footer, so testers can tell which build they are looking at.
> - The footer shows the version on every page.
> - Local development shows `dev` when no version is set.

The only comment is the `/feature-plan` trigger. Feature docs: [use-case.md](use-case.md), [sequence-diagram.md](sequence-diagram.md).

## How it works today

- There is no footer. The shell renders sidebar + `mainpane` (topbar, page) and nothing below the page: `frontend/src/shell/Shell.tsx:110-139`.
- The only version shown is the *frontend's* package version in the sidebar header, injected by vite: `frontend/src/shell/Shell.tsx:59-61`, `frontend/vite.config.ts:62-69`, `frontend/src/env.d.ts:1`. It is hidden when the navigation is hidden (`frontend/src/index.css:274`) and reflects release-please, not the deployed build.
- The backend version is `0.0.0` placeholder in `backend/Directory.Build.props:3` (release-please bumps it). Nothing exposes it; the only anonymous route is `/health` (`backend/src/Api/Program.cs:52`) and `auth/sign-in` (`backend/src/Api/Endpoints/Authentication/CreateSignInEndpoint.cs:34`).
- Endpoints follow one shape: `Get(...)`, `Version(1)`, `Summary(...)` (`backend/src/Api/Endpoints/Repositories/ReadRepositoriesEndpoint.cs:10-15`), routed under `/api/v1` (`Program.cs:40-43`).
- The Docker image bakes nothing in; config comes from the environment (`backend/Dockerfile:49-50`). CI builds the image with `docker/build-push-action` and no build args (`.github/workflows/backend.yml:59-67`). There is no frontend deploy workflow.
- Frontend api calls go through `request` in `frontend/src/api/client.ts:118`, wrappers in `frontend/src/api/portal.ts`. Footer-relevant styling: `.mainpane` is a flex column (`frontend/src/index.css:305`), `.page` has bottom padding 64px (`:315`), responsive block at `:337-346`.
- Tests: endpoint tests use `PortalApiFactory` (`backend/tests/UnitTests/Endpoints/PortalApiFactory.cs`) and the Arrange/Act/Assert style in `ReadRepositoriesEndpointTests.cs`. Frontend tests are vitest `src/**/*.test.ts` in a node environment (`vite.config.ts:75-78`), so no component tests.

## Approach

The "deployed version" is the build of the api, so the api owns it: a new anonymous `GET /api/v1/version` returns `{ "version": "..." }`. The value comes from configuration key `Version` (environment variable `Version`, set at build time from the commit SHA); when it is unset or blank the endpoint answers `dev`. The endpoint reads `IConfiguration` directly — no handler, no DTO layer — because there is no logic or data access to isolate (the precedent endpoints use handlers because they call GitHub or the database).

The frontend fetches it once in a small hook and renders a `<footer>` as the last child of `mainpane` in `Shell`, pushed to the bottom with `margin-top: auto`. Because `Shell` wraps every page, the footer shows on every page and does not depend on the sidebar being visible. A failed fetch shows `unknown`.

## Alternatives rejected

- **Frontend-only version (`__APP_VERSION__`)**: already exists, but it is the package version, not the deployed build or commit SHA the issue asks for.
- **Assembly informational version as the default**: it is never empty (`0.0.0+<hash>`), which would break "shows `dev` when no version is set".
- **Version inside `/auth/me`**: requires sign-in; the footer must show before GitHub is connected.
- **Application handler + response type**: an abstraction with one trivial caller.

## Screens and the endpoints they read

| Screen | Endpoint | Called as role | Endpoint allows |
|---|---|---|---|
| Every page (footer) | `GET /api/v1/version` | anyone, signed in or not | `AllowAnonymous()` (new) |

The footer does not call any other endpoint. No `coding-rules.md` or architecture tests exist in the repo (checked: none found), so there are no rules to cite.

## Milestones

### 1. Api version endpoint
- **Files**: `backend/src/Api/Endpoints/Version/ReadVersionEndpoint.cs`, `backend/tests/UnitTests/Endpoints/ReadVersionEndpointTests.cs`
- **Acceptance**: `GET /api/v1/version` without a cookie returns 200 `{ "version": "dev" }`; with `Version=abc1234` configured it returns `abc1234`; blank counts as unset.
- **Tests**: two endpoint tests on `PortalApiFactory` (default → `dev`; configured via `WithWebHostBuilder` + `ConfigureAppConfiguration` → value). The unauthenticated client is the right one here — the factory's signed-in header is not needed.
- **Rules**: naming follows the `Read<Thing>Endpoint` precedent.

### 2. Footer in the portal shell
- **Files**: `frontend/src/api/portal.ts`, `frontend/src/api/types.ts`, `frontend/src/shell/hooks.ts` (or a new `useAppVersion.ts`), `frontend/src/shell/Shell.tsx`, `frontend/src/index.css`
- **Acceptance**: every page shows `Version <value>` at the bottom of the main pane, also with navigation hidden and at phone width; api failure shows `unknown` and nothing else breaks.
- **Tests**: `npm run lint`, `npm run build` and `npm test` stay green; vitest has no DOM environment, so the footer itself is verified by running the app (unverified in the planning session). Any pure formatting helper gets a `.test.ts`.
- **Rules**: reuse the existing mono/muted tokens (`--mono`, `--muted`) like `.topright` (`index.css:301`).

### 3. Stamp the docker build with its version
- **Files**: `backend/Dockerfile`, `.github/workflows/backend.yml`
- **Acceptance**: the image takes `ARG APP_VERSION` and sets `ENV Version=$APP_VERSION`; the workflow passes `APP_VERSION=${{ github.sha }}` (short form acceptable) as a build arg, so a deployed container shows its commit.
- **Tests**: workflow and Docker build cannot run here (no Docker daemon) — unverified; `dotnet test` unaffected.
- **Rules**: keep the Dockerfile comment style (one why-comment per step).

## Risks

- Footer is hidden by a layout quirk on short pages — signal: footer mid-page on a near-empty page; mitigated by `margin-top: auto` in the flex column.
- CORS: the endpoint is called with `credentials: "include"` like the others, so the existing policy applies.
- Docker/CI change is unverified until the pipeline runs on the PR.

## Open questions

Assumptions, stated:

1. "Version" means the **api build** (commit SHA in deployments), not the frontend package version. The existing sidebar frontend version stays as is.
2. The assembly informational version is **not** used as a fallback, so unset means `dev` as the acceptance criterion requires. Say so if you would rather have it.
3. There is no frontend deploy pipeline in the repo, so only the api image is stamped.
4. The full SHA is used in the footer unless you want it shortened (cheap to change in the workflow).

## Out of scope

- Showing the frontend build SHA, or both versions.
- Changing release-please, version bumping or tagging.
- A version/about page or a health-check payload change.
