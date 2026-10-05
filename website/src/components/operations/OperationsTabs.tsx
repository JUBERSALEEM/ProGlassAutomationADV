"use client";

/**
 * The two halves of one operational question, one tab apart.
 *
 * Reports answers "what is in the workbook?" — every row, every status, the
 * source of truth. The Deliveries Tracker answers "how much of what we confirmed
 * has actually gone out?" — the same workbook, narrowed to the PIs that became
 * commitments, followed movement by movement.
 *
 * They are presented as tabs rather than as two unrelated pages because they
 * are two views of one dataset and an operator moves between them constantly:
 * read a PI in Reports, track its dispatch in Deliveries, come back to reconcile
 * the row. The tab is the cheapest possible connection between them, and the
 * deep links each page offers (`?search=` and `?pi=`) are the second.
 */

import { usePathname } from "next/navigation";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { pathWithoutLocale } from "@/i18n/core";
import { cn } from "@/lib/cn";

const TABS = [
  { href: "/reports", label: "Reports & Records", hint: "All statuses" },
  { href: "/deliveries", label: "Deliveries Tracker", hint: "Confirmed only" },
] as const;

export function OperationsTabs({ className }: { className?: string }) {
  const pathname = usePathname();
  const current = pathWithoutLocale(pathname || "/");

  return (
    <nav
      aria-label="Operations suite"
      className={cn(
        "inline-flex items-center gap-1 rounded-xl border border-line-strong/70 bg-ink-950/70 p-1",
        className,
      )}
    >
      {TABS.map((tab) => {
        const isActive = current === tab.href || current.startsWith(`${tab.href}/`);
        return (
          <LocaleLink
            key={tab.href}
            href={tab.href}
            aria-current={isActive ? "page" : undefined}
            className={cn(
              "group flex items-center gap-2 rounded-lg px-3 py-1.5 text-xs font-semibold transition-colors",
              isActive
                ? "bg-brand-500/25 text-brand-200 shadow-sm"
                : "text-text-muted hover:bg-ink-900/80 hover:text-text-primary",
            )}
          >
            <span>{tab.label}</span>
            <span
              className={cn(
                "hidden font-mono text-3xs uppercase tracking-wider sm:inline",
                isActive ? "text-brand-300/80" : "text-text-faint",
              )}
            >
              {tab.hint}
            </span>
          </LocaleLink>
        );
      })}
    </nav>
  );
}

export default OperationsTabs;
