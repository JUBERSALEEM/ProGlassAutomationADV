"use client";

import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";

type RecordsHeaderProps = {
  source: "supabase" | "mock_preview";
  serverTime?: string;
  executionMs?: number;
  loading: boolean;
  onRefresh: () => void;
  density: "comfortable" | "compact";
  onToggleDensity: () => void;
  totalEntries: number;
  userEmail?: string | null;
  onSignOut?: () => void;
};

export function RecordsHeader({
  source,
  serverTime,
  executionMs,
  loading,
  onRefresh,
  density,
  onToggleDensity,
  totalEntries,
  userEmail,
  onSignOut,
}: RecordsHeaderProps) {
  const formattedTime = serverTime
    ? new Date(serverTime).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" })
    : null;

  return (
    <header className="relative border-b border-line-strong/60 bg-ink-950/80 backdrop-blur-md">
      <div className="shell py-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="eyebrow text-brand-300">Operations Suite</span>
              <span className="text-text-faint">/</span>
              <span className="eyebrow text-text-muted">Excel Sync & Database</span>
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
            </div>

            <div className="mt-1 flex flex-wrap items-baseline gap-3">
              <h1 className="text-xl font-bold tracking-tight text-text-primary sm:text-2xl">
                Records & Operations Dashboard
              </h1>
              <span className="text-xs font-mono text-text-faint">
                {totalEntries.toLocaleString()} indexed {totalEntries === 1 ? "entry" : "entries"}
              </span>
            </div>

            <p className="mt-1 text-xs text-text-muted sm:text-sm">
              Live consumption of <code className="text-brand-300 font-mono text-xs">public.records</code> synchronized from Excel Sync.
              {source === "mock_preview" && " (Using isolated preview dataset — configure NEXT_PUBLIC_SUPABASE_URL for live queries)"}
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
              onClick={onToggleDensity}
              className="text-2xs font-mono uppercase"
              title="Toggle display density between comfortable and compact"
            >
              Density: {density}
            </Button>

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

            <Link
              href="/"
              className="inline-flex h-9 items-center justify-center rounded-xl border border-line-strong/80 bg-ink-850/80 px-3 text-xs font-semibold text-text-muted transition-colors hover:bg-ink-800 hover:text-text-primary"
            >
              ← Back to Site
            </Link>
          </div>
        </div>
      </div>
    </header>
  );
}
