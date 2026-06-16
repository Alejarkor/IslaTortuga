import type { ReactNode } from "react";

/** Panel con título, usado en las tres zonas del pre-juego. */
export function Panel({
  title,
  children,
  className = ""
}: {
  title?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`panel ${className}`}>
      {title && <h2 className="panel__title">{title}</h2>}
      <div className="panel__body">{children}</div>
    </section>
  );
}
