---
slug:       global-command-palette
issue:      https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/9
branch:     feat/issue-9
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
    title: Command model, registry and search
    status: todo
    commit: null
  - id: 2
    title: Palette UI - nesting, accessibility, focus, failures, shortcut guard
    status: todo
    commit: null
  - id: 3
    title: Move existing commands onto the registry
    status: todo
    commit: null
  - id: 4
    title: Feature docs and registration guide
    status: todo
    commit: null
---

# Implementation plan: Global command palette

## Context

[Issue #9](https://github.com/michiel-vanherreweghe-axxit/development-portal/issues/9): "Introduce a global
command palette that allows users to quickly navigate the application, search for resources, and execute
common actions using a keyboard-driven interface", inspired by GitHub's. Required: global `Ctrl/Cmd+K`,
filtering by name and keywords, arrow/Enter/Escape, navigation + action + nested commands, grouping,
contextual and permission-aware commands, a **centralized registration mechanism** features use without
touching the palette core, accessibility, failure handling, tests, and registration docs.

The issue comment is only `/feature-plan`; no further requirements.

## How it works today

The premise "introduce" is partly outdated: a basic palette exists. This plan evolves it.

- `usePalette` toggles on `Ctrl/Cmd+K` via a document keydown listener, with no check for other modals
  (`frontend/src/shell/hooks.ts:80-95`). The top bar button also opens it (`frontend/src/shell/Shell.tsx:116-120`).
- `Palette` filters by `label + group` substring only, caps at 25 hits, and has `role=combobox`/`listbox`/`option`
  but no `aria-activedescendant`, no scroll-into-view, no focus restore, no empty-input handling for nesting
  (`frontend/src/shell/Palette.tsx:8-94`). `run` closes first then calls `item.run()`, so errors are unhandled
  (`Palette.tsx:25-28`).
- `Command` is `{group,label,hint,icon,run}` - no id, keywords, description, shortcut, availability, children
  (`frontend/src/shell/types.ts:17-23`).
- Commands are assembled in `Shell` from pages + a `commands` prop + a hard-coded "hide navigation" action
  (`Shell.tsx:43-54`); `Portal` builds repository/action commands inline from its state
  (`frontend/src/Portal.tsx:213-243`), empty when GitHub is not connected. That is the only "permission" notion
  on the client; real authorization is on the api (actions go through `data.act`, which maps api errors to toasts,
  `Portal.tsx:95-128`).
- Routing is the hash (`useRoute`, `hooks.ts:21-35`). Pages are data (`PageDefinition`, `types.ts:6-15`).
- `Confirm` is the only other modal (`aria-modal`, own Escape + focus trap, restores focus; `frontend/src/ui/Confirm.tsx:15-57`).
  `RepositoryFinder` listens for `/` on document (`frontend/src/dashboard/RepositoryFinder.tsx:38`).
- Toasts: `useToasts().notify(text, "bad")` (`frontend/src/ui/useToasts.ts:15-24`).
- Palette styles: `frontend/src/index.css:349-371`.
- Tests: vitest, `environment: "node"`, `src/**/*.test.ts` only, no DOM library; the single test file is pure logic
  (`frontend/vite.config.ts:19-22`, `frontend/src/dashboard/model.test.ts`). Dependencies are React only
  (`frontend/package.json`).
- Absent: no `docs/`, no `coding-rules.md`, no architecture tests, no router, no user roles on the client.
  `CLAUDE.md` absent. Precedent for "logic pure in `.ts`, tested with vitest": `dashboard/model.ts`.

## Approach

Frontend-only; no api or data change.

- **Registry** (`frontend/src/shell/commands/`): a React context holding registered command sets, plus
  `useCommands(commands)` that registers on mount/change and unregisters on unmount. Features call the hook from
  where they live; a page-level component registers contextual commands that exist only while it is mounted.
  The palette reads the registry and knows nothing about features.
- **Command model**: `id`, `label`, `description?`, `group`, `keywords?`, `shortcut?` (display metadata),
  `icon?`, `hidden?(): boolean`, `disabled?(): boolean`, `keepOpen?`, and either `run(): void | Promise<void>` or
  `children: Command[]` (submenu). Visuals stay in the palette.
- **Search** (pure `search.ts`): case-insensitive; rank label-prefix > word-prefix > label substring > keyword >
  group; ties ordered by group then label (deterministic). Same substring approach as today, not fuzzy.
- **Shortcut**: one `PALETTE_SHORTCUT` definition (`mod+k`, `mod` = Cmd on macOS, Ctrl elsewhere) used for
  matching and for the displayed hint ("configurable" = one place to change; no settings UI).
  Ignored while another `aria-modal` dialog (e.g. `Confirm`) is open.
- **Presentation**: breadcrumb for nested groups, `Backspace` on empty input goes up one level, `aria-activedescendant`,
  selected row scrolled into view, focus restored to the previously focused element, disabled commands shown
  muted and skipped on `Enter`. A command that throws/rejects shows an error toast and keeps the palette open.
- **Authorization**: client-side `hidden`/`disabled` only hide things (e.g. repository commands need a connected
  account); commands call the same api paths as the buttons, so the server remains the authority (AC9).

## Alternatives rejected

- **A command-palette library (cmdk, kbar)**: new dependency for ~100 lines of existing UI, and the registry
  needs to be ours anyway.
- **Fuzzy search**: explicitly out of scope unless needed.
- **Registry as a module-level singleton**: simpler, but leaks across tests and Portal remounts
  (`App.tsx:17` remounts on account change); context + unregister-on-unmount is clean.
- **Router change to make navigation commands URL-based**: the hash route already works.

## Screens and the endpoints they read

No new or changed page. The palette adds no api calls of its own; its commands reuse calls that exist:

| Screen | Endpoint | Called as role | Endpoint allows |
|---|---|---|---|
| Palette "Refresh now" | existing dashboard refresh (`usePortalData`) | session user | unchanged, session-cookie gated |
| Palette pull request actions (if registered) | existing close/merge calls in `frontend/src/api/portal.ts` | session user | unchanged |

Backend authorization was not re-read: no backend change is planned. See Open questions.

## Milestones

### 1. Command model, registry and search
- **Files**: `frontend/src/shell/types.ts`, new `frontend/src/shell/commands/{registry.tsx,search.ts,shortcut.ts}`, tests next to them.
- **Acceptance**: `useCommands` registers/unregisters; available commands exclude `hidden`; `search` ranks as specified and is deterministic; shortcut matcher handles Cmd (mac) / Ctrl.
- **Tests**: vitest node tests for `search` and shortcut matching (pure); registry logic kept as a pure reducer so it is testable without a DOM.
- **Rules**: none found in repo; follow `dashboard/model.ts` precedent (pure logic in `.ts`).

### 2. Palette UI: nesting, accessibility, focus, failures, shortcut guard
- **Files**: `frontend/src/shell/Palette.tsx`, `hooks.ts` (`usePalette`), `index.css`, `frontend/package.json` (devDependencies `jsdom`, `@testing-library/react`, `@testing-library/user-event`), `vite.config.ts` (include `*.test.tsx`).
- **Acceptance**: AC1-AC4, AC7, AC11, AC12: input focused on open; Esc closes and restores focus; arrows/Enter work; selected row visible and exposed through `aria-activedescendant`; empty state; submenu enter/exit; failing command toasts and keeps palette open; shortcut ignored while `Confirm` is open.
- **Tests**: component tests with per-file `// @vitest-environment jsdom` (keyboard, search, empty state, failure, focus return, shortcut guard). Screen-reader announcement itself is not observable in jsdom: verify roles/attributes in tests, announcement by hand.
- **Rules**: none in repo; reuse `Confirm`'s focus-restore pattern (`Confirm.tsx:21-56`), `notify` from `useToasts`.

### 3. Move existing commands onto the registry
- **Files**: `frontend/src/shell/Shell.tsx`, `frontend/src/Portal.tsx`, optionally a small `useCommands` call per feature.
- **Acceptance**: pages, repositories, "Watch a workflow", attention toggle, refresh, navigation toggle all appear via `useCommands`, with ids; repository and action commands `hidden` when GitHub not connected; a Theme submenu (Auto/Light/Dark) demonstrates nesting (AC5, AC6, AC8, AC9, AC10); `Shell` no longer concatenates command lists; the top bar hint shows the platform shortcut.
- **Tests**: a component test that registering a new command from a test component makes it appear with no palette change (AC10) and that unregistering removes it.
- **Rules**: none in repo. Categories are the existing groups (Pages, Repositories, Actions) plus Settings/Help only if a command needs them; the issue's Planning/Tasks examples are not created.

### 4. Feature docs and registration guide
- **Files**: `docs/features/global-command-palette/registering-commands.md`, `frontend/README.md` (pointer + test note for jsdom).
- **Acceptance**: a feature developer can add a command (flat, contextual, nested, conditional) from the guide alone.
- **Tests**: `npm run lint`, `npm run test`, `npm run build` green.
- **Rules**: docs in English, matching `use-case.md` / `sequence-diagram.md` in the same folder.

## Risks

- jsdom/testing-library additions change `package-lock.json`; early signal: `npm ci` diff. Mitigation: keep to three devDependencies.
- Hooks registering on every render can loop; signal: re-render warnings in tests. Mitigation: registry compares by ids and stores the latest closures in a ref.
- `Ctrl+K` collides with browser/OS shortcuts in some browsers (already `preventDefault`ed today); checked by hand in Chrome, Firefox, Safari.
- `/` shortcut in `RepositoryFinder` should not fire while the palette input is focused; verify in the component test.

## Open questions

Assumptions made (no blocking questions):

- The existing palette is the starting point, not a rewrite from scratch.
- Test DOM tooling (jsdom + Testing Library) may be added as devDependencies, since the Definition of Done asks for tested keyboard interaction. Alternative: pure-logic tests only, with keyboard behaviour verified by hand.
- "Configurable shortcut" means a single definition in code, not a user-facing setting.
- No roles exist on the client; "permissions" means connected/not connected plus server-side checks. Backend endpoint role gates were not re-read since no backend change is planned.
- Nested groups are shown inside the same palette with a breadcrumb, not as a second dialog.
- Plan artifact (Dutch) is not published by this run; `artifacts.plan` stays null.
- Checks needing Docker are not applicable (frontend only); nothing was run in this planning step.

## Out of scope

AI/natural-language commands, user-defined commands, analytics or personalised ranking, OS-level commands,
fuzzy search, command history / recents, a shortcut-settings UI, and any backend change.
