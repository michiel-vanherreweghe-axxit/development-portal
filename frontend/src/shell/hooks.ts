import { useCallback, useEffect, useRef, useState, type Dispatch, type SetStateAction } from "react";

const read = (key: string) => {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
};

const write = (key: string, value: string) => {
  try {
    localStorage.setItem(key, value);
  } catch {
    // A browser with storage blocked simply forgets the preference.
  }
};

export function useRoute(ids: string[], fallback: string) {
  const [id, setId] = useState(() => window.location.hash.slice(1));

  useEffect(() => {
    const onHashChange = () => setId(window.location.hash.slice(1));
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  const go = useCallback((next: string) => {
    window.location.hash = next;
  }, []);

  return [ids.includes(id) ? id : fallback, go] as const;
}

export function useNav() {
  const [open, setOpen] = useState(() => read("nav") !== "closed");

  const toggle = useCallback(() => {
    setOpen((current) => {
      write("nav", current ? "closed" : "open");
      return !current;
    });
  }, []);

  return [open, toggle] as const;
}

export const THEMES = [
  ["system", "Auto"],
  ["light", "Light"],
  ["dark", "Dark"],
] as const;

export type Theme = (typeof THEMES)[number][0];

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(() => (read("theme") as Theme | null) ?? "system");

  useEffect(() => {
    if (theme === "system") {
      delete document.documentElement.dataset.theme;
    } else {
      document.documentElement.dataset.theme = theme;
    }
    write("theme", theme);
  }, [theme]);

  return [theme, setTheme] as const;
}

/** ⌘K / Ctrl+K anywhere opens the command palette. */
export function usePalette() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setOpen((current) => !current);
      }
    };

    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  return [open, setOpen] as const;
}

/** Closes an open popover on a click outside it or on Escape; put the ref on its box. */
export function useDismiss<T extends HTMLElement>(open: boolean, setOpen: Dispatch<SetStateAction<boolean>>) {
  const box = useRef<T>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    const onMouseDown = (event: MouseEvent) => {
      if (box.current && !box.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };

    document.addEventListener("mousedown", onMouseDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onMouseDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open, setOpen]);

  return box;
}
