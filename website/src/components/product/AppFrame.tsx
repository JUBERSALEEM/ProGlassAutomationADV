import type { ReactNode } from "react";
import { cn } from "@/lib/cn";

/**
 * The ProGlass application frame.
 *
 * HONESTY NOTE (project brief §41):
 * The shell below is a structural reconstruction of the real application
 * chrome — the navigation rail groups, their labels, the top-bar elements and
 * the module titles are read verbatim out of `MainWindow.xaml` and
 * `Themes/Languages/en-US.xaml`. The *content area* is not a screen capture:
 * when no capture exists for a module, the frame renders an explicit
 * "capture pending" plate so the site never presents a mock-up as a real
 * screenshot of the product.
 */

export type RailItem = { label: string; active?: boolean };
export type RailGroup = { label: string; items: RailItem[]; open?: boolean };

/**
 * The real sidebar grouping, in the real order
 * (Views/Personal/GlassShell.xaml: MAIN · INVENTORY · SALES · PRODUCTION · DISPATCH ·
 *  FINANCE & ACCOUNTING · REPORTS · MASTER DATA · SYSTEM).
 */
export const realRail: RailGroup[] = [
  { label: "MAIN", open: true, items: [{ label: "Dashboard" }] },
  {
    label: "INVENTORY",
    open: true,
    items: [{ label: "Sheet Store" }, { label: "Specifications" }],
  },
  {
    label: "SALES",
    open: true,
    items: [{ label: "Inquiry" }, { label: "Proforma Invoice" }, { label: "Job Order" }, { label: "Tax Invoice" }],
  },
  {
    label: "PRODUCTION",
    items: [
      { label: "Optimization" },
      { label: "Process Work Center" },
      { label: "Production Control Center" },
      { label: "Cutting" },
      { label: "Holes & Cutouts" },
      { label: "Polish" },
      { label: "Edge Deletion" },
      { label: "Mitring" },
      { label: "Tempering" },
      { label: "Pairing" },
      { label: "Doubling / Lamination" },
    ],
  },
  { label: "DISPATCH", items: [{ label: "Delivery Note" }] },
  {
    label: "FINANCE",
    items: [
      { label: "Statement Generator" },
      { label: "Order Report" },
      { label: "Job Profitability" },
      { label: "VAT Submission" },
    ],
  },
  {
    label: "REPORTS",
    items: [
      { label: "Executive Summary" },
      { label: "Customers" },
      { label: "Salesmen" },
      { label: "Companies" },
      { label: "Projects" },
      { label: "Proforma Invoice" },
      { label: "Job Order" },
      { label: "Delivery" },
      { label: "Tax Invoice" },
      { label: "Glass Specs" },
      { label: "Profitability" },
      { label: "Statement" },
    ],
  },
  {
    label: "MASTER DATA",
    items: [{ label: "Company Center" }, { label: "User Center" }],
  },
  {
    label: "SYSTEM",
    items: [{ label: "System Center" }, { label: "Diagnostics" }, { label: "Settings" }],
  },
];

function RailRow({ label, active, muted }: { label: string; active?: boolean; muted?: boolean }) {
  return (
    <span
      className={cn(
        "flex h-6 items-center gap-2 truncate rounded-[0.3rem] px-1.5 text-[0.6875rem] leading-none",
        active
          ? "bg-brand-400/16 font-semibold text-brand-100 ring-1 ring-inset ring-brand-400/35"
          : muted
            ? "text-text-faint/80"
            : "text-text-muted/85",
      )}
    >
      {!muted ? (
        <span
          aria-hidden="true"
          className={cn("h-1 w-1 shrink-0 rounded-full", active ? "bg-brand-300" : "bg-line-strong")}
        />
      ) : null}
      <span className="truncate">{label}</span>
    </span>
  );
}

export function AppFrame({
  /** Module title shown in the top bar — must match the real module name. */
  title,
  railActive,
  children,
  note,
  className,
  compact,
}: {
  title: string;
  railActive?: string;
  children: ReactNode;
  note?: ReactNode;
  className?: string;
  compact?: boolean;
}) {
  return (
    <figure className={cn("plate shadow-raised", className)}>
      {/* ── Window chrome ─────────────────────────────────────────── */}
      <div className="flex items-center justify-between gap-3 border-b border-line/70 bg-ink-900/85 px-3 py-2 sm:px-4">
        <div className="flex items-center gap-2.5">
          <span
            aria-hidden="true"
            className="grid h-5 w-5 place-items-center rounded-[0.3rem] bg-gradient-to-br from-brand-400 to-brand-600 text-[0.5rem] font-bold text-accent-ink"
          >
            PG
          </span>
          <span className="font-mono text-[0.625rem] font-semibold uppercase tracking-[0.18em] text-text-faint">
            ProGlass Automation
          </span>
        </div>
        <div className="flex items-center gap-1.5" aria-hidden="true">
          <span className="h-2.5 w-2.5 rounded-[0.15rem] border border-line-strong/80" />
          <span className="h-2.5 w-2.5 rounded-[0.15rem] border border-line-strong/80" />
          <span className="h-2.5 w-2.5 rounded-[0.15rem] border border-line-strong/80" />
        </div>
      </div>

      <div className="flex">
        {/* ── Navigation rail ─────────────────────────────────────── */}
        {!compact ? (
          <nav
            aria-hidden="true"
            className="hidden w-44 shrink-0 flex-col gap-2 border-r border-line/60 bg-ink-950/70 p-2.5 sm:flex"
          >
            {realRail.map((group) => (
              <div key={group.label} className="space-y-0.5">
                <p className="px-1.5 pb-0.5 font-mono text-[0.5625rem] font-semibold tracking-[0.16em] text-text-faint/70">
                  {group.label}
                </p>
                <div className="space-y-px">
                  {group.items.slice(0, group.open ? group.items.length : 2).map((item) => (
                    <RailRow key={item.label} label={item.label} active={item.label === railActive} />
                  ))}
                </div>
              </div>
            ))}
          </nav>
        ) : null}

        {/* ── Workspace ───────────────────────────────────────────── */}
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-3 border-b border-line/60 bg-ink-900/45 px-3 py-2">
            <p className="truncate text-xs font-semibold text-text-primary sm:text-sm">{title}</p>
            <div className="flex items-center gap-2" aria-hidden="true">
              <span className="hidden font-mono text-[0.5625rem] tracking-[0.14em] text-text-faint sm:inline">10:30</span>
              <span className="h-5 w-5 rounded-[0.3rem] border border-line-strong/70 bg-ink-800" />
              <span className="flex items-center gap-1.5 rounded-[0.35rem] border border-line-strong/70 bg-ink-800 px-1.5 py-0.5">
                <span className="grid h-3.5 w-3.5 place-items-center rounded-full bg-brand-400/20 text-[0.4375rem] font-bold text-brand-200">
                  AB
                </span>
                <span className="font-mono text-[0.5rem] uppercase tracking-[0.1em] text-text-faint">Admin</span>
              </span>
            </div>
          </div>

          <div className="min-h-[13rem] bg-ink-850/30 p-3 sm:min-h-[16rem] sm:p-5">{children}</div>
        </div>
      </div>

      {note ? (
        <figcaption className="border-t border-line/60 bg-ink-950/60 px-3 py-2.5 text-xs leading-relaxed text-text-faint sm:px-4">
          {note}
        </figcaption>
      ) : null}
    </figure>
  );
}

/**
 * The content placeholder used wherever a real screen capture does not exist
 * yet. It is deliberately explicit — the site never dresses a mock-up up as a
 * capture of the running product.
 */
export function CapturePending({
  module,
  detail,
  soon = "Screen capture pending",
}: {
  module: string;
  detail?: string;
  soon?: string;
}) {
  return (
    <div className="flex h-full min-h-[11rem] flex-col items-center justify-center gap-3 rounded-card border border-dashed border-line-strong/70 bg-ink-950/40 p-5 text-center sm:min-h-[14rem]">
      <span
        aria-hidden="true"
        className="grid h-10 w-10 place-items-center rounded-[0.6rem] border border-line-strong/60 bg-ink-900"
      >
        <svg viewBox="0 0 24 24" className="h-5 w-5 text-brand-300/70" fill="none" stroke="currentColor" strokeWidth="1.5">
          <rect x="3" y="5" width="18" height="14" rx="2" />
          <circle cx="9" cy="10.5" r="1.6" />
          <path d="M4 17l4.6-4.2L12 16l3-2.6L20 17" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </span>
      <div className="space-y-1">
        <p className="text-sm font-semibold text-text-secondary">{module}</p>
        <p className="font-mono text-2xs uppercase tracking-[0.18em] text-brand-300/80">{soon}</p>
      </div>
      {detail ? <p className="max-w-sm text-xs leading-relaxed text-text-faint">{detail}</p> : null}
    </div>
  );
}
