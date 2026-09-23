import { useCallback, useEffect, useState } from "react";

import { ApiError } from "../api/client";
import { connectGitHub, disconnectGitHub, readCurrentUser } from "../api/portal";
import type { CurrentUser } from "../api/types";

/** undefined while the session is still being read, null when GitHub is not connected. */
export function useSession() {
  const [user, setUser] = useState<CurrentUser | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    readCurrentUser()
      .then((current) => {
        if (!cancelled) {
          setUser(current);
        }
      })
      .catch((failure: unknown) => {
        if (cancelled) {
          return;
        }

        setUser(null);
        // Not connected is the expected answer; anything else is worth saying out loud.
        if (!(failure instanceof ApiError && failure.status === 401)) {
          setError(failure instanceof Error ? failure.message : "The portal api could not be reached.");
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const connect = useCallback(async (personalAccessToken: string) => {
    const current = await connectGitHub(personalAccessToken);
    setError(null);
    setUser(current);
  }, []);

  const disconnect = useCallback(async () => {
    try {
      await disconnectGitHub();
    } finally {
      setUser(null);
    }
  }, []);

  /** A call that came back 401 means the session expired mid-visit. */
  const expired = useCallback(() => setUser(null), []);

  return { user: user ?? null, error, loading: user === undefined, connect, disconnect, expired };
}

export type Session = ReturnType<typeof useSession>;
