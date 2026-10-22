import { useCallback, useEffect, useRef, useState } from "react";

import { ApiError } from "../api/client";
import { createPlanItem, deletePlanItem, readPlanItems, updatePlanItem } from "../api/portal";
import type { NewPlanItem, PlanItem, PlanItemChange } from "../api/types";
import type { Notify } from "../ui/useToasts";

/** The api sends its error code as the message when the problem carries no code field. */
const TEXTS: Record<string, string> = {
  "PlanItem.AlreadyPlanned": "That issue is already in your plan for that day or week.",
  "PlanItem.NotAnIssue": "That is a pull request, not an issue.",
  "PlanItem.TitleRequired": "Give the to-do a title.",
  "PlanItem.TitleTooLong": "That title is too long.",
  "PlanItem.DescriptionTooLong": "That description is too long.",
  "PlanItem.NotFound": "That entry no longer exists — it was removed elsewhere.",
  "GitHub.Unavailable": "GitHub does not show that issue — it may be gone or private.",
  "GitHub.RequestFailed": "GitHub did not answer. Try again in a minute.",
};

/** Loads the plan of one week, and saves changes to it. Mutations reload, so the view is always the api's. */
export function usePlanning(notify: Notify, onUnauthorized: () => void, enabled: boolean, from: string, to: string) {
  const [items, setItems] = useState<PlanItem[]>([]);
  const [loadedFrom, setLoadedFrom] = useState<string | null>(null);
  const [pending, setPending] = useState<number | null>(null);

  // Only the newest load may write: a slow answer for a week already left must not land on the current one.
  const loadId = useRef(0);

  const report = useCallback(
    (failure: unknown, message: string) => {
      // An expired cookie is not an error to shout about: it sends the user back to sign in.
      if (failure instanceof ApiError && failure.status === 401) {
        onUnauthorized();
        return;
      }

      if (!(failure instanceof ApiError)) {
        notify(message, "bad");
        return;
      }

      notify(TEXTS[failure.code ?? failure.message] ?? `${message} ${failure.message}`, "bad");
    },
    [notify, onUnauthorized],
  );

  const load = useCallback(async () => {
    const id = ++loadId.current;

    try {
      const loaded = await readPlanItems(from, to);
      if (id === loadId.current) {
        setItems(loaded);
        setLoadedFrom(from);
      }
    } catch (failure) {
      if (id === loadId.current) {
        setLoadedFrom(from);
      }
      report(failure, "Could not read your plan.");
    }
  }, [from, to, report]);

  useEffect(() => {
    if (enabled) {
      // Chained rather than called, so the first state lands after this effect instead of during it.
      void Promise.resolve().then(load);
    }
  }, [enabled, load]);

  const save = useCallback(
    async (run: () => Promise<unknown>, message: string, failure: string, mark: number | null = null) => {
      setPending(mark);
      try {
        await run();
        await load();
        notify(message);

        return true;
      } catch (error) {
        report(error, failure);

        return false;
      } finally {
        setPending(null);
      }
    },
    [load, notify, report],
  );

  const create = (item: NewPlanItem, message: string) =>
    save(() => createPlanItem(item), message, "Could not add that to your plan.");

  const change = (item: PlanItem, next: PlanItemChange, message: string) =>
    save(() => updatePlanItem(item.id, next), message, "Could not save the change.", item.id);

  const remove = (item: PlanItem) =>
    save(() => deletePlanItem(item.id), `Removed “${item.title}” from your plan.`, "Could not remove that.", item.id);

  return { items, loading: loadedFrom !== from, pending, report, create, change, remove };
}
