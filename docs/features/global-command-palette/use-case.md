# Global command palette

## Summary

A keyboard-driven palette, opened with `Ctrl+K` / `Cmd+K` from anywhere in the portal, to jump to pages and
repositories, run actions and enter nested command groups. Features contribute commands through a central
registry, so the palette itself never changes when a command is added.

Source: [issue #9](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/9).

## Actors

| Actor | Role |
|---|---|
| Portal user | Opens the palette, searches, runs commands. Connected to GitHub or not. |
| Feature code | Registers commands (and their availability rules) with the registry. |

## Preconditions

- The portal is open (`frontend/`), with or without a connected GitHub account.
- Without a connection only commands that need none are listed (pages, theme, navigation toggle).
- With a connection and at least one watched repository, repository and action commands are listed too.
- At least one watched repository with a pull request is useful to try the actions.

## Main flow

1. The user presses `Ctrl+K` (`Cmd+K` on macOS), or clicks the search button in the top bar.
2. The palette opens; the search input has focus and all available commands are listed, grouped.
3. The user types; the list filters case-insensitively on name, keywords and group, best match first.
4. The user moves with `ArrowUp` / `ArrowDown`; the selected row is highlighted, kept in view and announced.
5. The user presses `Enter` on a command:
   - a navigation command changes the page and the palette closes;
   - an action command runs and the palette closes;
   - a group command opens its submenu inside the palette.
6. `Escape` closes the palette (or, in a submenu, `Backspace` on an empty input goes up one level) and focus
   returns to the element that had it before.

## Alternate flows

- **No match**: the list shows “Nothing matches …”; the palette stays open and usable.
- **Command fails**: the action throws or rejects; an error toast is shown, the palette stays open.
- **Unavailable command**: hidden, or shown disabled and skipped by `Enter`, per its availability rule.
- **Another modal is open** (e.g. the confirm dialog): the shortcut does nothing.
- **Server refuses**: commands call the same api as the buttons; the server's authorization and business rules
  still decide, and the refusal surfaces as a toast.
