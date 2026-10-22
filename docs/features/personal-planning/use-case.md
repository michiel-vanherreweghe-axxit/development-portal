# Personal planning

## Summary

A signed-in user plans their own work per day or per week from one page. A plan entry is either an existing GitHub issue (from any repository the user's token can read) or a manual to-do. Completion is personal: it never touches the GitHub issue. Source: [issue #8](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/8).

## Actors

| Actor | Description |
|---|---|
| User | Signed in with a GitHub personal access token; sees and edits only their own plan |
| GitHub | Source of issue title, state and link; never written to |

## Preconditions

- The user is signed in (GitHub token connected in Settings) and provisioned in the `Users` table.
- For issue entries: at least one repository the token can read that has issues.
- For the unavailable-issue flow: a plan entry whose issue was deleted, transferred, or is in a repository the token can no longer read.
- Seed data: one user with a to-do today, a to-do in the current week, a completed to-do, an issue entry on today, and an issue entry pointing at a nonexistent issue number.

## Main flow

1. The user opens **Planning**. The view shows today (day mode); a toggle switches to week mode (Monday to Sunday).
2. The user moves between days or weeks with previous / next / today controls.
3. The user adds a manual to-do: title (required), description (optional), and a target (a day, or the week). It appears in the view and survives a refresh.
4. The user adds a GitHub issue: picks a repository, searches its issues, selects one, and chooses a day or the week. The entry shows title, `owner/repo#number`, GitHub state and a link.
5. The user ticks an entry as done. Only the personal flag changes; the entry stays in the view, struck through.
6. The user moves an entry to another day or week. It disappears from the old target and shows on the new one.
7. The user edits or deletes a to-do, or removes an issue entry from the plan. The GitHub issue is untouched.
8. In week mode, entries planned for a specific day of that week are listed under their day; weekly entries are listed in a "This week" group.

## Alternate flows

- **Duplicate issue**: adding an issue already planned for the same day (or same week) is refused with "already planned"; the same issue on a different day or week is allowed.
- **Unavailable issue** (404, 410, 403 without rate-limit): the entry is shown with its last known title and an "unavailable on GitHub" marker; the rest of the plan renders and the entry can still be completed, moved or removed.
- **GitHub temporarily failing** (rate limit, timeout, 5xx): entries show their last known title with a "could not refresh" marker; to-dos are unaffected.
- **Token expired** (401): the user is sent back to Settings, as on the existing dashboard.
- **Other user's entry**: reading, updating or deleting an entry id that belongs to someone else returns 404.
- **Not connected**: the page shows the same "connect GitHub" prompt as the repositories page.
