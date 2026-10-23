# Fixes — issue #13

Source: michiel-vanherreweghe-axxit/development-portal#13

- [x] **cancelled / action-required checks show as passed** — *"One of the checks on my PR was cancelled, but the row still showed a green ✓ badge."*
      `model.test.ts › does not call a pull request with a %s check passed` · defect `frontend/src/dashboard/model.ts:94-100`
      red: `AssertionError: expected {…} to match object { state: 'incomplete', … }` → green
- [x] **skipped checks counted as not passed** — *"A docs-only PR where every workflow was skipped shows ✓ 0/2 passed."*
      `model.test.ts › counts skipped and neutral checks as passed` · defect `model.ts:93`
      red: `expected {…} to match object { passing: 2, state: 'pass', … }` → green

Handover: https://claude.ai/code/artifact/96e5dbf9-2bb4-43c4-acd9-6f9497efc0cd

Both fixed in `buildPullRequests`: `passing` counts `GREEN`, state is `pass` only when every check is green, otherwise new `incomplete` (neutral badge without ✓). `mergeable`/`ready` now derive from that same state. Backend `MergePullRequest.cs` already used the same green rule; unchanged.
