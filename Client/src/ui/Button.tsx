import type { ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "ghost" | "danger";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant;
};

/** Botón reutilizable con variantes. Botones grandes y claros (sección 18). */
export function Button({
  variant = "secondary",
  className = "",
  ...rest
}: ButtonProps) {
  return <button className={`btn btn--${variant} ${className}`} {...rest} />;
}
