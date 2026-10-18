import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { ButtonLink } from "./Button";

type FeedbackTone = "empty" | "error" | "success" | "unavailable" | "info";

const toneClasses: Record<FeedbackTone, string> = {
  empty: "border-line-strong/65 bg-ink-900/72",
  unavailable: "border-line-strong/65 bg-ink-900/72",
  info: "border-state-info/55 bg-state-info/10",
  error: "border-state-danger/55 bg-state-danger/10",
  success: "border-state-success/55 bg-state-success/10",
};

/** Shared empty/error/success/unavailable surface. It communicates with text
 * and semantics, never color alone. */
export function Feedback({
  tone,
  title,
  description,
  action,
  compact = false,
  className,
}: {
  tone: FeedbackTone;
  title: ReactNode;
  description?: ReactNode;
  action?: { label: ReactNode; href: string };
  compact?: boolean;
  className?: string;
}) {
  const live = tone === "error" ? "assertive" : tone === "success" ? "polite" : undefined;
  return (
    <section
      className={cn("feedback-state rounded-card border text-center", compact ? "p-4" : "card-pad", toneClasses[tone], className)}
      aria-live={live}
      data-feedback-tone={tone}
    >
      <p className={cn("font-semibold text-text-primary", compact ? "text-sm" : "text-base")}>{title}</p>
      {description ? <div className="mx-auto mt-2 max-w-prose text-sm leading-relaxed text-text-muted">{description}</div> : null}
      {action ? <ButtonLink href={action.href} variant="secondary" size="sm" className="mt-4">{action.label}</ButtonLink> : null}
    </section>
  );
}
