import type { ReactNode } from "react";
import { Icon } from "./Icon";
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
  /*
   * Filled, not outlined.
   *
   * A 10%-alpha fill with a 35%-alpha border is a chip that disappears on a
   * deep surface — the two weakest values available, used for the one element
   * whose whole job is to be noticed at a glance. Each tone now carries a real
   * fill and a lit top edge, which is the same treatment the application gives
   * its own status pills, and the daylight contrast guard in `globals.css`
   * re-points the text tones so they stay legible on ivory.
   */
  const tones = {
    neutral: "border-line-strong/80 bg-ink-800/90 text-text-secondary",
    accent:
      "border-brand-500/50 bg-brand-600/30 text-brand-200 shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
    success:
      "border-state-success/50 bg-state-success/22 text-state-success shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
    warning:
      "border-state-warning/50 bg-state-warning/22 text-state-warning shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
    danger:
      "border-state-danger/50 bg-state-danger/22 text-state-danger shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
  } as const;

  return (
    <span
      className={cn(
        "badge-pill inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-3xs font-bold uppercase tracking-[0.1em]",
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
        /*
         * The number that marks a module or a stage. It is a small piece of the
         * brand rather than a label, so it takes the accent as a fill and the
         * figure as text: a 12%-alpha wash with accent type on it reads as a
         * mark, where an outlined box with the same type reads as a button that
         * does not work.
         */
        "index-chip inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-[0.6rem] border border-brand-500/45 bg-brand-600/25 font-mono text-xs font-bold text-brand-200 shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
        className,
      )}
    >
      {children}
    </span>
  );
}

/** Small square glyph tile — the icon twin of `IndexChip`. */
export function IconChip({ path, className }: { path: string; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        /*
         * The icon that names a card or a stage, drawn in `IndexChip`'s own
         * tile: same accent wash, same lit top edge, same corner radius — so a
         * numbered step and a pictured step read as one family of marks. The
         * glyph inside follows the site-wide 16-unit grid, slightly smaller
         * than the tile's own numeral so dense paths (trucks, keys) stay airy.
         */
        "icon-chip grid h-9 w-9 shrink-0 place-items-center rounded-[0.6rem] border border-brand-500/45 bg-brand-600/25 text-brand-200 shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]",
        className,
      )}
    >
      <Icon path={path} className="h-4 w-4" />
    </span>
  );
}
