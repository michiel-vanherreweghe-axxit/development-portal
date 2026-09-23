import { Fragment, useEffect, useMemo, useRef, useState } from "react";

import type { Command } from "./types";

const MAX_HITS = 25;

/** Pages, watched repositories and actions, on ⌘K / Ctrl+K. */
export function Palette({ items, onClose }: { items: Command[]; onClose: () => void }) {
  const [query, setQuery] = useState("");
  const [cursor, setCursor] = useState(0);
  const field = useRef<HTMLInputElement>(null);

  const hits = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const matches = needle
      ? items.filter((item) => `${item.label} ${item.group}`.toLowerCase().includes(needle))
      : items;
    return matches.slice(0, MAX_HITS);
  }, [query, items]);

  useEffect(() => field.current?.focus(), []);

  const run = (item: Command) => {
    onClose();
    item.run();
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "ArrowDown") {
      setCursor((current) => Math.min(current + 1, hits.length - 1));
      event.preventDefault();
    } else if (event.key === "ArrowUp") {
      setCursor((current) => Math.max(current - 1, 0));
      event.preventDefault();
    } else if (event.key === "Enter" && hits[cursor]) {
      run(hits[cursor]);
      event.preventDefault();
    } else if (event.key === "Escape") {
      onClose();
      event.preventDefault();
    }
  };

  return (
    <div
      className="pal-back"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="pal" role="dialog" aria-modal="true" aria-label="Command palette">
        <input
          ref={field}
          value={query}
          onChange={(event) => {
            setQuery(event.target.value);
            setCursor(0);
          }}
          onKeyDown={onKeyDown}
          placeholder="Jump to a page, a repository or an action…"
          aria-label="Search commands"
          role="combobox"
          aria-expanded="true"
          aria-controls="pal-results"
          autoComplete="off"
        />
        <div className="results" id="pal-results" role="listbox">
          {hits.length === 0 && <p className="none">Nothing matches “{query}”.</p>}
          {hits.map((item, index) => {
            const heading = item.group === hits[index - 1]?.group ? null : item.group;
            return (
              <Fragment key={`${item.group}/${item.label}`}>
                {heading && <div className="grp">{heading}</div>}
                <button
                  className="it"
                  role="option"
                  aria-selected={index === cursor}
                  onMouseEnter={() => setCursor(index)}
                  onClick={() => run(item)}
                >
                  {item.icon}
                  <span className="nm">{item.label}</span>
                  {item.hint && <span className="hint">{item.hint}</span>}
                </button>
              </Fragment>
            );
          })}
        </div>
      </div>
    </div>
  );
}
