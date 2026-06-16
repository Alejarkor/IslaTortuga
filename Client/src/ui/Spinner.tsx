export function Spinner({ label }: { label?: string }) {
  return (
    <div className="spinner" role="status" aria-live="polite">
      <span className="spinner__dot" />
      {label && <span className="spinner__label">{label}</span>}
    </div>
  );
}
