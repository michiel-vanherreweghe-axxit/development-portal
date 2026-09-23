import type { Toast } from "./useToasts";

const MARKS = { ok: "✓", bad: "✕", info: "•" };

export function Toasts({ items }: { items: Toast[] }) {
  return (
    <div className="toasts" role="status" aria-live="polite">
      {items.map((toast) => (
        <div className={`toast ${toast.kind}`} key={toast.id}>
          <span className="mark">{MARKS[toast.kind]}</span>
          <span>{toast.text}</span>
        </div>
      ))}
    </div>
  );
}
