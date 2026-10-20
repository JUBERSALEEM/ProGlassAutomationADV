import { cn } from "@/lib/cn";

/**
 * The application's own brand emblem — the isometric glass cube.
 *
 * This is not a website invention and not a variation. The three face paths
 * below are transcribed coordinate-for-coordinate from the splash screen the
 * desktop application opens with:
 *
 *   Views/Splash/SplashView.xaml — "Studio Ivory & Glass", concept #5
 *     top    M80,6   L150,44 L80,82  L10,44  Z     Sp.FaceTop
 *     left   M10,44  L10,122 L80,160 L80,82  Z     Sp.FaceLeft
 *     right  M150,44 L150,122 L80,160 L80,82 Z     Sp.FaceRight  (GlassFacePath)
 *     sheen  M138,58 L138,112 L96,136              40% white, 3 units
 *
 * The grid the splash lays those out on is 160 × 170, and that is the viewBox
 * here, so the mark is the same drawing at any size rather than a redraw that
 * happens to look similar.
 *
 * What *is* the website's own is the colour. The splash paints its faces from
 * three fixed brushes (#EFEAFB→#B4A1E1, #8568CF→#5A3FA8, #A78BDA→#6546B5);
 * this paints them from the site's design tokens, so the emblem follows the
 * mode and the appearance family exactly as the rest of the page does. The
 * three token pairs were chosen to land on the splash's own three brushes in
 * the live brand ramp at the dark mode's default — token-driven, never a fixed redraw.
 *
 * `SplashThemeManager` can re-tint `GlassFacePath` in the application; the
 * `tinted` prop is the same idea here, and it is what lets a page show the
 * emblem in a family accent without drawing a second mark.
 */
export function GlassCubeEmblem({
  className,
  size = 96,
  tinted = false,
  label = "ProGlass Automation emblem",
}: {
  className?: string;
  size?: number;
  /** Paints the right-hand face in the family's accent instead of the ramp. */
  tinted?: boolean;
  label?: string | null;
}) {
  return (
    <svg
      viewBox="0 0 160 170"
      width={size}
      height={(size * 170) / 160}
      className={cn("notranslate shrink-0", className)}
      role={label ? "img" : "presentation"}
      aria-label={label ?? undefined}
      aria-hidden={label ? undefined : true}
      data-notranslate=""
    >
      {/* Top face — the sheet as it lies on the rack, catching the light. */}
      <path
        d="M80,6 L150,44 L80,82 L10,44 Z"
        fill="url(#pg-cube-top)"
        stroke="rgb(255 255 255 / 0.62)"
        strokeWidth="2"
        strokeLinejoin="round"
      />
      {/* Left face — the shaded side of the build-up. */}
      <path
        d="M10,44 L10,122 L80,160 L80,82 Z"
        fill="url(#pg-cube-left)"
        stroke="rgb(255 255 255 / 0.62)"
        strokeWidth="2"
        strokeLinejoin="round"
      />
      {/* Right face — the one the application re-tints with the accent. */}
      <path
        d="M150,44 L150,122 L80,160 L80,82 Z"
        fill={tinted ? "url(#pg-cube-accent)" : "url(#pg-cube-right)"}
        stroke="rgb(255 255 255 / 0.62)"
        strokeWidth="2"
        strokeLinejoin="round"
      />
      {/* The sheen the splash draws across the right face. */}
      <path
        d="M138,58 L138,112 L96,136"
        fill="none"
        stroke="rgb(255 255 255 / 0.4)"
        strokeWidth="3"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

/**
 * The keyline divider the splash puts under its wordmark: a rule, a diamond,
 * the descriptor, a diamond, a rule. Reproduced here because it is the
 * product's own lockup furniture, and a site that borrows the mark but invents
 * its own divider is a site that looks like a reseller.
 *
 * The diamonds are the splash's `DiamondGlyph` path at its own 10 × 10 size.
 */
export function BrandKeyline({
  label,
  className,
}: {
  label: string;
  className?: string;
}) {
  return (
    <span className={cn("flex flex-wrap items-center justify-center gap-x-3.5 gap-y-2", className)}>
      <span aria-hidden="true" className="h-px w-14 bg-line-strong" />
      <Diamond />
      <span className="font-mono text-2xs font-semibold uppercase tracking-[0.2em] text-text-faint">
        {label}
      </span>
      <Diamond />
      <span aria-hidden="true" className="h-px w-14 bg-line-strong" />
    </span>
  );
}

function Diamond() {
  return (
    <svg aria-hidden="true" viewBox="0 0 10 10" className="h-2.5 w-2.5 shrink-0">
      <path d="M 5,0 L 10,5 L 5,10 L 0,5 Z" fill="rgb(var(--brand-400) / 0.85)" />
    </svg>
  );
}
