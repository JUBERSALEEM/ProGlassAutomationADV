"use client";

import type { ReactNode } from "react";
import { Icon } from "./Icon";
import { cn } from "@/lib/cn";

/**
 * The "nothing matched" panel for the operations tables.
 *
 * Every dashboard's empty queue gets the same furniture so an operator
 * moving between Records, Job Orders and Deliveries reads the same room:
 * a ghost glyph of the thing the table would hold (in the site's own
 * 16-unit hand, not a borrowed 24-grid face), one headline that names the
 * state, one sentence that explains why the state is the right one, and
 * — when a reset exists — the button that gets the data back.
 */
export function EmptyState({
  glyph,
  title,
  body,
  action,
  className,
}: {
  /** 16-unit grid path for the tile, same hand as the header icons. */
  glyph: string;
  title: ReactNode;
  body: ReactNode;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("rounded-2xl border border-line-strong/60 bg-ink-900/80 p-12 text-center shadow-panel", className)}>
      <div className="mx-auto grid h-14 w-14 place-items-center rounded-2xl border border-brand-500/45 bg-brand-600/25 text-brand-200 shadow-[inset_0_1px_0_0_rgb(255_255_255/0.08)]">
        <Icon path={glyph} className="h-6 w-6" />
      </div>
      <h3 className="mt-4 text-base font-bold text-text-primary">{title}</h3>
      <p className="mx-auto mt-1.5 max-w-sm text-xs leading-relaxed text-text-muted">{body}</p>
      {action ? <div className="mt-5">{action}</div> : null}
    </div>
  );
}
