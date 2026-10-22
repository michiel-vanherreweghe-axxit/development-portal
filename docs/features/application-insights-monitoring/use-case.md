# Application Insights monitoring

## Summary

A signed-in user adds Azure Application Insights instances to the portal, by resource ID, the same way they watch repositories. Each instance gets a card on the dashboard next to the repository cards, showing the number of exceptions thrown in the last 24 hours and the most recent ones. Zero exceptions is healthy; anything above zero stands out like a failed workflow run. An instance the portal cannot query shows an error state, never a healthy-looking empty card.

Source: https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/3

## Actors

| Actor | Role |
|---|---|
| Portal user | Signed in with a GitHub token; manages their own instances and reads the dashboard |
| Portal operator | Configures the one Entra app registration (tenant id, client id, client secret) and grants it Monitoring Reader on each instance |
| Azure Application Insights | Answers the `exceptions` query over its REST API |

## Preconditions

- The portal has an `AppInsights` configuration section (tenant id, client id, client secret). Without it the feature is off: the endpoints answer with an error state per instance and nothing is queried.
- The Entra app registration has the Monitoring Reader role on every instance a user adds.
- The user is provisioned (signed in with GitHub at least once).
- For seed data: a user with two or three instances, one with a resource ID the app registration can read, one it cannot (error state).

## Main flow

1. The user opens the dashboard's settings for Application Insights and pastes an Azure resource ID (`/subscriptions/<id>/resourceGroups/<group>/providers/microsoft.insights/components/<name>`) and a display name.
2. The portal rejects anything that is not a well-formed resource ID (`AppInsightsInstance.ResourceIdInvalid`) and saves the instance, after the user's existing ones.
3. The user can rename, reorder or remove instances; each change saves the whole list, like watches.
4. The dashboard loads and, per instance, the portal queries the `exceptions` table for the last 24 hours: total count, and the most recent exception groups (type, message, operation, latest timestamp, count).
5. Each instance card shows the count and the list, newest first. Zero exceptions: "all clear". More than zero: a failing badge such as "12 exceptions in 24h".
6. The dashboard refreshes every minute, like the repository cards.

## Alternate flows

- **Cannot read the instance** (403 or 404 from Azure, missing role, wrong resource ID): the card shows an error ("cannot read this instance — check Monitoring Reader") and counts as needing attention. One unreadable instance never fails the others.
- **Azure unreachable or token request fails**: the same error state, with a different message key.
- **Portal not configured**: every card shows "Application Insights is not configured on this portal".
- **Duplicate resource ID**: dropped silently, as duplicate watches are.
- **No instances**: the section shows an empty hint and nothing else.
