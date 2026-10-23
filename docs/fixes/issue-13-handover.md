# Handover: issue #13, checks badge

## What changed

**Frontend only.** No API, migration or config change.

- `frontend/src/dashboard/model.ts:93` counts `passing` with `GREEN` (`success`, `skipped`, `neutral`) instead of `success` only.
- `model.ts:94-103` adds checks state `incomplete`: no check failing or running, but not every check green (for example `cancelled`, `action_required`). `pass` now requires every check to be green.
- `model.ts:105` derives `ready`/`mergeable` from `state === "pass"`, so the badge and the merge rule use one definition. Backend `MergePullRequest.cs` already used the same rule and is unchanged.
- `frontend/src/dashboard/PullRequestRow.tsx:27-32` renders `incomplete` as a neutral `idle` badge, `n/m passed`, without a checkmark.

## Test flows

Preconditions come from live GitHub data, not from the database, so there is nothing to seed.

1. **Cancelled check is not shown as passed**
   - Precondition: a monitored repository with an open, non-draft PR where one check succeeded and one is `cancelled` (cancel a running workflow) or `action_required`
   - Path: open the dashboard, find the repository, look at the PR row's checks badge
   - Expected: neutral grey badge `1/2 passed`, no green checkmark; the PR is not offered for merge
   - Fails as: green badge with a checkmark, or a merge action offered

2. **Skipped checks count as passed**
   - Precondition: open PR where every workflow was skipped (a docs-only change with path filters) or ended `neutral`
   - Path: open the dashboard, look at the PR row's checks badge
   - Expected: green badge `2/2 passed`; the PR can be merged if not draft and no changes requested
   - Fails as: `0/2 passed`, or a neutral badge

3. **Regression: failing and running unchanged**
   - Precondition: one PR with a `failure` check, one with a check `in_progress`
   - Path: dashboard, look at both rows
   - Expected: failing count badge and running count badge as before
   - Fails as: either shows the neutral `passed` badge

## Technical notes

- New config: none. Migrations: none. Endpoints: none.
- Tests: `frontend/src/dashboard/model.test.ts:104-124` (cancelled / action_required, skipped / neutral). Both failed before the fix.

## Deviations from the plan

None. This is a `/fix` batch and has no plan.

## Not included

- Backend merge rule: already correct, not touched.
- Other `ChecksState` consumers beyond `PullRequestRow`: none found in the diff scope; none changed.
- Walked flows: Entra sign-in and live GitHub data mean no agent clicked through these; they come from the diff and the unit tests.
