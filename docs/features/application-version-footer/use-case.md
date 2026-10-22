# Application version in the footer

## Summary

Every page of the portal ends with a footer that shows the version of the deployed api (a commit SHA or release number), so testers can tell which build they are looking at. Without a configured version, the footer shows `dev`.

Source: [issue #10](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/10).

## Actors

| Actor | Goal |
|---|---|
| Tester | Know which build of the portal they are testing |
| Developer | See `dev` locally without configuring anything |
| Deployment pipeline | Stamp the build with its version |

## Preconditions

- The portal frontend is open; GitHub does not need to be connected (the version is public, so the footer also shows on the not-connected Settings flow).
- Deployed: the api container was built with `APP_VERSION` set (commit SHA). Local: nothing is set.
- Seed data: none needed.

## Main Flow

1. The tester opens any page of the portal.
2. The frontend asks the api for its version once, without credentials mattering.
3. The api answers with the configured version, or `dev` when none is set.
4. The footer at the bottom of the page shows `Version <value>`.
5. Navigating to another page keeps the same footer.

## Alternate Flows

- **Api unreachable**: the footer shows `Version unknown`; the rest of the page behaves as before.
- **Navigation hidden**: the footer is part of the main pane, so it stays visible.
- **Narrow screen**: the footer wraps under the page content, no horizontal scroll.
