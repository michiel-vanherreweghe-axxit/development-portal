# Dependency status page

## Summary

An administrator opens a page in the portal that lists the application's external dependencies with their current health: the database, the GitHub API, GitHub Actions and GitHub Pull Requests. Each row shows healthy, degraded or unhealthy and when it was last checked.

## Actors

| Actor | Description |
|---|---|
| Administrator | A signed-in user whose GitHub login is in the configured `Portal:Administrators` list |
| Signed-in user | Any other connected user; does not see the page and gets 403 from the api |
| Health publisher | Background job that runs the dependency health checks periodically and caches the latest result |

## Preconditions

- The portal api runs with `Portal:Administrators` containing the GitHub login of the test administrator (case-insensitive).
- A second connected user whose login is **not** in that list, to verify the page is closed to them.
- The api has been running for at least one publisher period (about a minute) so a first result is cached.
- No database rows are needed; this feature adds no tables.

## Main flow

1. The administrator connects GitHub in Settings.
2. The sidebar shows a **Dependencies** page; other users do not see it.
3. The administrator opens it. The portal reads the cached results from the api.
4. Each dependency (Database, GitHub API, GitHub Actions, GitHub Pull Requests) is listed with a status (healthy, degraded or unhealthy), a short description and the time of its last check.
5. The administrator presses Refresh to read the latest cached results again.

## Alternate flows

- **Not an administrator**: the page is not in the sidebar; opening `#dependencies` falls back to the default page; `GET /api/v1/dependencies` answers 403.
- **Not connected**: the api answers 401 and the portal shows the usual "not connected" message.
- **No check has run yet** (right after startup): the list is empty and the page says the first check is still pending.
- **githubstatus.com unreachable**: GitHub Actions and Pull Requests show as unhealthy with the reason in the description; the other rows are unaffected.
