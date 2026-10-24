"use client";

import { LocaleLink } from "@/components/ui/LocaleLink";

/**
 * The inbox header, compact by design.
 *
 * It carries the title, one sentence about what this register is, how many
 * filters are narrowing it, the way to the website submission register, the
 * refresh control and who is signed in. Everything else belongs on the cards.
 */
export function InboxHeader({
  email,
  activeFilters,
  refreshing,
  onRefresh,
  onSignOut,
}: {
  email: string | null;
  activeFilters: number;
  refreshing: boolean;
  onRefresh: () => void;
  onSignOut: () => void;
}) {
  return (
    <header className="border-b border-line-strong/60 bg-ink-950/80">
      <div className="shell flex flex-wrap items-center justify-between gap-x-6 gap-y-3 py-4">
        <div className="min-w-0">
          <div className="eyebrow text-brand-300">Internal operations</div>
          <div className="mt-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <h1 className="text-xl font-bold tracking-tight sm:text-2xl">Inquiry Inbox</h1>
            <span className="text-3xs font-semibold uppercase tracking-wider text-text-faint">
              {activeFilters === 0 ? "No filters" : `${activeFilters} active filter${activeFilters === 1 ? "" : "s"}`}
            </span>
          </div>
          <p className="mt-1 max-w-xl text-xs leading-relaxed text-text-muted">
            The desktop inquiry register, published read only. Proformas started here are recorded on this website and are not yet shared with the desktop.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {email ? (
            <span className="hidden max-w-[16rem] truncate rounded-xl border border-line-strong/60 px-3 py-2 text-3xs text-text-muted sm:inline" title={email}>
              {email}
            </span>
          ) : null}
          <LocaleLink
            href="/inquiries/customer-submissions"
            className="rounded-xl border border-brand-400/70 bg-brand-500/10 px-3 py-2 text-xs font-bold text-brand-200 hover:bg-brand-500/20"
          >
            Website submissions
          </LocaleLink>
          <button
            type="button"
            onClick={onRefresh}
            disabled={refreshing}
            className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary disabled:opacity-60"
          >
            {refreshing ? "Refreshing" : "Refresh"}
          </button>
          <button
            type="button"
            onClick={onSignOut}
            className="rounded-xl border border-line-strong px-3 py-2 text-xs font-semibold text-text-muted hover:text-text-primary"
          >
            Sign out
          </button>
        </div>
      </div>
    </header>
  );
}
