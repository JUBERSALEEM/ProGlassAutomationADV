"use client";

import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { OperationsTabs } from "@/components/operations/OperationsTabs";
import { LocaleLink } from "@/components/ui/LocaleLink";

type DeliveriesHeaderProps = {
  source: "supabase" | "mock_preview";
  serverTime?: string;
  executionMs?: number;
  loading: boolean;
  onRefresh: () => void;
  totalPis: number;
  confirmedStatuses: string[];
  ledgerInstalled: boolean;
  userEmail?: string | null;
  onSignOut?: () => void;
  currentSourceFile?: string | null;
  isCurrentSourceScoped?: boolean;
};

/**
 * The tracker's masthead.
 *
 * It states the one rule that makes this page different from Reports — which
 * status values count as confirmed — because a reader who does not know that
 * rule will misread the totals. The rule is rendered from the running
 * configuration, not from a hard-coded sentence, so a project that widened
 * `PROGLASS_CONFIRMED_STATUSES` sees its own statuses named back to it.
 */
export function DeliveriesHeader({
  source,
  serverTime,
  executionMs,
  loading,
  onRefresh,
  totalPis,
  confirmedStatuses,
  ledgerInstalled,
  userEmail,
  onSignOut,
  currentSourceFile,
  isCurrentSourceScoped,
}: DeliveriesHeaderProps) {
  const formattedTime = serverTime
    ? new Date(serverTime).toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      })
    : null;

  return (
    <header className="relative border-b border-line-strong/60 bg-ink-950/80 backdrop-blur-md">
      <div className="shell py-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="eyebrow text-brand-300">Operations Suite</span>
              <span className="text-text-faint">/</span>
              <span className="eyebrow text-text-muted">Dispatch & Balance</span>
              {source === "supabase" ? (
                <Badge tone="success" className="gap-1.5">
                  <span className="h-1.5 w-1.5 rounded-full bg-state-success animate-pulse" />
                  Supabase Live
                </Badge>
              ) : (
                <Badge tone="warning" className="gap-1.5">
                  <span className="h-1.5 w-1.5 rounded-full bg-state-warning" />
                  Demo Preview
                </Badge>
              )}
              <Badge tone={ledgerInstalled ? "accent" : "neutral"} className="text-2xs font-mono">
                {ledgerInstalled ? "Ledger connected" : "Ledger not installed"}
              </Badge>
              {currentSourceFile && (
                <span className="inline-flex items-center gap-1.5 rounded-md border border-line-strong/80 bg-ink-900/90 px-2 py-0.5 text-2xs font-mono text-text-secondary">
                  <span className="h-1.5 w-1.5 rounded-full bg-glass-teal" />
                  <span className="text-text-faint">Workbook:</span>
                  <strong className="text-brand-200 truncate max-w-[200px]" title={currentSourceFile}>
                    {currentSourceFile.split(/[\\/]/).pop() || currentSourceFile}
                  </strong>
                  {isCurrentSourceScoped && (
                    <span className="rounded bg-brand-500/20 px-1 py-0.2 text-3xs font-semibold text-brand-300 uppercase">
                      Current Active
                    </span>
                  )}
                </span>
              )}
            </div>

            <OperationsTabs className="mt-3" />

            <div className="mt-1 flex flex-wrap items-baseline gap-3">
              <h1 className="text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
                Deliveries Tracker
              </h1>
              <span className="text-xs font-mono text-text-faint">
                {totalPis.toLocaleString()} confirmed {totalPis === 1 ? "PI" : "PIs"} tracked
              </span>
            </div>

            <p className="mt-1 max-w-3xl text-xs text-text-muted sm:text-sm">
              Reads{" "}
              <code className="font-mono text-xs text-brand-300">public.records</code> for every PI
              marked{" "}
              <strong className="font-semibold text-text-secondary">
                {confirmedStatuses.join(" / ") || "Confirmed"}
              </strong>
              , plus any PI that already carries a delivery movement, and balances it against{" "}
              <code className="font-mono text-xs text-brand-300">public.delivery_entries</code>. One
              PI may hold several movements — a part load is a row, not an edit.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2.5 shrink-0">
            {formattedTime && (
              <span className="text-2xs font-mono text-text-faint hidden sm:inline-block">
                Updated {formattedTime} {executionMs ? `(${executionMs}ms)` : ""}
              </span>
            )}

            <Button
              variant="secondary"
              size="sm"
              onClick={onRefresh}
              loading={loading}
              className="text-2xs font-mono uppercase"
            >
              Refresh
            </Button>

            {userEmail ? (
              <div className="flex items-center gap-2 rounded-xl border border-line-strong/70 bg-ink-900/80 px-3 py-1.5 shadow-sm">
                <span className="h-2 w-2 rounded-full bg-state-success animate-pulse" />
                <span className="text-2xs font-mono text-text-secondary">{userEmail}</span>
                {onSignOut && (
                  <button
                    type="button"
                    onClick={onSignOut}
                    className="ml-1 text-2xs font-semibold text-text-muted hover:text-state-warning underline transition-colors"
                  >
                    Sign Out
                  </button>
                )}
              </div>
            ) : null}

            <LocaleLink
              href="/reports"
              className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-3 text-xs font-semibold text-text-muted transition-colors hover:bg-ink-800 hover:text-text-primary"
            >
              ← Reports
            </LocaleLink>
          </div>
        </div>
      </div>
    </header>
  );
}
