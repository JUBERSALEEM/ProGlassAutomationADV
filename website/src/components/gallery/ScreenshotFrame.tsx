import Image from "next/image";
import { AppFrame } from "@/components/product/AppFrame";
import { LiveSurface } from "@/components/product/LiveSurface";
import type { Screenshot } from "@/data/screenshots";
import { moduleByKey, modules } from "@/data/modules";
import { cn } from "@/lib/cn";
import type { Translator } from "@/i18n/core";

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
  /** The page's translator — this component renders on the server. */
  t,
  shot,
  className,
  priority,
  modal,
}: {
  /** The page's translator — this component renders on the server. */
  t: Translator;
  shot: Screenshot;
  className?: string;
  priority?: boolean;
  modal?: boolean;
}) {
  if (shot.src && shot.source !== "pending") {
    return (
      <figure className={cn("surface-raised", className, modal && "mx-auto w-fit max-w-full")}>
        {/*
          Opened large, the asset is fitted to the window.
          
          A screenshot viewer has one job: show the screen as large as the
          window allows, whole. So in the lightbox the frame is not a fixed
          3:2 box with a letterbox inside it — the image is capped by the
          viewport height and by the column width, which means the picture is
          as big as it can be and still be complete. The frame hugs it, and the
          plate border sits around the picture rather than around empty space.
          
          Inline nothing changes: the gallery is a grid of equal cards, so those
          keep the 3:2 crop.
        */}
        <div
          className={cn(
            "relative w-full overflow-hidden",
            modal ? "bg-ink-950" : "aspect-[3/2]",
          )}
        >
          {modal ? (
            /* Intrinsic size plus the asset's own ratio, capped by the viewport
               height and the column width: the picture is as large as the
               window allows and still whole. */
            <Image
              src={shot.src}
              alt={t(shot.assetNote ?? shot.title)}
              width={shot.width}
              height={shot.height}
              sizes="(max-width: 1024px) 100vw, 1760px"
              className="mx-auto block h-auto max-h-[calc(100dvh-14rem)] w-auto max-w-full object-contain"
            />
          ) : (
          <Image
            src={shot.src}
            alt={t(shot.assetNote ?? shot.title)}
            fill
            // The gallery grid is `sm:grid-cols-2 xl:grid-cols-3`, so the
            // half-width guess has to hold until 1280px, not until 1024px.
            sizes="(max-width: 640px) 100vw, (max-width: 1280px) 50vw, 33vw"
            className="object-cover"
            priority={priority}
          />
          )}
        </div>
        <figcaption className="border-t border-line/60 bg-ink-950/60 px-4 py-3">
          <p className="text-sm font-medium text-text-primary">{t(shot.title)}</p>
          <p className="mt-1 text-xs leading-relaxed text-text-faint font-medium">
            {t(shot.assetNote ??
              shot.caption)}
          </p>
          <p className="mono-label mt-2">
            {shot.source === "capture" ? t("Screen capture") : t("Illustration shipped inside the application")} ·{" "}
            {t(shot.evidence)}
          </p>
        </figcaption>
      </figure>
    );
  }

  const owner = modules.find((item) => item.moduleKey === shot.moduleKey) ?? moduleByKey(shot.moduleKey);

  return (
    <AppFrame
      t={t}
      title={t(shot.title)}
      railActive={railLabelFor(shot.moduleKey)}
      className={className}
      /* Opened large, the preview is given a taller body than it needs inline. */
      tall={modal}
      note={
        <>
          <span className="font-mono text-2xs uppercase tracking-[0.16em] text-brand-300">
            {t("Interactive preview")}
          </span>{" "}
          {t(
            "— this is a working reconstruction of the {title} screen, not a screen capture. {caption}",
            { title: t(shot.title), caption: t(shot.caption) },
          )}
        </>
      }
    >
      <LiveSurface
        moduleKey={shot.moduleKey}
        name={t(shot.title)}
        capabilities={owner?.capabilities ?? shot.highlights}
        /* Which screen of the module this slot is, when it has more than one. */
        variant={shot.previewVariant}
        /* A slide: the run is shown as a strip of plates, not paged. */
        compact
      />
    </AppFrame>
  );
}

/**
 * Maps a module key to the navigation-rail label it sits under.
 *
 * Only the labels that are really in the rail — the three flat items and the
 * seven expandable groups' children. A module reached from inside another one
 * (Process Work Center and Production Control Center from Production, the Order
 * Report from Reports, Settings and About from the shell's own menu) has no
 * rail entry to highlight, so it falls through to its own name and nothing
 * lights up rather than lighting up the wrong thing.
 */
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
    /* The reports hub is the Overview item in the Reports group. */
    SalesReport: "Overview",
    CompanyCenter: "Company Center",
    UserCenter: "User Center",
    SystemCenter: "System Center",
    Diagnostics: "Logs & Activity",
    Settings: "Settings",
  };
  return map[moduleKey] ?? moduleKey;
}
