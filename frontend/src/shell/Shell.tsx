import type { ReactNode } from "react";

import type { CurrentUser } from "../api/types";
import { IconMark, IconMenu, IconSearch, ThemeIcon } from "./icons";
import { Palette } from "./Palette";
import { THEMES, useNav, usePalette, useRoute, useTheme } from "./hooks";
import type { Command, PageDefinition } from "./types";

interface ShellProps {
  pages: PageDefinition[];
  commands: Command[];
  status: ReactNode;
  busy: boolean;
  user: CurrentUser | null;
  /** Where to land when the hash names no page. */
  fallbackPage: string;
}

const groupOf = (page: PageDefinition) => page.group ?? "Monitoring";

export function Shell({ pages, commands, status, busy, user, fallbackPage }: ShellProps) {
  const [navOpen, toggleNav] = useNav();
  const [route, go] = useRoute(
    pages.map((page) => page.id),
    fallbackPage,
  );
  const [paletteOpen, setPaletteOpen] = usePalette();
  const [theme, setTheme] = useTheme();

  const page = pages.find((candidate) => candidate.id === route)!;

  const at = Math.max(
    0,
    THEMES.findIndex(([id]) => id === theme),
  );
  const [themeId, themeLabel] = THEMES[at]!;
  const [nextTheme, nextLabel] = THEMES[(at + 1) % THEMES.length]!;

  const allCommands: Command[] = [
    ...pages.map((candidate) => ({
      group: "Pages",
      label: candidate.title,
      icon: candidate.icon,
      hint: `#${candidate.id}`,
      run: () => go(candidate.id),
    })),
    ...commands,
    { group: "Actions", label: "Hide or show the navigation", run: toggleNav },
  ];

  return (
    <div className={`app${navOpen ? "" : " nonav"}`}>
      <aside className="side">
        <div className="sidehead">
          <span className={`logo${busy ? " busy" : ""}`}>
            <IconMark />
          </span>
          <span className="name">DevPortal</span>
          <span className="ver" title="Frontend version">
            v{__APP_VERSION__}
          </span>
        </div>

        <nav className="nav" aria-label="Pages">
          {[...new Set(pages.map(groupOf))].map((group) => (
            <div className="navgroup" key={group}>
              <div className="navlabel">{group}</div>
              {pages
                .filter((candidate) => groupOf(candidate) === group)
                .map((candidate) => (
                  <button
                    className="navitem"
                    key={candidate.id}
                    aria-current={candidate.id === route}
                    onClick={() => go(candidate.id)}
                  >
                    {candidate.icon}
                    <span className="nm">{candidate.title}</span>
                    {candidate.badge ? <b>{candidate.badge}</b> : null}
                  </button>
                ))}
            </div>
          ))}
        </nav>

        <div className="sidefoot">
          <span className="userchip">
            {user ? (
              <>
                {user.avatarUrl && <img src={user.avatarUrl} alt="" />}
                <span>{user.login}</span>
              </>
            ) : (
              <span>GitHub not connected</span>
            )}
          </span>
          <button
            type="button"
            className="themebtn"
            onClick={() => setTheme(nextTheme)}
            title={`Theme: ${themeLabel}. Switch to ${nextLabel}.`}
            aria-label={`Theme: ${themeLabel}. Switch to ${nextLabel}.`}
          >
            <ThemeIcon of={themeId} />
            <span>{themeLabel}</span>
          </button>
        </div>
      </aside>

      <div className="mainpane">
        <header className="topbar">
          <button
            className="navtoggle"
            onClick={toggleNav}
            aria-expanded={navOpen}
            aria-label={navOpen ? "Hide navigation" : "Show navigation"}
            title={navOpen ? "Hide navigation" : "Show navigation"}
          >
            <IconMenu />
          </button>
          <button className="cmdk" onClick={() => setPaletteOpen(true)}>
            <IconSearch size={13} />
            Search pages, repositories, actions
            <span className="kbd">⌘K</span>
          </button>
          <div className="topright">{status}</div>
        </header>

        <div className="page">
          <div className="pagehead">
            <div>
              <h1>{page.title}</h1>
              <p>{page.hint}</p>
            </div>
            {page.action}
          </div>
          {page.render()}
        </div>
      </div>

      {paletteOpen && <Palette items={allCommands} onClose={() => setPaletteOpen(false)} />}
    </div>
  );
}
