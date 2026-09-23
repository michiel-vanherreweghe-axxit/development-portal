import { useCallback, useEffect, useRef, useState } from "react";

export type ToastKind = "ok" | "bad" | "info";

export interface Toast {
  id: string;
  text: string;
  kind: ToastKind;
}

const LIFETIME_MS = 4200;

export function useToasts() {
  const [toasts, setToasts] = useState<Toast[]>([]);
  const timers = useRef<ReturnType<typeof setTimeout>[]>([]);

  useEffect(() => () => timers.current.forEach(clearTimeout), []);

  const notify = useCallback((text: string, kind: ToastKind = "ok") => {
    const id = crypto.randomUUID();
    setToasts((current) => [...current, { id, text, kind }]);
    timers.current.push(
      setTimeout(() => setToasts((current) => current.filter((toast) => toast.id !== id)), LIFETIME_MS),
    );
  }, []);

  return { toasts, notify };
}

export type Notify = ReturnType<typeof useToasts>["notify"];
