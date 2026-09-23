import { useCallback, useEffect, useRef, useState } from "react";

import { ApiError } from "../api/client";
import { readPullRequests, readRepositories, readRuns, readWatches, updateWatches } from "../api/portal";
import type { PullRequest, Repository, WatchRuns, Watches } from "../api/types";
import type { Notify } from "../ui/useToasts";

const REFRESH_MS = 60_000;

const DEFAULT_WATCHES: Watches = { runLimit: 10, watches: [] };

/** Loads the dashboard, keeps it fresh, and saves changes to the watch list. */
export function usePortalData(notify: Notify, onUnauthorized: () => void, enabled: boolean) {
  const [repositories, setRepositories] = useState<Repository[]>([]);
  const [watches, setWatches] = useState<Watches>(DEFAULT_WATCHES);
  const [rows, setRows] = useState<WatchRuns[]>([]);
  const [pullRequests, setPullRequests] = useState<PullRequest[] | null>(null);
  const [busy, setBusy] = useState(enabled);
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [pending, setPending] = useState<string | null>(null);

  // The list the next change builds on, which is not the same as the last one the api answered:
  // a second edit can be made while the first is still in flight.
  const latestWatches = useRef(watches);

  // Only the newest load may write: a slow fan-out must not land on top of a newer one.
  const loadId = useRef(0);

  useEffect(() => {
    latestWatches.current = watches;
  }, [watches]);

  const report = useCallback(
    (failure: unknown, message: string, texts: Record<string, string> = {}) => {
      // An expired cookie is not an error to shout about: it sends the user back to sign in.
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      if (!(failure instanceof ApiError)) {
        notify(message, "bad");
        return;
      }

      // The api sends its error code as the message when the problem carries no code field.
      notify(texts[failure.code ?? failure.message] ?? `${message} ${failure.message}`, "bad");
    },
    [notify, onUnauthorized],
  );

  const load = useCallback(async () => {
    const id = ++loadId.current;

    try {
      const [runs, pulls] = await Promise.all([readRuns(), readPullRequests()]);
      if (id !== loadId.current) {
        return;
      }

      setRows(runs);
      setPullRequests(pulls);
      setUpdatedAt(new Date());
    } catch (failure) {
      report(failure, "Could not reach the portal api.");
    } finally {
      if (id === loadId.current) {
        setBusy(false);
      }
    }
  }, [report]);

  const refresh = useCallback(async () => {
    setBusy(true);
    await load();
  }, [load]);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    readRepositories()
      .then(setRepositories)
      .catch((failure: unknown) => report(failure, "Could not read your repositories."));

    readWatches()
      .then((loaded) => {
        latestWatches.current = loaded;
        setWatches(loaded);
      })
      .catch((failure: unknown) => report(failure, "Could not read your watched workflows."));

    // Chained rather than called, so the first state lands after this effect instead of during it.
    void Promise.resolve().then(load);

    const timer = setInterval(() => void refresh(), REFRESH_MS);
    return () => clearInterval(timer);
  }, [enabled, load, refresh, report]);

  /**
   * The api replaces the whole watch list, so every change sends the next full state. The change
   * is applied to the newest list rather than to a copy captured at render, so a second edit made
   * while the first is saving does not send the first one back undone. `mark` is the row that
   * should show as pending while it saves.
   */
  const commit = useCallback(
    async (change: (current: Watches) => Watches, message: string, mark: string | null) => {
      const next = change(latestWatches.current);
      latestWatches.current = next;

      setPending(mark);
      setBusy(true);
      try {
        setWatches(await updateWatches(next));
        await refresh();
        notify(message);
      } catch (failure) {
        report(failure, "Could not save the change.");
      } finally {
        setPending(null);
        setBusy(false);
      }
    },
    [notify, refresh, report],
  );

  /** A write that changes something on GitHub rather than in the portal: run it, then reload. */
  const act = useCallback(
    async (run: () => Promise<void>, message: string, mark: string, failed: Record<string, string>) => {
      setPending(mark);
      setBusy(true);
      try {
        await run();
        await load();
        notify(message);
      } catch (failure) {
        report(failure, "Could not do that.", failed);
      } finally {
        setPending(null);
        setBusy(false);
      }
    },
    [load, notify, report],
  );

  return { repositories, watches, rows, pullRequests, busy, updatedAt, pending, refresh, commit, act };
}
