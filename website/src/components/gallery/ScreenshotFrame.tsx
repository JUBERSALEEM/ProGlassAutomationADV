import Image from "next/image";
import { AppFrame, CapturePending } from "@/components/product/AppFrame";
import type { Screenshot } from "@/data/screenshots";
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
 *              around an explicit "capture pending" plate. The rail labels are
 *              the application's own; the content area is not faked.
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
            sizes={modal ? "(max-width: 1024px) 100vw, 900px" : "(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 33vw"}
            className="object-cover"
            priority={priority}
          />
        </div>
        <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3">
          <p className="text-sm font-medium text-text-primary">{shot.title}</p>
          <p className="mt-1 text-xs leading-relaxed text-text-faint">
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

  return (
    <AppFrame
      title={shot.title}
      railActive={railLabelFor(shot.moduleKey)}
      className={className}
      note={
        <>
          <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300/80">Capture pending</span> —
          {" "}
          {shot.caption}
        </>
      }
    >
      <CapturePending module={shot.title} detail={shot.highlights[0]} />
    </AppFrame>
  );
}

/** Maps a module key to its navigation-rail label. */
function railLabelFor(moduleKey: string): string {
  const map: Record<string, string> = {
    Dashboard: "Dashboard",
    ProformaInvoice: "Proforma Invoice",
    SheetStore: "Sheet Store",
    DeliveryNote: "Delivery Note",
    TaxInvoice: "Tax Invoice",
    Optimization: "Optimization",
    ProductionControlCenter: "Production Control Center",
    Settings: "Settings",
    VatSubmission: "VAT Submission",
    SalesReport: "Overview",
    About: "Dashboard",
  };
  return map[moduleKey] ?? moduleKey;
}
