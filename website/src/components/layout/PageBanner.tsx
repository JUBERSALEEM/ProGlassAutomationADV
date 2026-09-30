import { cn } from "@/lib/cn";

/**
 * Page banner artwork.
 *
 * Every page opens with a full-width banner drawing its own subject: the
 * nesting page gets a cutting plan, the reports page gets report columns, the
 * pricing page gets editions, the sponsors page gets the gaps. They are line
 * drawings rather than pictures, and they are painted from the same tokens as
 * the rest of the site, so they re-tint with the palette family and follow the
 * appearance studio — including the corner radius.
 *
 * Nothing here claims to be a screen of the product: these are diagrams of the
 * subject a page is about, and the caption under each page title says what the
 * page covers.
 */

export type BannerArt =
  | "product"
  | "nesting"
  | "build-up"
  | "production"
  | "dispatch"
  | "finance"
  | "reports"
  | "calculator"
  | "people"
  | "support"
  | "writing"
  | "licence";

const paths: Record<BannerArt, { label: string; cells: { x: number; y: number; w: number; h: number }[] }> = {
  product: {
    label: "Module grid",
    cells: [
      { x: 0, y: 0, w: 3, h: 2 }, { x: 3, y: 0, w: 2, h: 1 }, { x: 5, y: 0, w: 2, h: 2 },
      { x: 3, y: 1, w: 2, h: 1 }, { x: 0, y: 2, w: 2, h: 1 }, { x: 2, y: 2, w: 3, h: 2 },
      { x: 5, y: 2, w: 2, h: 1 }, { x: 0, y: 3, w: 2, h: 1 }, { x: 5, y: 3, w: 2, h: 1 },
      { x: 0, y: 4, w: 3, h: 1 }, { x: 3, y: 4, w: 4, h: 1 },
    ],
  },
  nesting: {
    label: "Guillotine plan",
    cells: [
      { x: 0, y: 0, w: 3, h: 3 }, { x: 3, y: 0, w: 2, h: 2 }, { x: 5, y: 0, w: 2, h: 3 },
      { x: 3, y: 2, w: 2, h: 1 }, { x: 0, y: 3, w: 2, h: 2 }, { x: 2, y: 3, w: 1, h: 2 },
      { x: 3, y: 3, w: 4, h: 1 }, { x: 3, y: 4, w: 2, h: 1 }, { x: 5, y: 4, w: 2, h: 1 },
    ],
  },
  "build-up": {
    label: "Glazing build-up",
    cells: [
      { x: 0, y: 0, w: 1, h: 5 }, { x: 2, y: 0, w: 1, h: 5 }, { x: 4, y: 0, w: 1, h: 5 },
      { x: 6, y: 0, w: 1, h: 5 },
    ],
  },
  production: {
    label: "Stage queues",
    cells: [
      { x: 0, y: 0, w: 1, h: 3 }, { x: 1, y: 1, w: 1, h: 3 }, { x: 2, y: 0, w: 1, h: 4 },
      { x: 3, y: 1, w: 1, h: 2 }, { x: 4, y: 0, w: 1, h: 3 }, { x: 5, y: 2, w: 1, h: 3 },
      { x: 6, y: 1, w: 1, h: 4 },
    ],
  },
  dispatch: {
    label: "Movements",
    cells: [
      { x: 0, y: 0, w: 4, h: 1 }, { x: 0, y: 1, w: 2, h: 1 }, { x: 2, y: 1, w: 2, h: 1 },
      { x: 0, y: 2, w: 3, h: 1 }, { x: 3, y: 2, w: 1, h: 1 }, { x: 0, y: 3, w: 2, h: 1 },
      { x: 2, y: 3, w: 2, h: 1 },
    ],
  },
  finance: {
    label: "Ledger",
    cells: [
      { x: 0, y: 0, w: 1, h: 4 }, { x: 2, y: 1, w: 1, h: 3 }, { x: 4, y: 0, w: 1, h: 2 },
      { x: 5, y: 2, w: 1, h: 2 }, { x: 6, y: 1, w: 1, h: 3 },
    ],
  },
  reports: {
    label: "Report columns",
    cells: [
      { x: 0, y: 0, w: 2, h: 2 }, { x: 2, y: 0, w: 2, h: 4 }, { x: 4, y: 0, w: 2, h: 3 },
      { x: 0, y: 2, w: 2, h: 2 }, { x: 4, y: 3, w: 2, h: 1 },
    ],
  },
  calculator: {
    label: "Area and weight",
    cells: [
      { x: 0, y: 0, w: 3, h: 4 }, { x: 4, y: 1, w: 1, h: 1 }, { x: 5, y: 1, w: 1, h: 1 },
      { x: 4, y: 2, w: 1, h: 1 }, { x: 5, y: 2, w: 1, h: 1 }, { x: 4, y: 3, w: 2, h: 1 },
    ],
  },
  people: {
    label: "Roles",
    cells: [
      { x: 1, y: 0, w: 1, h: 1 }, { x: 3, y: 0, w: 1, h: 1 }, { x: 5, y: 0, w: 1, h: 1 },
      { x: 0, y: 1, w: 1, h: 2 }, { x: 2, y: 1, w: 1, h: 2 }, { x: 4, y: 1, w: 1, h: 2 },
      { x: 6, y: 1, w: 1, h: 2 }, { x: 1, y: 3, w: 5, h: 1 },
    ],
  },
  support: {
    label: "Sponsorship",
    cells: [
      { x: 0, y: 0, w: 3, h: 1 }, { x: 0, y: 1, w: 1, h: 3 }, { x: 2, y: 1, w: 1, h: 3 },
      { x: 4, y: 0, w: 3, h: 1 }, { x: 4, y: 1, w: 1, h: 3 }, { x: 6, y: 1, w: 1, h: 3 },
    ],
  },
  writing: {
    label: "Notes",
    cells: [
      { x: 0, y: 0, w: 5, h: 1 }, { x: 0, y: 1, w: 7, h: 1 }, { x: 0, y: 2, w: 4, h: 1 },
      { x: 0, y: 3, w: 7, h: 1 }, { x: 0, y: 4, w: 3, h: 1 },
    ],
  },
  licence: {
    label: "Editions",
    cells: [
      { x: 0, y: 0, w: 1, h: 4 }, { x: 2, y: 0, w: 1, h: 3 }, { x: 4, y: 0, w: 1, h: 2 },
      { x: 6, y: 0, w: 1, h: 1 },
    ],
  },
};

const unit = 26;
const gap = 4;
const cols = 7;

export function PageBanner({
  /** The page's translator — this component renders on the server. */
  art,
  className,
  /** Text is laid over the left of the banner, so the art sits right. */
  align = "right",
}: {
  art: BannerArt;
  className?: string;
  align?: "left" | "right";
}) {
  const { cells, label } = paths[art];
  const width = cols * unit;
  const height = 5 * unit;

  return (
    <div
      aria-hidden="true"
      className={cn(
        "pointer-events-none absolute inset-0 overflow-hidden",
        align === "right" ? "opacity-[0.5]" : "opacity-[0.35]",
        className,
      )}
    >
      <svg
        viewBox={`0 0 ${width} ${height}`}
        preserveAspectRatio="xMaxYMid slice"
        className={cn(
          "absolute inset-y-0 h-full",
          align === "right" ? "right-0 w-full max-w-[62rem] sm:w-[70%]" : "left-0 w-full",
        )}
      >

        {cells.map((cell, index) => (
          <g key={`cell-${cell.x}-${cell.y}-${index}`}>
            <rect
              x={cell.x * unit + gap / 2}
              y={cell.y * unit + gap / 2}
              width={cell.w * unit - gap}
              height={cell.h * unit - gap}
              rx={6}
              fill="url(#pg-banner)"
              stroke="rgb(var(--brand-300) / 0.28)"
              strokeWidth="0.7"
              className="animate-pulse-soft"
              style={{ animationDelay: `${(index % 6) * 520}ms` }}
            />
            {/* A single glint across each pane, so the art reads as glass. */}
            <rect
              x={cell.x * unit + gap / 2}
              y={cell.y * unit + gap / 2}
              width={(cell.w * unit - gap) * 0.42}
              height="2"
              rx="1"
              fill="rgb(255 255 255 / 0.28)"
            />
          </g>
        ))}

        {/* Fade the art out towards the side the heading sits on. */}
        <rect x="0" y="0" width={width} height={height} fill="url(#pg-banner-fade)" />
      </svg>

      <span className="sr-only">{label}</span>
    </div>
  );
}
