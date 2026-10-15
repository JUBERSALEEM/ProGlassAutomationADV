"use client";

import { Badge, IconChip } from "@/components/ui/Badge";
import { footerLinkIcons } from "@/data/footerIcons";
import { Button } from "@/components/ui/Button";
import { LocaleLink } from "@/components/ui/LocaleLink";
import { OperationsTabs } from "@/components/operations/OperationsTabs";
import type { JobOrdersApiData } from "@/types/job-orders";

type JobOrdersHeaderProps = {
  source: JobOrdersApiData["source"];
  serverTime?: string;
  executionMs?: number;
  totalJobOrders: number;
  loading: boolean;
  onRefresh: () => void;
  userEmail?: string | null;
  onSignOut?: () => void;
  currentSourceFile?: string | null;
  sourceFileFilter?: string;
  isCurrentSourceScoped?: boolean;
};

export function JobOrdersHeader({
  source,
  serverTime,
  executionMs,
  totalJobOrders,
  loading,
  onRefresh,
  userEmail,
  onSignOut,
  currentSourceFile,
  sourceFileFilter,
  isCurrentSourceScoped,
}: JobOrdersHeaderProps) {
  const explicitSourceFile = sourceFileFilter && sourceFileFilter !== "current" && sourceFileFilter !== "all"
    ? sourceFileFilter
    : null;
  const displayedSourceFile = sourceFileFilter === "all"
    ? "All workbooks"
    : explicitSourceFile || currentSourceFile;
  const isCurrentScope = Boolean(isCurrentSourceScoped) &&
    (!explicitSourceFile || explicitSourceFile === currentSourceFile);
  const updatedAt = serverTime
    ? new Date(serverTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" })
    : null;

  return (
    <header className="relative border-b border-line-strong/60 bg-ink-950/90">
      <div className="shell py-4">
        <div className="flex flex-col gap-3.5 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="eyebrow text-brand-300">Operations Suite</span>
              <span className="text-text-faint">/</span>
              <span className="eyebrow text-text-muted">Production hand-off</span>
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
              <Badge tone="accent" className="font-mono">Confirmed only</Badge>
              {displayedSourceFile && (
                <span className="inline-flex max-w-full items-center gap-1.5 rounded-md border border-line-strong/80 bg-ink-900/90 px-2 py-0.5 font-mono text-2xs text-text-secondary">
                  <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-glass-teal" />
                  <span className="shrink-0 text-text-faint">Workbook:</span>
                  <strong className="truncate text-brand-200" title={displayedSourceFile}>
                    {displayedSourceFile === "All workbooks"
                      ? displayedSourceFile
                      : displayedSourceFile.split(/[\\/]/).pop() || displayedSourceFile}
                  </strong>
                  {isCurrentScope && (
                    <span className="shrink-0 rounded bg-brand-500/20 px-1 py-0.5 text-3xs font-semibold uppercase text-brand-300">
                      Current
                    </span>
                  )}
                </span>
              )}
            </div>

            <OperationsTabs className="mt-3" />

            <div className="mt-1 flex flex-wrap items-baseline gap-3">
              <span className="flex items-center gap-3">
                <IconChip path={footerLinkIcons["/job-orders"]!} />
                <h2 className="text-xl font-bold tracking-tight text-text-primary sm:text-2xl">Job Orders</h2>
              </span>
              <span className="font-mono text-xs text-text-faint">
                {totalJobOrders.toLocaleString()} confirmed {totalJobOrders === 1 ? "PI" : "PIs"} ready for hand-off
              </span>
            </div>
            <p className="mt-1 max-w-3xl text-xs leading-relaxed text-text-muted sm:text-sm">
              Active <code className="font-mono text-brand-300">public.records</code> rows with exact status <strong className="text-text-secondary">Confirmed</strong> can be reviewed and released as a website Production Job Order. The source PI remains unchanged; each released job snapshots its source lines and tracks planned/completed quantities through its production stages.
            </p>
          </div>

          <div className="flex shrink-0 flex-wrap items-center gap-2.5">
            {updatedAt && (
              <span className="hidden font-mono text-2xs text-text-faint sm:inline-block">
                Updated {updatedAt}{executionMs ? ` (${executionMs}ms)` : ""}
              </span>
            )}
            <Button variant="secondary" size="sm" onClick={onRefresh} loading={loading} className="text-2xs font-mono uppercase">
              Refresh
            </Button>
            {userEmail && (
              <div className="flex items-center gap-2 rounded-xl border border-line-strong/70 bg-ink-900/80 px-3 py-1.5 shadow-sm">
                <span className="h-2 w-2 rounded-full bg-state-success animate-pulse" />
                <span className="max-w-44 truncate font-mono text-2xs text-text-secondary">{userEmail}</span>
                {onSignOut && (
                  <button
                    type="button"
                    onClick={onSignOut}
                    className="ml-1 text-2xs font-semibold text-text-muted underline transition-colors hover:text-state-warning"
                  >
                    Sign Out
                  </button>
                )}
              </div>
            )}
            <LocaleLink
              href="/reports?status=Confirmed"
              className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-3 text-xs font-semibold text-text-muted transition-colors hover:bg-ink-800 hover:text-text-primary"
            >
              ← Confirmed PIs
            </LocaleLink>
          </div>
        </div>
      </div>
    </header>
  );
}
