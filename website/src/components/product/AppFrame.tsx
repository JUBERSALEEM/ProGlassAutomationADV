import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { LogoMark } from "@/components/brand/Logo";
import type { Translator } from "@/i18n/core";

/**
 * The ProGlass application frame.
 *
 * HONESTY NOTE (project brief §41):
 * The shell below is a structural reconstruction of the real application
 * chrome — the navigation rail groups, their labels, the top-bar elements and
 * the module titles are read verbatim out of `Views/Personal/GlassShell.xaml`
 * and `Themes/Languages/en-US.xaml`. The content area is not a screen capture:
 * it holds a live, interactive preview of the module (`LiveSurface`), carrying
 * the module's own tabs and field names over clearly labelled sample data, so
 * the site never presents a mock-up as a photograph of the product.
 */

export type RailItem = { label: string; active?: boolean };
export type RailGroup = { label: string; items: RailItem[]; open?: boolean };

/**
 * The navigation rail, as `MainWindow.xaml` — and the Personal shell,
 * `Views/Personal/GlassShell.xaml`, which mirrors it — actually builds it.
 *
 * Three flat items, then seven expandable groups. The group labels are *module
 * names*, not section headings: "Sales & Orders", "Production", "Dispatch",
 * "Finance & Accounting", "Reports", "Master Data", "System". The children are
 * the real ones:
 *
 *   Sales & Orders        Inquiry · Proforma Invoice · Job Order · Tax Invoice
 *   Production            Optimization · Cutting · Holes & Cutouts · Polish ·
 *                         Edge Deletion · Mitring · Tempering · Pairing ·
 *                         Doubling / Lamination
 *   Dispatch              Delivery Note
 *   Finance & Accounting  Statement Generator · Job Profitability · VAT Submission
 *   Reports               Overview · Proforma Invoice · Job Order · Delivery ·
 *                         Tax Invoice · Statement
 *   Master Data           Company Center · User Center
 *   System                System Center · Logs & Activity
 *
 * Process Work Center, Production Control Center, the Order Report and the
 * Settings and About surfaces are reached from inside these modules rather than
 * from the rail, so the site documents them as modules but does not draw them
 * as rail entries.
 */
export const realRail: RailGroup[] = [
  {
    label: "Main",
    open: true,
    items: [{ label: "Dashboard" }, { label: "Sheet Store" }, { label: "Specifications" }],
  },
  {
    label: "Sales & Orders",
    open: true,
    items: [{ label: "Inquiry" }, { label: "Proforma Invoice" }, { label: "Job Order" }, { label: "Tax Invoice" }],
  },
  {
    label: "Production",
    items: [
      { label: "Optimization" },
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
  { label: "Dispatch", items: [{ label: "Delivery Note" }] },
  {
    label: "Finance & Accounting",
    items: [{ label: "Statement Generator" }, { label: "Job Profitability" }, { label: "VAT Submission" }],
  },
  {
    label: "Reports",
    items: [
      { label: "Overview" },
      { label: "Proforma Invoice" },
      { label: "Job Order" },
      { label: "Delivery" },
      { label: "Tax Invoice" },
      { label: "Statement" },
    ],
  },
  { label: "Master Data", items: [{ label: "Company Center" }, { label: "User Center" }] },
  { label: "System", items: [{ label: "System Center" }, { label: "Logs & Activity" }] },
];

function RailRow({ label, active, muted }: { label: string; active?: boolean; muted?: boolean }) {
  return (
    <span
      className={cn(
        "flex h-6 items-center gap-2 truncate rounded-[0.3rem] px-1.5 text-[0.75rem] leading-none",
        active
          ? "bg-brand-400/16 font-semibold text-brand-100 ring-1 ring-inset ring-brand-400/35"
          : muted
            ? "text-text-faint"
            : "text-text-muted",
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
  /** The page's translator — this component renders on the server. */
  t,
  /** Module title shown in the top bar — must match the real module name. */
  title,
  railActive,
  children,
  note,
  className,
  compact,
  tall,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  title: string;
  railActive?: string;
  children: ReactNode;
  note?: ReactNode;
  className?: string;
  compact?: boolean;
  /**
   * A taller, viewport-aware body, for a frame shown in the lightbox at full
   * width.
   *
   * The inline body is sized for a card in a grid. In the lightbox the frame is
   * the point of the screen, so the body takes a share of the *window* height
   * rather than a fixed pixel minimum: on a laptop the preview and the caption
   * below it both fit without scrolling, and on a large monitor the window grows
   * into real landscape instead of staying a squat strip. The clamp is the
   * honest way to say "as tall as there is room for, within reason".
   *
   * A minimum rather than a fixed height, deliberately: a fixed height would
   * clip a preview that is taller than the clamp — and a preview that has been
   * clipped has hidden exactly the rows the visitor opened it to read.
   */
  tall?: boolean;
}) {
  return (
    <figure className={cn("plate shadow-raised", className)}>
      {/* ── Window chrome ─────────────────────────────────────────── */}
      <div className="flex items-center justify-between gap-3 border-b border-line/70 bg-ink-900/85 px-3 py-2 sm:px-4">
        <div className="flex items-center gap-2.5">
          <LogoMark size={20} />
          <span className="font-mono text-[0.75rem] font-semibold uppercase tracking-[0.18em] text-text-faint">
            {t("ProGlass Automation")}
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
                <p className="px-1.5 pb-0.5 font-mono text-[0.6875rem] font-semibold tracking-[0.16em] text-text-faint">
                  {t(group.label)}
                </p>
                <div className="space-y-px">
                  {group.items.slice(0, group.open ? group.items.length : 2).map((item) => (
                    <RailRow key={item.label} label={t(item.label)} active={item.label === railActive} />
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
              <span className="hidden font-mono text-[0.6875rem] tracking-[0.14em] text-text-faint font-medium sm:inline">10:30</span>
              <span className="h-5 w-5 rounded-[0.3rem] border border-line-strong/70 bg-ink-800" />
              <span className="flex items-center gap-1.5 rounded-[0.35rem] border border-line-strong/70 bg-ink-800 px-1.5 py-0.5">
                <span className="grid h-4 w-4 place-items-center rounded-full bg-brand-400/20 text-[0.625rem] font-bold text-brand-200">
                  AB
                </span>
                <span className="font-mono text-3xs uppercase tracking-[0.08em] text-text-faint font-medium">Admin</span>
              </span>
            </div>
          </div>

          <div
            className={cn(
              "bg-ink-850/30 p-3 sm:p-5",
              tall ? "min-h-[clamp(17rem,58vh,36rem)]" : "min-h-[13rem] sm:min-h-[16rem]",
            )}
          >
            {children}
          </div>
        </div>
      </div>

      {note ? (
        <figcaption className="border-t border-line/60 bg-ink-950/60 px-3 py-2.5 text-xs leading-relaxed text-text-faint font-medium sm:px-4">
          {note}
        </figcaption>
      ) : null}
    </figure>
  );
}
