export function Spinner({ label = "Working" }: { label?: string }) {
  return <span className="spin" role="status" aria-label={label} />;
}
