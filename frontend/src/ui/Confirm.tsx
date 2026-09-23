import { useEffect, useRef, type ReactNode } from "react";

export interface Question {
  title: string;
  detail: ReactNode;
  verb: string;
  cancel?: string;
  onYes: () => void;
}

/** Owns its backdrop, focus trap and Escape key, so it needs no dialog element. */
export function Confirm({ ask, onClose }: { ask: Question | null; onClose: () => void }) {
  const panel = useRef<HTMLDivElement>(null);
  const yes = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!ask) {
      return;
    }

    const returnTo = document.activeElement;
    yes.current?.focus();

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }

      if (event.key !== "Tab" || !panel.current) {
        return;
      }

      const stops = panel.current.querySelectorAll("button");
      const first = stops[0];
      const last = stops[stops.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        last?.focus();
        event.preventDefault();
      } else if (!event.shiftKey && document.activeElement === last) {
        first?.focus();
        event.preventDefault();
      }
    };

    document.addEventListener("keydown", onKeyDown);
    const scroll = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = scroll;
      (returnTo as HTMLElement | null)?.focus?.();
    };
  }, [ask, onClose]);

  if (!ask) {
    return null;
  }

  return (
    <div
      className="modal-back"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <div className="modal" ref={panel} role="dialog" aria-modal="true" aria-labelledby="ask-title">
        <h3 id="ask-title">{ask.title}</h3>
        <p>{ask.detail}</p>
        <div className="acts">
          <button className="ghost" onClick={onClose}>
            {ask.cancel ?? "Keep watching"}
          </button>
          <button
            className="btn"
            ref={yes}
            onClick={() => {
              onClose();
              ask.onYes();
            }}
          >
            {ask.verb}
          </button>
        </div>
      </div>
    </div>
  );
}
