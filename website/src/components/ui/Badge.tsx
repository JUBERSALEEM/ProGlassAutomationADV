import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

export function Badge({
  children,
  tone = "neutral",
  className,
}: {
  children: ReactNode;
  tone?: "neutral" | "accent" | "success" | "warning" | "danger";
  className?: string;
}) {
  const tones = {
    neutral: "border-line-strong/70 bg-ink-850/80 text-text-muted",
    accent: "border-brand-400/35 bg-brand-400/10 text-brand-200",
    success: "border-state-success/35 bg-state-success/10 text-state-success",
    warning: "border-state-warning/35 bg-state-warning/10 text-state-warning",
    danger: "border-state-danger/35 bg-state-danger/10 text-state-danger",
  } as const;

  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-2xs font-semibold uppercase tracking-[0.12em]",
        tones[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}

/** Small square numeric index used to number modules and workflow stages. */
export function IndexChip({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[0.6rem] border border-line-strong/70 bg-ink-800/80 font-mono text-xs font-semibold text-brand-300",
        className,
      )}
    >
      {children}
    </span>
  );
}
