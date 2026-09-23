import { useEffect, useMemo, useRef, useState } from "react";

import type { Repository, Watch } from "../api/types";
import { IconSearch } from "../shell/icons";
import { ago } from "./model";

const MAX_HITS = 40;

interface RepositoryFinderProps {
  repositories: Repository[];
  watches: Watch[];
  onPick: (repository: string) => void;
}

/** Type to find a repository, or press / from anywhere on the page. */
export function RepositoryFinder({ repositories, watches, onPick }: RepositoryFinderProps) {
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState(false);
  const [cursor, setCursor] = useState(0);
  const box = useRef<HTMLDivElement>(null);
  const field = useRef<HTMLInputElement>(null);

  const hits = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const matches = needle
      ? repositories.filter((repository) => repository.fullName.toLowerCase().includes(needle))
      : repositories;
    return matches.slice(0, MAX_HITS);
  }, [query, repositories]);

  useEffect(() => {
    const onMouseDown = (event: MouseEvent) => {
      if (box.current && !box.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "/" && document.activeElement !== field.current) {
        event.preventDefault();
        field.current?.focus();
        setOpen(true);
      }
    };

    document.addEventListener("mousedown", onMouseDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onMouseDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  const pick = (repository: Repository) => {
    onPick(repository.fullName);
    setQuery("");
    setOpen(false);
    field.current?.blur();
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === "ArrowDown") {
      setCursor((current) => Math.min(current + 1, hits.length - 1));
      event.preventDefault();
    } else if (event.key === "ArrowUp") {
      setCursor((current) => Math.max(current - 1, 0));
      event.preventDefault();
    } else if (event.key === "Enter" && hits[cursor]) {
      pick(hits[cursor]);
      event.preventDefault();
    } else if (event.key === "Escape") {
      setOpen(false);
      field.current?.blur();
    }
  };

  const mark = (name: string) => {
    const needle = query.trim();
    const at = name.toLowerCase().indexOf(needle.toLowerCase());
    if (!needle || at < 0) {
      return name;
    }

    return (
      <>
        {name.slice(0, at)}
        <mark>{name.slice(at, at + needle.length)}</mark>
        {name.slice(at + needle.length)}
      </>
    );
  };

  return (
    <div className="finder" ref={box}>
      <div className="finder-in">
        <IconSearch />
        <input
          ref={field}
          id="repo-search"
          value={query}
          autoComplete="off"
          placeholder={
            repositories.length
              ? `Search ${repositories.length} repositories to watch a workflow`
              : "Loading repositories…"
          }
          onChange={(event) => {
            setQuery(event.target.value);
            setCursor(0);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={onKeyDown}
          role="combobox"
          aria-expanded={open}
          aria-controls="repo-menu"
        />
        <span className="kbd">/</span>
      </div>
      {open && hits.length > 0 && (
        <div className="menu" id="repo-menu" role="listbox">
          {hits.map((repository, index) => {
            const watched = watches.filter((watch) => watch.repository === repository.fullName).length;
            return (
              <button
                className="opt"
                key={repository.fullName}
                role="option"
                aria-selected={index === cursor}
                onMouseEnter={() => setCursor(index)}
                onClick={() => pick(repository)}
              >
                <span className="nm">{mark(repository.fullName)}</span>
                {repository.private && <span className="pill">private</span>}
                {watched > 0 && <span className="pill">{watched} watched</span>}
                <span className="meta">{ago(repository.pushedAt)}</span>
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
