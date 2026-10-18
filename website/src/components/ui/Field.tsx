import { cloneElement, isValidElement, useId, type ReactElement, type ReactNode } from "react";
import { cn } from "@/lib/cn";

type FieldState = "default" | "error" | "success" | "readonly";
type FieldSize = "sm" | "md" | "lg";

type ControlProps = {
  id?: string;
  "aria-describedby"?: string;
  "aria-invalid"?: boolean;
  "aria-readonly"?: boolean;
  className?: string;
};

const sizes: Record<FieldSize, string> = {
  sm: "min-h-9 px-3 py-2 text-xs",
  md: "min-h-11 px-3.5 py-2.5 text-sm",
  lg: "min-h-12 px-4 py-3 text-base",
};

const states: Record<FieldState, string> = {
  default: "border-line-strong/75 focus:border-brand-400",
  error: "border-state-danger/80 focus:border-state-danger",
  success: "border-state-success/75 focus:border-state-success",
  readonly: "border-line/65 bg-ink-900/45 text-text-muted",
};

/** Semantic field wrapper. The caller supplies a native input/select/textarea;
 * the wrapper supplies the approved label, help/error relationship and visual
 * states without hiding native behavior behind a custom control. */
export function Field({
  label,
  control,
  hint,
  message,
  state = "default",
  size = "md",
  optional,
  className,
}: {
  label: ReactNode;
  control: ReactElement<ControlProps>;
  hint?: ReactNode;
  message?: ReactNode;
  state?: FieldState;
  size?: FieldSize;
  optional?: ReactNode;
  className?: string;
}) {
  const generated = useId();
  const controlId = control.props.id ?? `${generated}-control`;
  const hintId = hint ? `${generated}-hint` : undefined;
  const messageId = message ? `${generated}-message` : undefined;
  const describedBy = [control.props["aria-describedby"], hintId, messageId].filter(Boolean).join(" ") || undefined;

  if (!isValidElement(control)) throw new TypeError("Field control must be a React element");

  const enhanced = cloneElement(control, {
    id: controlId,
    "aria-describedby": describedBy,
    "aria-invalid": state === "error" || undefined,
    "aria-readonly": state === "readonly" || undefined,
    className: cn(
      "field-control w-full rounded-input border-2 bg-ink-950/70 text-text-primary outline-none transition-[border-color,box-shadow,background-color] duration-200 placeholder:text-text-faint disabled:cursor-not-allowed disabled:opacity-55 focus-visible:ring-2 focus-visible:ring-brand-400/55",
      sizes[size], states[state], control.props.className,
    ),
  });

  return (
    <div className={cn("field-group", className)} data-field-state={state}>
      <label htmlFor={controlId} className="flex items-baseline justify-between gap-3 text-sm font-semibold text-text-primary">
        <span>{label}</span>
        {optional ? <span className="text-xs font-normal text-text-muted">{optional}</span> : null}
      </label>
      <div className="mt-2">{enhanced}</div>
      {hint ? <p id={hintId} className="mt-1.5 text-xs leading-relaxed text-text-muted">{hint}</p> : null}
      {message ? (
        <p
          id={messageId}
          role={state === "error" ? "alert" : "status"}
          className={cn(
            "mt-1.5 text-xs font-medium leading-relaxed",
            state === "error" && "text-state-danger",
            state === "success" && "text-state-success",
            (state === "default" || state === "readonly") && "text-text-muted",
          )}
        >
          {message}
        </p>
      ) : null}
    </div>
  );
}
