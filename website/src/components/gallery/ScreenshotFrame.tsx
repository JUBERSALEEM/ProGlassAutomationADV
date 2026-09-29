import Image from "next/image";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import type { Screenshot } from "@/data/screenshots";
import { moduleByKey, modules } from "@/data/modules";
import { cn } from "@/lib/cn";

/**
 * Renders one screenshot entry.
 *
 * Three cases, and each one looks different on purpose so a visitor is never
 * misled about what they are looking at:
 *
 *   capture  → a real screen capture of the running application.
 *   repo     → a genuine asset that ships inside the WPF repository, captioned
 *              with what it actually is.
 *   pending  → no capture exists yet, so the real application chrome is drawn
 *              around a live, interactive reconstruction of that module. The
 *              rail labels, the tabs, the field names and the arithmetic are
 *              the application's own; the data is labelled sample data and the
 *              caption says plainly that it is not a screen capture.
 */
export function ScreenshotFrame({
  shot,
  className,
  priority,
  modal,
}: {
  shot: Screenshot;
  className?: string;
  priority?: boolean;
  modal?: boolean;
}) {
  if (shot.src && shot.source !== "pending") {
    return (
      <figure className={cn("plate shadow-raised", className)}>
        <div className="relative aspect-[3/2] w-full overflow-hidden">
          <Image
            src={shot.src}
            alt={shot.assetNote ?? shot.title}
            fill
            sizes={
              modal
                ? // The lightbox image column is 1.7fr of a max-w-5xl grid, so it
                  // is ~630px wide once the container padding and gap are taken off.
                  "(max-width: 1024px) 100vw, 640px"
                : // The gallery grid itself is `sm:grid-cols-2 xl:grid-cols-3`, so the
                  // half-width guess has to hold until 1280px, not until 1024px.
                  "(max-width: 640px) 100vw, (max-width: 1280px) 50vw, 33vw"
            }
            className="object-cover"
            priority={priority}
          />
        </div>
        <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3">
          <p className="text-sm font-medium text-text-primary">{shot.title}</p>
          <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">
            {shot.assetNote ??
              shot.caption}
          </p>
          <p className="mono-label mt-2">
            {shot.source === "capture" ? "Screen capture" : "Illustration shipped inside the application"} ·{" "}
            {shot.evidence}
          </p>
        </figcaption>
      </figure>
    );
  }

  const owner = modules.find((item) => item.moduleKey === shot.moduleKey) ?? moduleByKey(shot.moduleKey);

  return (
    <AppFrame
      title={shot.title}
      railActive={railLabelFor(shot.moduleKey)}
      className={className}
      note={
        <>
          <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
            Interactive preview
          </span>{" "}
          — this is a working reconstruction of the {shot.title} screen, not a screen capture. {shot.caption}
        </>
      }
    >
      <LiveSurface
        moduleKey={shot.moduleKey}
        name={shot.title}
        capabilities={owner?.capabilities ?? shot.highlights}
        /* A slide: the run is shown as a strip of plates, not paged. */
        compact
      />
    </AppFrame>
  );
}

/** Maps a module key to its navigation-rail label. */
function railLabelFor(moduleKey: string): string {
  const map: Record<string, string> = {
    Dashboard: "Dashboard",
    SheetStore: "Sheet Store",
    Specifications: "Specifications",
    Inquiry: "Inquiry",
    ProformaInvoice: "Proforma Invoice",
    JobOrder: "Job Order",
    TaxInvoice: "Tax Invoice",
    Optimization: "Optimization",
    ProcessWorkCenter: "Process Work Center",
    ProductionControlCenter: "Production Control Center",
    Cutting: "Cutting",
    HolesCutouts: "Holes & Cutouts",
    Polish: "Polish",
    EdgeDeletion: "Edge Deletion",
    Mitring: "Mitring",
    Tempering: "Tempering",
    Pairing: "Pairing",
    DoublingLamination: "Doubling / Lamination",
    DeliveryNote: "Delivery Note",
    OrderReport: "Order Report",
    StatementGenerator: "Statement Generator",
    JobProfitability: "Job Profitability",
    VatSubmission: "VAT Submission",
    SalesReport: "Executive Summary",
    CompanyCenter: "Company Center",
    UserCenter: "User Center",
    SystemCenter: "System Center",
    Diagnostics: "Diagnostics",
    Settings: "Settings",
    About: "Dashboard",
    Licensing: "Settings",
  };
  return map[moduleKey] ?? moduleKey;
}
