"use client";

import { useId, useState, type ReactNode } from "react";
import { useT } from "@/i18n/provider";
import { cn } from "@/lib/cn";

/**
 * Disclosure list. Used for FAQ sections and the technical-detail panels.
 * Real <button> + aria-expanded, so it is operable from the keyboard and
 * announced correctly, and the panel height animates with grid-template-rows
 * (which the browser can transition without measuring anything).
 */
export function Accordion({
  items,
  defaultOpen = -1,
  className,
}: {
  /**
   * One row. `answer` is the plain-text case, which is what an FAQ needs;
   * `body` is the same disclosure holding structure — a list of capabilities,
   * an evidence line — for the panels that would otherwise be a wall of items
   * on a phone. Exactly one of the two is rendered, and `body` wins.
   */
  items: { id?: string; question: string; answer?: string; body?: ReactNode }[];
  defaultOpen?: number;
  className?: string;
}) {
  const t = useT();
  const [open, setOpen] = useState(defaultOpen);
  const baseId = useId();

  return (
    /*
     * An accordion is a card, so it is drawn as one — the same tier-2 surface
     * as every other elevated panel on the site. In Crystal it is a sheet of
     * glass with the questions stacked on it; in the other families it is the
     * flat card it has always been.
     */
    <div className={cn("surface-raised divide-y divide-line/60 overflow-hidden", className)}>
      {items.map((item, index) => {
        const expanded = open === index;
        const panelId = `${baseId}-panel-${index}`;
        const buttonId = `${baseId}-button-${index}`;
        return (
          <div key={`${item.id ?? item.question}-${index}`}>
            <h3>
              <button
                id={buttonId}
                type="button"
                aria-expanded={expanded}
                aria-controls={panelId}
                onClick={() => setOpen(expanded ? -1 : index)}
                className="flex w-full items-start justify-between gap-4 px-5 py-4 text-left transition-colors duration-200 hover:bg-ink-850/50 sm:px-6 sm:py-5"
              >
                <span className="text-sm font-medium text-text-primary sm:text-base">{t(item.question)}</span>
                <span
                  aria-hidden="true"
                  className={cn(
                    "mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-lg border transition-all duration-300",
                    expanded ? "border-brand-400/60 bg-brand-400/12 text-brand-200" : "border-line-strong/70 text-text-muted",
                  )}
                >
                  <svg viewBox="0 0 12 12" className="h-2.5 w-2.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round">
                    <path d="M2 6h8" />
                    <path d="M6 2v8" className={cn("origin-center transition-transform duration-300", expanded && "scale-y-0")} />
                  </svg>
                </span>
              </button>
            </h3>
            <div
              id={panelId}
              role="region"
              aria-labelledby={buttonId}
              aria-hidden={!expanded}
              inert={!expanded}
              className={cn(
                "accordion-panel grid transition-[grid-template-rows] duration-300 ease-industrial",
                expanded ? "grid-rows-[1fr]" : "grid-rows-[0fr]",
              )}
            >
              <div className="overflow-hidden">
                {item.body ? (
                  <div className="px-5 pb-5 sm:px-6 sm:pb-6">{item.body}</div>
                ) : (
                  <p className="px-5 pb-5 text-sm leading-relaxed text-text-muted sm:px-6 sm:pb-6">
                    {t(item.answer ?? "")}
                  </p>
                )}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
